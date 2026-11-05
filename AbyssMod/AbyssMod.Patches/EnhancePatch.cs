using System.Linq;
using Absf;
using HarmonyLib;
using Il2CppSystem.Threading;
using Live2D.Cubism.Core;
using Project.Notice;
using Project.Novel;
using Project.Title;
using UnityEngine;

namespace AbyssMod.Patches;

[HarmonyPatch]
public static class EnhancePatch
{
	private static int _allowStopVoiceCount;

	[HarmonyPostfix]
	[HarmonyPatch(typeof(NovelLive2DObject), "Initialize")]
	public static void DisableMosaic(NovelLive2DObject __instance)
	{
		if (!Config.DynamicMosaic.Value)
		{
			__instance.GetDrawables()?.Where((CubismDrawable d) => ((Object)d).name.StartsWith("Mosaic_") || ((Object)d).name.StartsWith("MosaicInsted_")).ToList().ForEach((CubismDrawable d) =>
			{
				((Component)d).gameObject.SetActive(false);
			});
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SoundCautionPopupController), "SetupPopupEvent")]
	public static bool DisableSoundCaution(SoundCautionPopupController __instance)
	{
		if (!Config.SoundCaution.Value)
		{
			__instance._onClickOk.Invoke();
			return false;
		}
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(NovelSoundManager), "StopCategory")]
	public static bool CancelStoppingVoice(int nCategory, bool playFade)
	{
		if (Config.VoiceInterruption.Value || _allowStopVoiceCount > 0)
		{
			return true;
		}
		return (nCategory != 2) | playFade;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(NovelSoundManager), "PlaySound")]
	public static void StopVoiceBeforePlaying(NovelSoundManager __instance, SoundCategory category)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Invalid comparison between Unknown and I4
		if (!Config.VoiceInterruption.Value && (int)category == 2)
		{
			_allowStopVoiceCount++;
			try
			{
				__instance.StopCategory(2, false);
			}
			finally
			{
				_allowStopVoiceCount--;
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(TopView), "PlayMovie")]
	public static void DisableTitleMovie(TopView __instance, CancellationToken ct)
	{
		if (!Config.TitleMovie.Value)
		{
			__instance.MovieSkip(ct);
		}
	}
}
