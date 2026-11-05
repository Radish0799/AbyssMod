using System;
using System.Reflection;
using AbyssMod.Services;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace AbyssStaticFix;

[BepInPlugin("AbyssStaticFix", "AbyssStaticFix", "1.0.1")]
[BepInDependency("AbyssMod", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BasePlugin
{
	internal static FieldInfo? TablesField;

	internal static FieldInfo? CacheField;

	internal static FieldInfo? CdnField;

	internal static FieldInfo? LangField;

	public override void Load()
	{
		try
		{
			Type type = Array.Find(typeof(TranslationManager).Assembly.GetTypes(), (Type t) => t.Name == "TranslationCache");
			Type typeFromHandle = typeof(TranslationManager);
			TablesField = typeFromHandle.GetField("_tables", BindingFlags.Instance | BindingFlags.NonPublic);
			CacheField = typeFromHandle.GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);
			if (type != null)
			{
				CdnField = type.GetField("_cdn", BindingFlags.Instance | BindingFlags.NonPublic);
				LangField = type.GetField("_language", BindingFlags.Instance | BindingFlags.NonPublic);
			}
			Harmony harmony = new Harmony("AbyssStaticFix");
			MethodInfo method = typeFromHandle.GetMethod("EnsureStaticTranslationsLoaded", BindingFlags.Instance | BindingFlags.Public);
			MethodInfo method2 = typeof(Patches).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public);
			harmony.Patch(method, null, new HarmonyMethod(method2));
			Log.LogInfo("[AbyssStaticFix] v1.0.1 loaded. Patched OK.");
		}
		catch (Exception ex)
		{
			Log.LogError("[AbyssStaticFix] Load error: " + ex);
		}
	}
}
