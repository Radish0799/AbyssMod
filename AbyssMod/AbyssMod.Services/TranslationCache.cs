using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AbyssCdnRouter;
using Utility.Toast;

namespace AbyssMod.Services;

public class TranslationCache
{
	private readonly string _cdn;

	private readonly string _cacheDir;

	private readonly string _language;

	private readonly HttpClient _client;

	private Manifest _manifest;

	private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();

	private int _lockCleanupCounter;

	private const int LockCleanupInterval = 32;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		WriteIndented = false
	};

	private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	public Manifest Manifest => _manifest;

	public string CacheDir => _cacheDir;

	public TranslationCache(string cdn, string cacheDir, string language, HttpClient client)
	{
		_cdn = cdn.TrimEnd('/');
		_cacheDir = cacheDir;
		_language = language;
		_client = client;
		Directory.CreateDirectory(_cacheDir);
	}

	public async Task FetchManifestAsync()
	{
		if (string.IsNullOrWhiteSpace(_cdn))
		{
			TryLoadLocalManifest(TranslationPaths.BuildCachePath(_cacheDir, "manifest", _language));
			return;
		}
		string url = TranslationPaths.BuildRemoteUrl(_cdn, "manifest", _language);
		string path = TranslationPaths.BuildCachePath(_cacheDir, "manifest", _language);
		try
		{
			HttpResponseMessage response = await CdnRouter.GetDualAsync(_client, url, preferRaw: true);
			if (response.IsSuccessStatusCode)
			{
				string text = await response.Content.ReadAsStringAsync();
				_manifest = JsonSerializer.Deserialize<Manifest>(text);
				if (_manifest != null)
				{
					EnsureCacheDirectory(path);
					await File.WriteAllTextAsync(path, text, Utf8);
					Logger.Info("Manifest loaded (" + _language + "). Hash: " + _manifest.Hash);
					return;
				}
				Logger.Warn("Remote manifest parse returned null");
			}
			Logger.Warn($"Manifest fetch returned {response.StatusCode}");
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to fetch manifest: " + ex.Message);
		}
		TryLoadLocalManifest(path);
	}

	private void TryLoadLocalManifest(string path)
	{
		if (!File.Exists(path))
		{
			Logger.Warn("No local manifest cache available, will fetch without hash verification.");
			Toast.Warn("翻译服务", "翻译清单不可用，将直接请求翻译");
			return;
		}
		try
		{
			string json = File.ReadAllText(path, Utf8);
			_manifest = JsonSerializer.Deserialize<Manifest>(json);
			if (_manifest != null)
			{
				Logger.Info("Loaded cached manifest from local (" + _language + "). Hash: " + _manifest.Hash);
				Toast.Warn("翻译服务", "无法连接远程，使用本地翻译清单");
			}
			else
			{
				Logger.Warn("Cached manifest parse returned null");
			}
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to load local manifest: " + ex.Message);
		}
	}

	public async Task SyncLegacyUiMiscAsync()
	{
		if (string.IsNullOrWhiteSpace(_cdn))
		{
			return;
		}
		string key = _language + "/legacy/add-on/ui_misc";
		string legacyCachePath = TranslationPaths.BuildLegacyAddOnCachePath(_cacheDir, "ui_misc", _language);
		string addOnCachePath = TranslationPaths.BuildAddOnCachePath(_cacheDir, "ui_misc", _language);
		string remoteUrl = TranslationPaths.BuildLegacyAddOnRemoteUrl(_cdn, "ui_misc", _language);
		string expectedHash = GetAddOnManifestHash("ui_misc");
		SemaphoreSlim semaphore = _locks.GetOrAdd(key, (string _) => new SemaphoreSlim(1, 1));
		await semaphore.WaitAsync();
		try
		{
			if (expectedHash != null && File.Exists(legacyCachePath) && HashFile(legacyCachePath) == expectedHash)
			{
				CopyLegacyToAddOnIfNewer(legacyCachePath, addOnCachePath);
				Logger.Info("legacy/add-on/ui_misc cache hit");
				return;
			}
			Dictionary<string, string> dictionary;
			try
			{
				HttpResponseMessage httpResponseMessage = await CdnRouter.GetDualAsync(_client, remoteUrl, preferRaw: false);
				if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound)
				{
					httpResponseMessage = await CdnRouter.GetDualAsync(url: TranslationPaths.BuildAddOnRemoteUrl(_cdn, "ui_misc", _language), client: _client, preferRaw: false);
				}
				if (!httpResponseMessage.IsSuccessStatusCode)
				{
					return;
				}
				dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(await httpResponseMessage.Content.ReadAsStringAsync());
				if (dictionary == null || dictionary.Count == 0)
				{
					return;
				}
			}
			catch (Exception ex)
			{
				Logger.Warn("legacy/add-on/ui_misc fetch failed: " + ex.Message);
				return;
			}
			Dictionary<string, string> dictionary2;
			if (File.Exists(legacyCachePath))
			{
				dictionary2 = LoadFromFile(legacyCachePath) ?? new Dictionary<string, string>();
			}
			else
			{
				dictionary2 = (File.Exists(addOnCachePath) ? (LoadFromFile(addOnCachePath) ?? new Dictionary<string, string>()) : new Dictionary<string, string>());
			}
			Dictionary<string, string> dictionary3 = new Dictionary<string, string>(dictionary2);
			foreach (KeyValuePair<string, string> item in dictionary)
			{
				dictionary3[item.Key] = item.Value;
			}
			SaveToFile(legacyCachePath, dictionary3);
			SaveToFile(addOnCachePath, dictionary3);
			Logger.Info($"legacy/add-on/{"ui_misc"} synced: +{dictionary.Count} remote, {dictionary3.Count} total");
		}
		finally
		{
			semaphore.Release();
			CleanupLocksIfNeeded();
		}
	}

	private static void CopyLegacyToAddOnIfNewer(string legacyPath, string addOnPath)
	{
		if (!File.Exists(legacyPath))
		{
			return;
		}
		Dictionary<string, string> dictionary = LoadFromFile(legacyPath);
		if (dictionary == null || dictionary.Count == 0)
		{
			return;
		}
		Dictionary<string, string> dictionary2 = new Dictionary<string, string>(File.Exists(addOnPath) ? (LoadFromFile(addOnPath) ?? new Dictionary<string, string>()) : new Dictionary<string, string>());
		foreach (KeyValuePair<string, string> item in dictionary)
		{
			dictionary2[item.Key] = item.Value;
		}
		SaveToFile(addOnPath, dictionary2);
	}

	public async Task SyncAddOnFromCdnAsync()
	{
		if (!string.IsNullOrWhiteSpace(_cdn))
		{
			List<string> list = CollectAddOnCategoryCandidates().ToList();
			if (list.Count != 0)
			{
				Logger.Info($"Syncing add-on/ from CDN ({list.Count} categories)...");
				await Task.WhenAll(list.Select(SyncAddOnCategoryAsync));
			}
		}
	}

	private IEnumerable<string> CollectAddOnCategoryCandidates()
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string[] allCustomCategories = TextClassifier.AllCustomCategories;
		foreach (string item in allCustomCategories)
		{
			hashSet.Add(item);
		}
		hashSet.Add("items");
		hashSet.Add("ui");
		hashSet.Add("dictionary");
		hashSet.Add("ui_misc");
		hashSet.Add("ui_misc");
		foreach (string item2 in TranslationPaths.EnumerateLocalCategories(_cacheDir, _language))
		{
			hashSet.Add(item2);
		}
		if (_manifest?.AddOn != null)
		{
			foreach (string key in _manifest.AddOn.Keys)
			{
				hashSet.Add(key);
			}
		}
		return hashSet;
	}

	private async Task SyncAddOnCategoryAsync(string category)
	{
		string key = _language + "/add-on/" + category;
		string cachePath = TranslationPaths.BuildAddOnCachePath(_cacheDir, category, _language);
		string remoteUrl = TranslationPaths.BuildAddOnRemoteUrl(_cdn, category, _language);
		string expectedHash = GetAddOnManifestHash(category);
		SemaphoreSlim semaphore = _locks.GetOrAdd(key, (string _) => new SemaphoreSlim(1, 1));
		await semaphore.WaitAsync();
		try
		{
			if (expectedHash != null && File.Exists(cachePath) && HashFile(cachePath) == expectedHash)
			{
				Logger.Info("add-on/" + category + " cache hit");
				return;
			}
			Dictionary<string, string> dictionary;
			try
			{
				HttpResponseMessage httpResponseMessage = await CdnRouter.GetDualAsync(_client, remoteUrl, preferRaw: false);
				if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound)
				{
					return;
				}
				if (!httpResponseMessage.IsSuccessStatusCode)
				{
					Logger.Warn($"add-on/{category} fetch returned {httpResponseMessage.StatusCode}");
					return;
				}
				dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(await httpResponseMessage.Content.ReadAsStringAsync());
				if (dictionary == null || dictionary.Count == 0)
				{
					return;
				}
			}
			catch (Exception ex)
			{
				Logger.Warn("add-on/" + category + " fetch failed: " + ex.Message);
				return;
			}
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(File.Exists(cachePath) ? (LoadFromFile(cachePath) ?? new Dictionary<string, string>()) : new Dictionary<string, string>());
			foreach (KeyValuePair<string, string> item in dictionary)
			{
				dictionary2[item.Key] = item.Value;
			}
			SaveToFile(cachePath, dictionary2);
			Logger.Info($"add-on/{category} synced from CDN: +{dictionary.Count} remote, {dictionary2.Count} total");
		}
		finally
		{
			semaphore.Release();
			CleanupLocksIfNeeded();
		}
	}

	private string GetAddOnManifestHash(string category)
	{
		if (_manifest?.AddOn == null)
		{
			return null;
		}
		if (!_manifest.AddOn.TryGetValue(category, out var value))
		{
			return null;
		}
		return value;
	}

	public async Task SyncOtherFromCdnAsync()
	{
		if (!string.IsNullOrWhiteSpace(_cdn))
		{
			List<string> list = CollectOtherCategoryCandidates().ToList();
			if (list.Count != 0)
			{
				Logger.Info($"Syncing other/ from CDN ({list.Count} categories)...");
				await Task.WhenAll(list.Select(SyncOtherCategoryAsync));
			}
		}
	}

	private IEnumerable<string> CollectOtherCategoryCandidates()
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string[] allCustomCategories = TextClassifier.AllCustomCategories;
		foreach (string item in allCustomCategories)
		{
			hashSet.Add(item);
		}
		foreach (string item2 in TranslationPaths.EnumerateOtherCategories(_cacheDir, _language))
		{
			hashSet.Add(item2);
		}
		if (_manifest?.Other != null)
		{
			foreach (string key in _manifest.Other.Keys)
			{
				hashSet.Add(key);
			}
		}
		return hashSet;
	}

	private async Task SyncOtherCategoryAsync(string category)
	{
		string key = _language + "/other/" + category;
		string cachePath = TranslationPaths.BuildOtherCachePath(_cacheDir, category, _language);
		string remoteUrl = TranslationPaths.BuildOtherRemoteUrl(_cdn, category, _language);
		string expectedHash = GetOtherManifestHash(category);
		SemaphoreSlim semaphore = _locks.GetOrAdd(key, (string _) => new SemaphoreSlim(1, 1));
		await semaphore.WaitAsync();
		try
		{
			if (expectedHash != null && File.Exists(cachePath) && HashFile(cachePath) == expectedHash)
			{
				Logger.Info("other/" + category + " cache hit");
				return;
			}
			Dictionary<string, string> dictionary;
			try
			{
				HttpResponseMessage httpResponseMessage = await CdnRouter.GetDualAsync(_client, remoteUrl, preferRaw: false);
				if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound)
				{
					return;
				}
				if (!httpResponseMessage.IsSuccessStatusCode)
				{
					Logger.Warn($"other/{category} fetch returned {httpResponseMessage.StatusCode}");
					return;
				}
				dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(await httpResponseMessage.Content.ReadAsStringAsync());
				if (dictionary == null || dictionary.Count == 0)
				{
					return;
				}
			}
			catch (Exception ex)
			{
				Logger.Warn("other/" + category + " fetch failed: " + ex.Message);
				return;
			}
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(File.Exists(cachePath) ? (LoadFromFile(cachePath) ?? new Dictionary<string, string>()) : new Dictionary<string, string>());
			foreach (KeyValuePair<string, string> item in dictionary)
			{
				dictionary2[item.Key] = item.Value;
			}
			SaveToFile(cachePath, dictionary2);
			Logger.Info($"other/{category} synced from CDN: +{dictionary.Count} remote, {dictionary2.Count} total");
		}
		finally
		{
			semaphore.Release();
			CleanupLocksIfNeeded();
		}
	}

	private string GetOtherManifestHash(string category)
	{
		if (_manifest?.Other == null)
		{
			return null;
		}
		if (!_manifest.Other.TryGetValue(category, out var value))
		{
			return null;
		}
		return value;
	}

	public async Task<Dictionary<string, string>> LoadAsync(string type, string id = null)
	{
		string cacheKey = ((id != null) ? $"{_language}/{type}/{id}" : (_language + "/" + type));
		string cachePath = TranslationPaths.BuildCachePath(_cacheDir, type, _language, id);
		if (!TranslationPaths.IsCdnFlatType(type) && type != "novels")
		{
			if (File.Exists(cachePath))
			{
				Dictionary<string, string> dictionary = LoadFromFile(cachePath);
				Logger.Info($"Local-only category '{type}' loaded: {dictionary?.Count ?? 0} entries");
				return dictionary;
			}
			return new Dictionary<string, string>();
		}
		string remoteUrl = TranslationPaths.BuildRemoteUrl(_cdn, type, _language, id);
		string expectedHash = GetManifestHash(type, id);
		SemaphoreSlim semaphore = _locks.GetOrAdd(cacheKey, (string _) => new SemaphoreSlim(1, 1));
		await semaphore.WaitAsync();
		try
		{
			if (string.IsNullOrWhiteSpace(_cdn))
			{
				if (File.Exists(cachePath))
				{
					Logger.Info("CDN disabled, loading local cache: " + cacheKey);
					return LoadFromFile(cachePath);
				}
				Logger.Warn("CDN disabled and no local cache for " + cacheKey);
				return new Dictionary<string, string>();
			}
			if (expectedHash != null && File.Exists(cachePath))
			{
				string text = HashFile(cachePath);
				if (text == expectedHash)
				{
					Logger.Info("Cache hit: " + cacheKey);
					return LoadFromFile(cachePath);
				}
				Logger.Info($"Cache hash mismatch for {cacheKey}, expected={expectedHash}, local={text}");
			}
			Logger.Info("Fetching from remote: " + remoteUrl);
			Dictionary<string, string> dictionary2 = await GetAsync<Dictionary<string, string>>(remoteUrl);
			if (dictionary2 != null)
			{
				if (type == "ability_descriptions" && File.Exists(cachePath))
				{
					Dictionary<string, string> dictionary3 = LoadFromFile(cachePath) ?? new Dictionary<string, string>();
					int count = dictionary2.Count;
					Dictionary<string, string> dictionary4 = new Dictionary<string, string>(dictionary3);
					foreach (KeyValuePair<string, string> item in dictionary2)
					{
						dictionary4[item.Key] = item.Value;
					}
					dictionary2 = dictionary4;
					Logger.Info($"ability_descriptions merged: {dictionary2.Count} total (remote {count}, +{dictionary2.Count - count} local-only kept)");
				}
				SaveToFile(cachePath, dictionary2);
			}
			else
			{
				Logger.Warn("Remote fetch failed for " + cacheKey + ", trying local fallback.");
				if (File.Exists(cachePath))
				{
					dictionary2 = LoadFromFile(cachePath);
					Logger.Info("Loaded stale cache for " + cacheKey);
				}
			}
			return dictionary2;
		}
		finally
		{
			semaphore.Release();
			CleanupLocksIfNeeded();
		}
	}

	private void CleanupLocksIfNeeded()
	{
		if (++_lockCleanupCounter % 32 != 0)
		{
			return;
		}
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, SemaphoreSlim> @lock in _locks)
		{
			if (@lock.Value.CurrentCount > 0)
			{
				list.Add(@lock.Key);
			}
		}
		foreach (string item in list)
		{
			if (_locks.TryRemove(item, out var value) && value.CurrentCount > 0)
			{
				value.Dispose();
			}
		}
	}

	private string GetManifestHash(string type, string id)
	{
		if (_manifest == null)
		{
			return null;
		}
		if (type == "novels" && id != null)
		{
			if (_manifest.Novels == null || !_manifest.Novels.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}
		string fileHash = _manifest.GetFileHash(type);
		if (!string.IsNullOrEmpty(fileHash))
		{
			return fileHash;
		}
		return type switch
		{
			"names" => _manifest.Names, 
			"titles" => _manifest.Titles, 
			"descriptions" => _manifest.Descriptions, 
			"another_name" => _manifest.AnotherName, 
			"ability_descriptions" => _manifest.AbilityDescriptions, 
			"ui_texts" => _manifest.UiTexts, 
			_ => null, 
		};
	}

	private async Task<T> GetAsync<T>(string url) where T : class
	{
		_ = 1;
		try
		{
			HttpResponseMessage httpResponseMessage = await CdnRouter.GetDualAsync(_client, url, preferRaw: false);
			if (httpResponseMessage.IsSuccessStatusCode)
			{
				return await httpResponseMessage.Content.ReadFromJsonAsync<T>((JsonSerializerOptions?)null, default(CancellationToken));
			}
		}
		catch (Exception ex)
		{
			Logger.Error("HTTP GET error for " + url + ": " + ex.Message);
		}
		return null;
	}

	private static Dictionary<string, string> LoadFromFile(string path)
	{
		try
		{
			return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path, Utf8));
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to load translation cache " + path + ": " + ex.Message);
			return null;
		}
	}

	private static void SaveToFile(string path, Dictionary<string, string> data)
	{
		EnsureCacheDirectory(path);
		string contents = JsonSerializer.Serialize(data, JsonOptions);
		File.WriteAllText(path, contents, Utf8);
	}

	private static void EnsureCacheDirectory(string path)
	{
		string directoryName = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
	}

	private static string HashFile(string path)
	{
		return GetHash(JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path, Utf8)));
	}

	private static string GetHash(Dictionary<string, string> dict)
	{
		if (dict == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (string item in dict.Keys.OrderBy((string k) => k, StringComparer.Ordinal))
		{
			stringBuilder.Append(item);
			stringBuilder.Append('\0');
			stringBuilder.Append(dict[item]);
			stringBuilder.Append('\0');
		}
		return Convert.ToHexString(MD5.HashData(Utf8.GetBytes(stringBuilder.ToString()))).ToLowerInvariant();
	}
}
