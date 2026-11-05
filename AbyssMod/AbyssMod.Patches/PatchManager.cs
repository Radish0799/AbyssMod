using HarmonyLib;

namespace AbyssMod.Patches;

public static class PatchManager
{
	public static string NovelId = string.Empty;

	public static void Initialize()
	{
		Harmony.CreateAndPatchAll(typeof(EnhancePatch));
		Harmony.CreateAndPatchAll(typeof(TranslationPatch));
		Harmony.CreateAndPatchAll(typeof(ItemPatch));
		Harmony.CreateAndPatchAll(typeof(GeneralTextPatch));
		Harmony.CreateAndPatchAll(typeof(MasterDataTranslationPatch));
	}
}
