using HarmonyLib;
using Project.ItemList.Top;
using Project.Outgame.UI.Popup;

namespace AbyssMod.Patches;

[HarmonyPatch]
public static class ItemPatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(LeftView), "ViewUpdate")]
	public static void TranslateItemListDetail(LeftViewModel model)
	{
		if (model != null)
		{
			string description = model.Description;
			string text = TextTranslator.Process("items", description);
			if ((object)description != text)
			{
				model.Description = text;
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(ItemDetailFlavorTextView), "UpdateView")]
	public static void TranslateFlavorText(ref string flavorText)
	{
		flavorText = TextTranslator.Process("items", flavorText);
	}
}
