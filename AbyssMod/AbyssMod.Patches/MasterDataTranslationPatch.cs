using IntPtr = System.IntPtr;
using Exception = System.Exception;
using System;
using System.Collections.Generic;
using AbyssMod.Services;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem;
using Il2CppSystem.Reflection;
using Project.Master;

namespace AbyssMod.Patches;

[HarmonyPatch]
public static class MasterDataTranslationPatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(MasterDataStore), "_DownloadFirstAsync_b__8_0")]
	public static void TranslateBeforeCache(Il2CppSystem.Type elementType, Il2CppSystem.Object rowsArray)
	{
		if (!Config.Translation.Value || Plugin.Trans == null || rowsArray == null)
		{
			return;
		}
		try
		{
			Plugin.Trans.EnsureStaticTranslationsLoaded();
			string text = ((elementType != null) ? ((MemberInfo)elementType).Name : null);
			if (text == null || !MasterMapping.Tables.TryGetValue(text, out var value))
			{
				return;
			}
			List<IntPtr> arrayElements = MasterMapping.GetArrayElements(((Il2CppObjectBase)(object)rowsArray).Pointer);
			if (arrayElements == null)
			{
				return;
			}
			int num = 0;
			foreach (IntPtr item in arrayElements)
			{
				if (item != IntPtr.Zero)
				{
					num += TranslateRow(item, value);
				}
			}
			if (num > 0)
			{
				Logger.Info($"MasterData translated: {text}, fields: {num}");
			}
		}
		catch (Exception value2)
		{
			Logger.Error($"[MasterDataTranslation] threw: {value2}");
		}
	}

	private static int TranslateRow(System.IntPtr rowPtr, TableMapping table)
	{
		int num = 0;
		foreach (FieldEntry field in table.Fields)
		{
			string text = MasterMapping.ReadField(rowPtr, field);
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			foreach (FieldRule rule in field.Rules)
			{
				Dictionary<string, string> table2 = Plugin.Trans.GetTable(rule.Dict);
				if (table2 != null && table2.TryGetValue(text, out var value) && !string.IsNullOrEmpty(value))
				{
					MasterMapping.WriteField(rowPtr, field, rule.Seal ? RestoreSealNames(value) : value);
					num++;
					break;
				}
			}
		}
		return num;
	}

	private static string RestoreSealNames(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		text = text.Replace("纹章：冲击", "紋章：衝撃");
		text = text.Replace("纹章：热情", "紋章：情熱");
		return text;
	}
}
