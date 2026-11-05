using System;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace AbyssTypewriterGuard;

[BepInPlugin("dotabyss.translation.typewriter.guard", "Abyss Typewriter Guard", "1.0.0")]
[BepInDependency("AbyssMod", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
	private const string PluginGuid = "dotabyss.translation.typewriter.guard";

	private const string PluginName = "Abyss Typewriter Guard";

	private const string PluginVersion = "1.0.0";

	private Harmony? _harmony;

	public override void Load()
	{
		_harmony = new Harmony("dotabyss.translation.typewriter.guard");
		Type type = AccessTools.TypeByName("AbyssMod.Patches.GeneralTextPatch");
		if (type == null)
		{
			Log.LogError("AbyssMod.Patches.GeneralTextPatch was not found.");
			return;
		}
		MethodInfo methodInfo = AccessTools.Method(type, "ApplyTranslation");
		if (methodInfo == null)
		{
			Log.LogError("GeneralTextPatch.ApplyTranslation was not found.");
			return;
		}
		MethodInfo method = AccessTools.Method(typeof(Plugin), "SkipSingleCjkPrefix");
		_harmony.Patch(methodInfo, new HarmonyMethod(method));
		Log.LogInfo("Patched GeneralTextPatch.ApplyTranslation. Single CJK typewriter fragments will skip static fallback.");
	}

	public override bool Unload()
	{
		_harmony?.UnpatchSelf();
		return base.Unload();
	}

	private static bool SkipSingleCjkPrefix(ref string s)
	{
		return !ShouldSkipTranslation(s);
	}

	private static bool ShouldSkipTranslation(string? text)
	{
		if (string.IsNullOrEmpty(text) || text.Length != 1)
		{
			return false;
		}
		return IsCjkUnifiedIdeograph(text[0]);
	}

	private static bool IsCjkUnifiedIdeograph(char c)
	{
		return (c >= '㐀' && c <= '䶿') || (c >= '一' && c <= '鿿') || (c >= '豈' && c <= '\ufaff');
	}
}
