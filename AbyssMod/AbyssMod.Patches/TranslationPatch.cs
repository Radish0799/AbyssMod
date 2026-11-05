using System.Collections.Generic;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Threading;
using Project.Library;
using Project.MainStory;
using Project.Novel;
using Project.Outgame;

namespace AbyssMod.Patches;

[HarmonyPatch]
public static class TranslationPatch
{
	private static NovelController _novelController;

	private static string NovelId
	{
		get
		{
			NovelController novelController = _novelController;
			object obj;
			if (novelController == null)
			{
				obj = null;
			}
			else
			{
				NovelModelCommon common = novelController._common;
				obj = ((common != null) ? common.ScriptId : null);
			}
			if (obj == null)
			{
				obj = string.Empty;
			}
			return (string)obj;
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(NovelController), "InitNovel")]
	public static void InitNovelController(NovelController __instance)
	{
		_novelController = __instance;
	}

	public static bool TryGetCurrentNovel(out Dictionary<string, string> translation)
	{
		translation = null;
		if (Config.Translation.Value)
		{
			return Plugin.Trans.Novels.TryGetValue(NovelId, out translation);
		}
		return false;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(NovelPathUtility), "GetNovelScenarioDirectory")]
	public static void SetupTranslation(string novelId)
	{
		if (Config.Translation.Value)
		{
			ManualLogSource log = Plugin.Log;
			BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(9, 1, out var isEnabled);
			if (isEnabled)
			{
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral("NovelId: ");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted(novelId);
			}
			log.LogInfo(bepInExInfoLogInterpolatedStringHandler);
			Plugin.Trans.GetNovelTranslationAsync(novelId).Wait();
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(NovelScriptInfoUtility), "GetScriptInfo")]
	public static void SetTitleAndDescription(ValueTuple<string, string> __result)
	{
		if (TryGetCurrentNovel(out var _))
		{
			string item = __result.Item1;
			if (!string.IsNullOrEmpty(item) && Plugin.Trans.Titles.TryGetValue(item, out var value))
			{
				__result.Item1 = value;
			}
			string item2 = __result.Item2;
			if (!string.IsNullOrEmpty(item2) && Plugin.Trans.Descriptions.TryGetValue(item2, out var value2))
			{
				__result.Item2 = value2;
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(NovelTitle), "SetTitle")]
	public static void SetTitle(ref string title)
	{
		if (TryGetCurrentNovel(out var _) && !string.IsNullOrEmpty(title) && Plugin.Trans.Titles.TryGetValue(title, out var value))
		{
			title = value;
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(NovelViewMessageWindow), "SetName")]
	public static void SetName(ref string name)
	{
		if (TryGetCurrentNovel(out var _) && !string.IsNullOrEmpty(name) && Plugin.Trans.Names.TryGetValue(name, out var value))
		{
			name = value;
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(NovelText), "Parse")]
	public static void SetText(List<Letter> letters, ref string message)
	{
		if (TryGetCurrentNovel(out var translation) && !string.IsNullOrEmpty(message) && translation.TryGetValue(message, out var value))
		{
			message = value;
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(NovelModelMessageLog), "Add")]
	public static void SetLogAdd(string scriptId, string assetId, ref string charaName, ref string message, string logId, NovelSound voice, CancellationToken ct)
	{
		charaName = charaName?.Replace("<user>", "%user%");
		message = message?.Replace("<user>", "%user%");
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(NovelLogPopup), "SetData")]
	public static void SetLog(ref List<NovelLogData> dataList)
	{
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected Obj, but got Unknown
		List<NovelLogData> val = new List<NovelLogData>();
		Enumerator<NovelLogData> enumerator = dataList.GetEnumerator();
		while (enumerator.MoveNext())
		{
			NovelLogData current = enumerator.Current;
			string text = current.Name?.Replace("%user%", "<user>");
			string text2 = current.Message?.Replace("%user%", "<user>");
			if (TryGetCurrentNovel(out var translation))
			{
				if (!string.IsNullOrEmpty(text) && Plugin.Trans.Names.TryGetValue(text, out var value))
				{
					text = value;
				}
				if (!string.IsNullOrEmpty(text2) && translation.TryGetValue(text2, out var value2))
				{
					text2 = value2;
				}
			}
			val.Add(new NovelLogData(current.ScriptId, current.AssetId, text, text2, current.LogId, current.Voice, current.Ct));
		}
		dataList = val;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(NovelModelDotBalloon), "StartBalloonMessage")]
	public static void SetBalloon(CommandDotMessageData messageData)
	{
		if (TryGetCurrentNovel(out var translation))
		{
			string message = messageData.Message;
			if (!string.IsNullOrEmpty(message) && translation.TryGetValue(message, out var value))
			{
				messageData.Message = value;
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(NovelMessageTextComponent), "SetMessageText")]
	public static void SetTextCenter(NovelModelCommon common, CommandMessageTextData data)
	{
		if (TryGetCurrentNovel(out var translation))
		{
			string message = data.Message;
			if (!string.IsNullOrEmpty(message) && translation.TryGetValue(message, out var value))
			{
				data.Message = value;
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LibraryNovelPlayPopupController), "InitializePopup")]
	public static void SetLibraryPopup(LibraryNovelPlayPopupController __instance, TextPopup popup)
	{
		if (Config.Translation.Value)
		{
			string title = __instance._model.Title;
			if (!string.IsNullOrEmpty(title) && Plugin.Trans.Titles.TryGetValue(title, out var value))
			{
				popup._titleText.text = value;
			}
			string description = __instance._model.Description;
			if (!string.IsNullOrEmpty(description) && Plugin.Trans.Descriptions.TryGetValue(description, out var value2))
			{
				popup._contentText.text = value2;
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(StoryQuestDetailPopup), "Setup")]
	public static void SetStoryQuestDetail(StoryQuestDetailPopup __instance)
	{
		if (Config.Translation.Value)
		{
			string text = __instance._storyDescription.text;
			if (!string.IsNullOrEmpty(text) && Plugin.Trans.Descriptions.TryGetValue(text, out var value))
			{
				__instance._storyDescription.text = value;
			}
		}
	}
}
