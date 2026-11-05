using Object = UnityEngine.Object;
using System;
using TMPro;
using UnityEngine;

namespace AbyssMod.Services;

public static class UiContextHelper
{
	public static string ResolveCategory(TMP_Text tmp, string text)
	{
		if ((Object)(object)tmp == (Object)null)
		{
			return null;
		}
		try
		{
			if (IsUnder(tmp, "Mission", "Weekly", "Daily", "Achievement", "MissionPass", "BeginnerMission"))
			{
				return "mission";
			}
			if (IsUnder(tmp, "AbyssCode", "ImpactCode", "RushCode", "SafeCode", "RiskCode", "CodeDetail", "CodeBook"))
			{
				return "abyss_code";
			}
			if (IsUnder(tmp, "Dictionary", "Encyclopedia", "Library", "NovelList", "StoryReplay", "MainStory", "ScenarioList"))
			{
				return "titles";
			}
			if (IsUnder(tmp, "Option", "Settings", "Config", "MenuPopup", "GameMenu"))
			{
				return "system";
			}
		}
		catch
		{
		}
		return null;
	}

	private static bool IsUnder(TMP_Text tmp, params string[] markers)
	{
		GameObject gameObject = ((Component)tmp).gameObject;
		if ((Object)(object)gameObject == (Object)null)
		{
			return false;
		}
		Transform val = gameObject.transform;
		while ((Object)(object)val != (Object)null)
		{
			string text = ((Object)val).name ?? string.Empty;
			foreach (string value in markers)
			{
				if (text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
			val = val.parent;
		}
		return false;
	}
}
