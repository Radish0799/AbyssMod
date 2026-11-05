using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AbyssMod.Services;

public static class MachineTranslator
{
	private static bool _initialized;

	private static string _otherDir;

	private static string _pendingPath;

	private static string _language;

	private static readonly HttpClient _http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(30.0)
	};

	private static readonly ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();

	private static readonly ConcurrentDictionary<string, string> _pending = new ConcurrentDictionary<string, string>();

	private static int _cacheDirty;

	private static int _pendingDirty;

	private const int SaveCacheEvery = 10;

	private const int RescanIntervalMs = 30000;

	private const int BetweenRequestsMs = 50;

	private static readonly object _fileLock = new object();

	private const string SystemPromptHans = "你是手机游戏《ドットアビス》的日译简体中文本地化译者。请把用户给出的日文翻译成简体中文，只输出译文本身，不要解释、不要加引号、不要输出英文。最重要：原文中形如 {0} {1} 的花括号占位符必须连同花括号原样保留，位置和数量都不能改；<...> 标签、【】符号、\\n 换行也都原样保留。术语：紋章=纹章，衝撃=冲击，情熱=热情，会心=会心，スキル=技能，マナ=魔力，バリア=护盾，付与=附加，上昇=提升，永続=永续，リトライ=重试，パーティ=队伍，ソート=排序。";

	private const string SystemPromptHant = "你是手機遊戲《ドットアビス》的日譯繁體中文（台灣）在地化譯者。請把使用者給出的日文翻譯成台灣慣用的繁體中文，只輸出譯文本身，不要解釋、不要加引號、不要輸出英文或簡體字。最重要：原文中形如 {0} {1} 的花括號佔位符必須連同花括號原樣保留，位置和數量都不能改；<...> 標籤、【】符號、\\n 換行也都原樣保留。術語：紋章=紋章，衝撃=衝擊，情熱=熱情，会心=會心，スキル=技能，マナ=魔力，バリア=護盾，付与=附加，上昇=提升，永続=永續，リトライ=重試，パーティ=隊伍，ソート=排序。";

	private static readonly object[] FewShotHans = new object[4]
	{
		new
		{
			role = "user",
			content = "自身の攻撃力が【{0}】上昇"
		},
		new
		{
			role = "assistant",
			content = "自身攻击力提升【{0}】"
		},
		new
		{
			role = "user",
			content = "紋章：衝撃を【{0}】付与 / 自身に最大HP【{1}】分の回復"
		},
		new
		{
			role = "assistant",
			content = "附加纹章：冲击【{0}】 / 回复自身最大HP的【{1}】"
		}
	};

	private static readonly object[] FewShotHant = new object[4]
	{
		new
		{
			role = "user",
			content = "自身の攻撃力が【{0}】上昇"
		},
		new
		{
			role = "assistant",
			content = "自身攻擊力提升【{0}】"
		},
		new
		{
			role = "user",
			content = "紋章：衝撃を【{0}】付与 / 自身に最大HP【{1}】分の回復"
		},
		new
		{
			role = "assistant",
			content = "附加紋章：衝擊【{0}】 / 回復自身最大HP的【{1}】"
		}
	};

	private static readonly Regex TagOrNumber = new Regex("<[^>]*>|[0-9]+(?:\\.[0-9]+)?", RegexOptions.Compiled);

	private static readonly Regex Placeholder = new Regex("\\{(\\d+)\\}", RegexOptions.Compiled);

	private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private static bool IsTraditional => string.Equals(Config.TranslationLanguage.Value, "zh_Hant", StringComparison.OrdinalIgnoreCase);

	private static string SystemPrompt
	{
		get
		{
			if (!IsTraditional)
			{
				return "你是手机游戏《ドットアビス》的日译简体中文本地化译者。请把用户给出的日文翻译成简体中文，只输出译文本身，不要解释、不要加引号、不要输出英文。最重要：原文中形如 {0} {1} 的花括号占位符必须连同花括号原样保留，位置和数量都不能改；<...> 标签、【】符号、\\n 换行也都原样保留。术语：紋章=纹章，衝撃=冲击，情熱=热情，会心=会心，スキル=技能，マナ=魔力，バリア=护盾，付与=附加，上昇=提升，永続=永续，リトライ=重试，パーティ=队伍，ソート=排序。";
			}
			return "你是手機遊戲《ドットアビス》的日譯繁體中文（台灣）在地化譯者。請把使用者給出的日文翻譯成台灣慣用的繁體中文，只輸出譯文本身，不要解釋、不要加引號、不要輸出英文或簡體字。最重要：原文中形如 {0} {1} 的花括號佔位符必須連同花括號原樣保留，位置和數量都不能改；<...> 標籤、【】符號、\\n 換行也都原樣保留。術語：紋章=紋章，衝撃=衝擊，情熱=熱情，会心=會心，スキル=技能，マナ=魔力，バリア=護盾，付与=附加，上昇=提升，永続=永續，リトライ=重試，パーティ=隊伍，ソート=排序。";
		}
	}

	private static object[] FewShot
	{
		get
		{
			if (!IsTraditional)
			{
				return FewShotHans;
			}
			return FewShotHant;
		}
	}

	public static void Initialize(string otherDir, string language)
	{
		if (!_initialized)
		{
			_initialized = true;
			_otherDir = otherDir;
			_language = language;
			try
			{
				_http.Timeout = TimeSpan.FromSeconds(Math.Max(5, Config.MTTimeout.Value));
			}
			catch
			{
			}
			Directory.CreateDirectory(otherDir);
			_pendingPath = Path.Combine(otherDir, language + ".pending.json");
			LoadAllCaches();
			PruneGraduatedKeys();
			LoadPending();
			if (Config.MTEnabled.Value)
			{
				Task.Run((Func<Task?>)PretranslateLoop);
			}
			string value = (IsTraditional ? "zh_Hant (繁體)" : "zh_Hans (簡體)");
			Logger.Info($"MachineTranslator (batch mode) initialized. Enabled={Config.MTEnabled.Value}, language={value}, cached={_cache.Count}, pending={_pending.Count}");
		}
	}

	public static void ReloadFromDisk()
	{
		if (_initialized)
		{
			_cache.Clear();
			LoadAllCaches();
			PruneGraduatedKeys();
			Logger.Info($"MachineTranslator reloaded from disk. cached={_cache.Count}");
		}
	}

	public static string Handle(string category, string text)
	{
		if (!_initialized || string.IsNullOrEmpty(text))
		{
			return text;
		}
		if (category == "name")
		{
			return text;
		}
		if (!HasKana(text))
		{
			return text;
		}
		var (key, numbers) = Normalize(text);
		if (_cache.TryGetValue(key, out var value))
		{
			string text2 = Fill(value, numbers);
			if (text2 != null)
			{
				return text2;
			}
		}
		if (!Config.MTEnabled.Value)
		{
			return text;
		}
		if (_pending.TryAdd(key, category) && Interlocked.Increment(ref _pendingDirty) % 20 == 0)
		{
			SavePending();
		}
		return text;
	}

	public static void Save()
	{
		SaveAllCaches();
		SavePending();
	}

	private static async Task PretranslateLoop()
	{
		await Task.Delay(3000);
		while (_initialized)
		{
			try
			{
				if (Config.MTEnabled.Value)
				{
					await TranslatePendingOnce();
				}
			}
			catch (Exception ex)
			{
				Logger.Error("MT batch error: " + ex.Message);
			}
			await Task.Delay(30000);
		}
	}

	private static async Task TranslatePendingOnce()
	{
		List<(string, string)> list = (from kv in _pending
			where !_cache.ContainsKey(kv.Key)
			select (template: kv.Key, category: kv.Value)).ToList();
		if (list.Count == 0)
		{
			return;
		}
		Logger.Info($"MT pretranslate: {list.Count} pending templates...");
		int done = 0;
		foreach (var (template, category) in list)
		{
			if (!Config.MTEnabled.Value)
			{
				break;
			}
			string value;
			if (_cache.ContainsKey(template))
			{
				_pending.TryRemove(template, out value);
				continue;
			}
			string text = await TranslateAsync(template);
			if (!string.IsNullOrEmpty(text))
			{
				_cache[template] = text;
				_pending.TryRemove(template, out value);
				done++;
				WriteToCategoryFile(category, template, text);
				if (Interlocked.Increment(ref _cacheDirty) % 10 == 0)
				{
					SaveAllCaches();
				}
			}
			await Task.Delay(50);
		}
		if (done > 0)
		{
			SaveAllCaches();
			SavePending();
			Logger.Info($"MT pretranslate done: +{done} translated, cache={_cache.Count}");
		}
	}

	private static string CategoryCachePath(string category)
	{
		return Path.Combine(_otherDir, category, _language + ".json");
	}

	private static void WriteToCategoryFile(string category, string template, string translated)
	{
		string path = CategoryCachePath(category);
		lock (_fileLock)
		{
			try
			{
				Dictionary<string, string> dictionary;
				if (File.Exists(path))
				{
					dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path, Utf8NoBom)) ?? new Dictionary<string, string>();
				}
				else
				{
					dictionary = new Dictionary<string, string>();
					Directory.CreateDirectory(Path.GetDirectoryName(path));
				}
				dictionary[template] = translated;
				WriteJson(path, dictionary);
			}
			catch (Exception ex)
			{
				Logger.Warn("WriteToCategoryFile failed (" + category + "): " + ex.Message);
			}
		}
	}

	private static void LoadAllCaches()
	{
		string path = Path.Combine(_otherDir, _language + ".json");
		if (File.Exists(path))
		{
			LoadCacheFile(path, "(legacy)");
		}
		if (!Directory.Exists(_otherDir))
		{
			return;
		}
		string[] directories = Directory.GetDirectories(_otherDir);
		foreach (string text in directories)
		{
			string path2 = Path.Combine(text, _language + ".json");
			if (File.Exists(path2))
			{
				string fileName = Path.GetFileName(text);
				LoadCacheFile(path2, fileName);
			}
		}
	}

	private static void PruneGraduatedKeys()
	{
		string text = Path.Combine(Path.GetDirectoryName(_otherDir), "add-on");
		if (!Directory.Exists(text) || !Directory.Exists(_otherDir))
		{
			return;
		}
		int num = 0;
		string[] directories = Directory.GetDirectories(_otherDir);
		string value;
		foreach (string obj in directories)
		{
			string fileName = Path.GetFileName(obj);
			string path = Path.Combine(obj, _language + ".json");
			if (!File.Exists(path))
			{
				continue;
			}
			string path2 = Path.Combine(text, fileName, _language + ".json");
			if (!File.Exists(path2))
			{
				continue;
			}
			try
			{
				string json = File.ReadAllText(path, Utf8NoBom);
				string json2 = File.ReadAllText(path2, Utf8NoBom);
				Dictionary<string, string> dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
				Dictionary<string, string>? obj2 = JsonSerializer.Deserialize<Dictionary<string, string>>(json2) ?? new Dictionary<string, string>();
				int count = dictionary.Count;
				foreach (string key in obj2.Keys)
				{
					if (dictionary.Remove(key))
					{
						_cache.TryRemove(key, out value);
					}
				}
				int num2 = count - dictionary.Count;
				if (num2 > 0)
				{
					WriteJson(path, dictionary);
					num += num2;
					Logger.Info($"MT prune: removed {num2} graduated key(s) from other/{fileName}");
				}
			}
			catch (Exception ex)
			{
				Logger.Warn("PruneGraduatedKeys failed for '" + fileName + "': " + ex.Message);
			}
		}
		if (num <= 0)
		{
			return;
		}
		int num3 = 0;
		foreach (string item in _pending.Keys.ToList())
		{
			if (!_cache.ContainsKey(item) && IsInAnyAddOn(text, item))
			{
				_pending.TryRemove(item, out value);
				num3++;
			}
		}
		Logger.Info($"MT prune total: {num} entries cleaned from other/, {num3} removed from pending");
	}

	private static bool IsInAnyAddOn(string addOnDir, string key)
	{
		string[] directories = Directory.GetDirectories(addOnDir);
		for (int i = 0; i < directories.Length; i++)
		{
			string path = Path.Combine(directories[i], _language + ".json");
			if (!File.Exists(path))
			{
				continue;
			}
			try
			{
				Dictionary<string, string> dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path, Utf8NoBom));
				if (dictionary != null && dictionary.ContainsKey(key))
				{
					return true;
				}
			}
			catch
			{
			}
		}
		return false;
	}

	private static void LoadCacheFile(string path, string label)
	{
		try
		{
			Dictionary<string, string> dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path, Utf8NoBom));
			if (dictionary != null)
			{
				foreach (KeyValuePair<string, string> item in dictionary)
				{
					_cache[item.Key] = item.Value;
				}
			}
			Logger.Info($"MT cache loaded from '{label}': {dictionary?.Count ?? 0} entries");
		}
		catch (Exception ex)
		{
			Logger.Warn("Load MT cache '" + label + "' failed: " + ex.Message);
		}
	}

	private static void SaveAllCaches()
	{
	}

	private static void LoadPending()
	{
		if (!File.Exists(_pendingPath))
		{
			return;
		}
		try
		{
			string json = File.ReadAllText(_pendingPath, Utf8NoBom);
			try
			{
				Dictionary<string, string> dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
				if (dictionary != null)
				{
					foreach (KeyValuePair<string, string> item in dictionary)
					{
						if (!_cache.ContainsKey(item.Key))
						{
							_pending.TryAdd(item.Key, item.Value);
						}
					}
					Logger.Info($"MT pending loaded (new format): {dictionary.Count} entries");
					return;
				}
			}
			catch
			{
			}
			List<string> list = JsonSerializer.Deserialize<List<string>>(json);
			if (list == null)
			{
				return;
			}
			foreach (string item2 in list)
			{
				if (!_cache.ContainsKey(item2))
				{
					_pending.TryAdd(item2, "ui_misc");
				}
			}
			Logger.Info($"MT pending loaded (legacy list format): {list.Count} entries");
		}
		catch (Exception ex)
		{
			Logger.Warn("Load MT pending failed: " + ex.Message);
		}
	}

	private static void SavePending()
	{
		Dictionary<string, string> data = new Dictionary<string, string>(_pending);
		WriteJson(_pendingPath, data);
	}

	private static async Task<string> TranslateAsync(string text)
	{
		_ = 3;
		try
		{
			return (Config.MTEngine.Value ?? "openai").Trim().ToLowerInvariant() switch
			{
				"claude" => await TranslateClaude(text), 
				"sugoi" => await TranslateSugoi(text), 
				"libre" => await TranslateLibre(text), 
				_ => await TranslateOpenAI(text), 
			};
		}
		catch (Exception ex)
		{
			Logger.Warn("MT request failed: " + ex.Message);
			return null;
		}
	}

	private static async Task<string> TranslateClaude(string text)
	{
		string value = Config.MTApiKey?.Value ?? "";
		string model = Config.MTModel.Value ?? "claude-haiku-4-5";
		string text2 = Config.MTEndpoint.Value;
		if (string.IsNullOrEmpty(text2) || text2.Contains("11434"))
		{
			text2 = "https://api.anthropic.com/v1/messages";
		}
		List<object> list = new List<object>();
		object[] fewShot = FewShot;
		foreach (object item in fewShot)
		{
			list.Add(item);
		}
		list.Add(new
		{
			role = "user",
			content = text
		});
		string content = JsonSerializer.Serialize(new
		{
			model = model,
			max_tokens = 512,
			system = SystemPrompt,
			messages = list
		});
		using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, text2);
		req.Content = new StringContent(content, Encoding.UTF8, "application/json");
		req.Headers.Add("x-api-key", value);
		req.Headers.Add("anthropic-version", "2023-06-01");
		using HttpResponseMessage resp = await _http.SendAsync(req);
		if (!resp.IsSuccessStatusCode)
		{
			string text3 = await resp.Content.ReadAsStringAsync();
			Logger.Warn($"Claude API {(int)resp.StatusCode}: {text3.Substring(0, Math.Min(200, text3.Length))}");
			return null;
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
		return Clean(jsonDocument.RootElement.GetProperty("content")[0].GetProperty("text").GetString());
	}

	private static async Task<string> TranslateOpenAI(string text)
	{
		List<object> list = new List<object>
		{
			new
			{
				role = "system",
				content = SystemPrompt
			}
		};
		list.AddRange(FewShot);
		list.Add(new
		{
			role = "user",
			content = text
		});
		var body = new
		{
			model = Config.MTModel.Value,
			temperature = 0,
			stream = false,
			messages = list
		};
		using HttpResponseMessage resp = await PostJson(Config.MTEndpoint.Value, body, Config.MTApiKey?.Value);
		if (resp == null)
		{
			return null;
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
		return Clean(jsonDocument.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString());
	}

	private static async Task<string> TranslateLibre(string text)
	{
		var body = new
		{
			q = text,
			source = "ja",
			target = "zh",
			format = "text"
		};
		using HttpResponseMessage resp = await PostJson(Config.MTEndpoint.Value, body);
		if (resp == null)
		{
			return null;
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
		return Clean(jsonDocument.RootElement.GetProperty("translatedText").GetString());
	}

	private static async Task<string> TranslateSugoi(string text)
	{
		var body = new
		{
			content = text,
			message = "translate sentences"
		};
		using HttpResponseMessage resp = await PostJson(Config.MTEndpoint.Value, body);
		if (resp == null)
		{
			return null;
		}
		string text2 = (await resp.Content.ReadAsStringAsync()).Trim();
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(text2);
			if (jsonDocument.RootElement.ValueKind == JsonValueKind.String)
			{
				return Clean(jsonDocument.RootElement.GetString());
			}
			if (jsonDocument.RootElement.ValueKind == JsonValueKind.Array && jsonDocument.RootElement.GetArrayLength() > 0)
			{
				return Clean(jsonDocument.RootElement[0].GetString());
			}
		}
		catch
		{
		}
		return Clean(text2);
	}

	private static async Task<HttpResponseMessage> PostJson(string url, object body, string apiKey = null)
	{
		string content = JsonSerializer.Serialize(body);
		using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, url);
		req.Content = new StringContent(content, Encoding.UTF8, "application/json");
		if (!string.IsNullOrEmpty(apiKey))
		{
			req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
		}
		HttpResponseMessage httpResponseMessage = await _http.SendAsync(req);
		if (!httpResponseMessage.IsSuccessStatusCode)
		{
			Logger.Warn($"MT endpoint returned {(int)httpResponseMessage.StatusCode}");
			httpResponseMessage.Dispose();
			return null;
		}
		return httpResponseMessage;
	}

	private static string Clean(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return null;
		}
		s = s.Trim();
		if (s.Length >= 2 && (s[0] == '"' || s[0] == '「' || s[0] == '“'))
		{
			char c = s[s.Length - 1];
			if ((s[0] == '"' && c == '"') || (s[0] == '「' && c == '」') || (s[0] == '“' && c == '”'))
			{
				s = s.Substring(1, s.Length - 2).Trim();
			}
		}
		if (s.Length != 0)
		{
			return s;
		}
		return null;
	}

	private static (string template, string[] numbers) Normalize(string text)
	{
		List<string> nums = new List<string>();
		int i = 0;
		return (template: TagOrNumber.Replace(text, (Match m) =>
		{
			if (m.Value.Length > 0 && m.Value[0] == '<')
			{
				return m.Value;
			}
			nums.Add(m.Value);
			return "{" + i++ + "}";
		}), numbers: nums.ToArray());
	}

	private static string Fill(string template, string[] numbers)
	{
		if (numbers.Length == 0)
		{
			return template;
		}
		bool ok = true;
		string result = Placeholder.Replace(template, (Match m) =>
		{
			int num = int.Parse(m.Groups[1].Value);
			if (num < 0 || num >= numbers.Length)
			{
				ok = false;
				return m.Value;
			}
			return numbers[num];
		});
		if (!ok)
		{
			return null;
		}
		return result;
	}

	private static void WriteJson(string path, object data)
	{
		try
		{
			string directoryName = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOpts), Utf8NoBom);
		}
		catch (Exception ex)
		{
			Logger.Warn("Save MT file failed (" + Path.GetFileName(path) + "): " + ex.Message);
		}
	}

	private static bool HasKana(string s)
	{
		foreach (char c in s)
		{
			if ((c >= '\u3040' && c <= 'ゟ') || (c >= '゠' && c <= 'ヿ'))
			{
				return true;
			}
		}
		return false;
	}
}
