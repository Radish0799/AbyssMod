using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using AbyssMod.Services;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using HarmonyLib;

namespace AbyssStaticFix;

public static class Patches
{
	private static readonly HttpClient Http = new HttpClient();

	private static Dictionary<string, Dictionary<string, Dictionary<string, string>>>? _bundle;

	private static bool _injected = false;

	[HarmonyPatch(typeof(TranslationManager), "EnsureStaticTranslationsLoaded")]
	[HarmonyPostfix]
	public static void Postfix(TranslationManager __instance)
	{
		if (_injected)
		{
			return;
		}
		try
		{
			if (!(Plugin.TablesField?.GetValue(__instance) is Dictionary<string, Dictionary<string, string>> dictionary))
			{
				return;
			}
			bool isEnabled;
			BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler;
			if (_bundle == null)
			{
				object obj = Plugin.CacheField?.GetValue(__instance);
				if (obj == null)
				{
					return;
				}
				string text = (string)(Plugin.CdnField?.GetValue(obj) ?? "");
				string text2 = (string)(Plugin.LangField?.GetValue(obj) ?? "zh_Hant");
				if (string.IsNullOrEmpty(text))
				{
					return;
				}
				string text3 = text + "/static/" + text2 + ".json";
				ManualLogSource manualLogSource = Logger.CreateLogSource("AbyssStaticFix");
				bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(46, 1, out isEnabled);
				if (isEnabled)
				{
					bepInExInfoLogInterpolatedStringHandler.AppendLiteral("[AbyssStaticFix] Fetching static bundle once: ");
					bepInExInfoLogInterpolatedStringHandler.AppendFormatted(text3);
				}
				manualLogSource.LogInfo(bepInExInfoLogInterpolatedStringHandler);
				string result = Http.GetStringAsync(text3).GetAwaiter().GetResult();
				if (string.IsNullOrEmpty(result))
				{
					return;
				}
				_bundle = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(result);
			}
			if (_bundle == null)
			{
				return;
			}
			int num = 0;
			foreach (KeyValuePair<string, Dictionary<string, Dictionary<string, string>>> item in _bundle)
			{
				string key = item.Key;
				if (!key.StartsWith("", StringComparison.Ordinal) || (dictionary.TryGetValue(key, out var value) && value.Count > 0))
				{
					continue;
				}
				Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
				foreach (Dictionary<string, string> value2 in item.Value.Values)
				{
					foreach (KeyValuePair<string, string> item2 in value2)
					{
						if (!string.IsNullOrEmpty(item2.Value))
						{
							dictionary2[item2.Key] = item2.Value;
						}
					}
				}
				if (dictionary2.Count > 0)
				{
					dictionary[key] = dictionary2;
					num++;
				}
			}
			_injected = true;
			ManualLogSource manualLogSource2 = Logger.CreateLogSource("AbyssStaticFix");
			bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(44, 1, out isEnabled);
			if (isEnabled)
			{
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral("[AbyssStaticFix] Injected ");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted(num);
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" m_* tables. Done.");
			}
			manualLogSource2.LogInfo(bepInExInfoLogInterpolatedStringHandler);
		}
		catch (Exception ex)
		{
			Logger.CreateLogSource("AbyssStaticFix").LogError("[AbyssStaticFix] Postfix error: " + ex);
		}
	}
}
