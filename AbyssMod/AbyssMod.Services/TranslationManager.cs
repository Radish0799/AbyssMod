using Object = UnityEngine.Object;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AbyssMod.Patches;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using TMPro;
using UnityEngine;
using Utility.Fonts;
using Utility.Toast;

namespace AbyssMod.Services;

public class TranslationManager
{
	private static readonly HashSet<string> CriticalTypes = new HashSet<string> { "names", "ui_texts" };

	private readonly TranslationCache _cache;

	private readonly FontHelper _font;

	private readonly object _loadLock = new object();

	private Task _loadTask;

	private readonly ConcurrentDictionary<string, Task> _loadingNovels = new ConcurrentDictionary<string, Task>();

	private readonly Dictionary<string, Dictionary<string, string>> _tables = new Dictionary<string, Dictionary<string, string>>();

	public Dictionary<string, string> Names { get; private set; } = new Dictionary<string, string>();

	public Dictionary<string, string> Titles { get; private set; } = new Dictionary<string, string>();

	public Dictionary<string, string> Descriptions { get; private set; } = new Dictionary<string, string>();

	public Dictionary<string, string> Texts { get; private set; } = new Dictionary<string, string>();

	public Dictionary<string, string> AbilityDescriptions { get; private set; } = new Dictionary<string, string>();

	public ConcurrentDictionary<string, Dictionary<string, string>> Novels { get; private set; } = new ConcurrentDictionary<string, Dictionary<string, string>>();

	public FontHelper Font => _font;

	public TranslationManager(TranslationCache cache, FontHelper font)
	{
		_cache = cache;
		_font = font;
	}

	public void Initialize()
	{
		Plugin.Instance.StartCoroutine(_font.LoadAsync(() =>
		{
			Logger.Info("Font loaded: " + ((Object)_font.Asset).name);
			TMP_Settings.fallbackFontAssets.Add(_font.Asset);
		}).WrapToIl2Cpp());
		EnsureStaticTranslationsLoadedAsync();
	}

	public Task EnsureStaticTranslationsLoadedAsync()
	{
		lock (_loadLock)
		{
			return _loadTask ?? (_loadTask = LoadTranslationAsync());
		}
	}

	public void EnsureStaticTranslationsLoaded()
	{
		EnsureStaticTranslationsLoadedAsync().GetAwaiter().GetResult();
	}

	public Dictionary<string, string> GetTable(string type)
	{
		if (!_tables.TryGetValue(type, out var value))
		{
			return null;
		}
		return value;
	}

	public async Task LoadTranslationAsync()
	{
		if (!Config.Translation.Value)
		{
			return;
		}
		await _cache.FetchManifestAsync();
		Dictionary<string, Task<Dictionary<string, string>>> tasks = new Dictionary<string, Task<Dictionary<string, string>>>();
		foreach (string item in TranslationPaths.EnumerateMasterDictTypes())
		{
			tasks[item] = _cache.LoadAsync(item);
		}
		_ = new string[4] { "titles", "descriptions", "another_name", "ability_descriptions" };
		await Task.WhenAll(tasks.Values);
		foreach (var (text2, task2) in tasks)
		{
			if (task2.Result != null)
			{
				_tables[text2] = task2.Result;
				Logger.Info($"Translation loaded [{text2}]. Total: {task2.Result.Count}");
			}
			else
			{
				Logger.Warn("Translation load failed [" + text2 + "]");
				if (CriticalTypes.Contains(text2))
				{
					Toast.Warn("加载失败", "翻译加载失败: " + text2);
				}
			}
		}
		Names = GetTable("names") ?? new Dictionary<string, string>();
		Titles = GetTable("titles") ?? new Dictionary<string, string>();
		Descriptions = GetTable("descriptions") ?? new Dictionary<string, string>();
		AbilityDescriptions = GetTable("ability_descriptions") ?? GetTable("m_ability_details") ?? new Dictionary<string, string>();
		await _cache.SyncLegacyUiMiscAsync();
		await _cache.SyncAddOnFromCdnAsync();
		await _cache.SyncOtherFromCdnAsync();
		MachineTranslator.ReloadFromDisk();
		await MergeLocalAddOnFallbackAsync();
		AbilityTextMatcher.Rebuild(AbilityDescriptions);
		TemplateTextMatcher.Rebuild(Texts, Titles, Descriptions);
		GeneralTextPatch.RefreshAllVisibleText();
	}

	private async Task MergeLocalAddOnFallbackAsync()
	{
		Dictionary<string, string> merged = new Dictionary<string, string>();
		HashSet<string> masterKeys = BuildMasterDataKeySet();
		MergeTable("titles");
		MergeTable("descriptions");
		MergeTable("another_name");
		MergeTable("ability_descriptions");
		string value = Config.TranslationLanguage.Value;
		List<string> localCategories = (from cat in TranslationPaths.EnumerateLocalCategories(_cache.CacheDir, value)
			where cat != "ui"
			select cat).ToList();
		if (localCategories.Count > 0)
		{
			List<Task<Dictionary<string, string>>> localTasks = localCategories.Select((string cat) => _cache.LoadAsync(cat)).ToList();
			await Task.WhenAll(localTasks);
			for (int num = 0; num < localCategories.Count; num++)
			{
				MergeInto(merged, localTasks[num].Result, "add-on/" + localCategories[num], masterKeys);
			}
		}
		foreach (string item in TranslationPaths.EnumerateMasterDictTypes())
		{
			if (item.StartsWith("m_", StringComparison.Ordinal))
			{
				MergeTable(item);
			}
		}
		MergeTable("names", masterKeys);
		MergeTable("ui_texts", masterKeys);
		Texts = merged;
		Logger.Info($"Non-story text fallback merged. Total: {Texts.Count} (add-on: {localCategories.Count}, m_* keys: {masterKeys.Count})");
		void MergeTable(string type, ISet<string> skipKeys = null)
		{
			if (_tables.TryGetValue(type, out var value2))
			{
				MergeInto(merged, value2, type, skipKeys);
			}
		}
	}

	private HashSet<string> BuildMasterDataKeySet()
	{
		HashSet<string> hashSet = new HashSet<string>();
		foreach (string item in TranslationPaths.EnumerateMasterDictTypes())
		{
			if (!item.StartsWith("m_", StringComparison.Ordinal) || !_tables.TryGetValue(item, out var value))
			{
				continue;
			}
			foreach (string key in value.Keys)
			{
				hashSet.Add(key);
			}
		}
		return hashSet;
	}

	private static void MergeInto(Dictionary<string, string> target, Dictionary<string, string> source, string label, ISet<string> skipKeys = null)
	{
		if (source == null || source.Count == 0)
		{
			return;
		}
		int num = 0;
		foreach (KeyValuePair<string, string> item in source)
		{
			if (skipKeys == null || !skipKeys.Contains(item.Key))
			{
				target[item.Key] = item.Value;
				num++;
			}
		}
		Logger.Info($"Text fallback '{label}' merged. Added: {num} (source: {source.Count})");
	}

	public async Task GetNovelTranslationAsync(string novelId)
	{
		if (Novels.ContainsKey(novelId))
		{
			return;
		}
		TaskCompletionSource tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Task orAdd = _loadingNovels.GetOrAdd(novelId, tcs.Task);
		if (orAdd == tcs.Task)
		{
			try
			{
				Dictionary<string, string> dictionary = await _cache.LoadAsync("novels", novelId);
				if (dictionary != null)
				{
					Novels[novelId] = dictionary;
					Logger.Info($"Scenario translation loaded. Total: {dictionary.Count}");
				}
				else
				{
					Logger.Warn("Translations loaded failed: " + novelId);
					Toast.Warn("加载失败", "剧本ID: " + novelId);
				}
				tcs.SetResult();
				return;
			}
			catch (Exception exception)
			{
				tcs.SetException(exception);
				throw;
			}
			finally
			{
				_loadingNovels.TryRemove(novelId, out var _);
			}
		}
		await orAdd;
	}
}
