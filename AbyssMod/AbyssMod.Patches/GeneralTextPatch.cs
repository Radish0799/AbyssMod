using System;
using AbyssMod.Services;
using HarmonyLib;
using Project;
using TMPro;
using UnityEngine;

namespace AbyssMod.Patches;

[HarmonyPatch]
public static class GeneralTextPatch
{
	[ThreadStatic]
	private static bool _inTranslation;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(TMP_Text), "set_text")]
	public static void OnSetText(ref string value, TMP_Text __instance)
	{
		ApplyTranslation(ref value, __instance);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(TextMeshProUGUI), "OnEnable")]
	public static void OnUiTextEnable(TextMeshProUGUI __instance)
	{
		TranslateStaticUiText((TMP_Text)(object)__instance);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(TextMeshPro), "OnEnable")]
	public static void OnWorldTextEnable(TextMeshPro __instance)
	{
		TranslateStaticUiText((TMP_Text)(object)__instance);
	}

	private static void TranslateStaticUiText(TMP_Text text)
	{
		if ((Object)(object)text == (Object)null || !Config.Translation.Value)
		{
			return;
		}
		try
		{
			string text2 = UiTextTranslator.Translate(text, text.text);
			if (!string.Equals(text2, text.text, StringComparison.Ordinal))
			{
				_inTranslation = true;
				try
				{
					TmpTextHelper.TrySetTextDirect(text, text2);
					return;
				}
				finally
				{
					_inTranslation = false;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Warn("TranslateStaticUiText failed: " + ex.Message);
		}
	}

	public static void RefreshVisibleText()
	{
		if (!Config.Translation.Value || _inTranslation)
		{
			return;
		}
		try
		{
			foreach (TMP_Text item in Object.FindObjectsOfType<TMP_Text>(true))
			{
				if ((Object)(object)item == (Object)null)
				{
					continue;
				}
				string s = item.text;
				if (string.IsNullOrEmpty(s) || !TextTranslator.HasKana(s))
				{
					continue;
				}
				string text = s;
				ApplyTranslation(ref s, item);
				if (!(s == text))
				{
					_inTranslation = true;
					try
					{
						TmpTextHelper.TrySetTextDirect(item, s);
					}
					finally
					{
						_inTranslation = false;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Warn("RefreshVisibleText failed: " + ex.Message);
		}
	}

	public static void RefreshAllVisibleText()
	{
		RefreshVisibleText();
	}

	private static void ApplyTranslation(ref string s, TMP_Text instance = null)
	{
		if (_inTranslation)
		{
			return;
		}
		_inTranslation = true;
		try
		{
			if ((Object)(object)instance != (Object)null)
			{
				string text = UiTextTranslator.Translate(instance, s);
				if (!string.Equals(text, s, StringComparison.Ordinal))
				{
					s = text;
					return;
				}
			}
			string text2;
			if (IsNameField(instance))
			{
				text2 = "name";
			}
			else
			{
				text2 = ((!IsAbilityDescriptionField(instance) && !TextClassifier.IsActionSkillDescription(s)) ? (UiContextHelper.ResolveCategory(instance, s) ?? (Config.ClassifyText.Value ? TextClassifier.Classify(s) : "ui_misc")) : "ability_descriptions");
			}
			s = TextTranslator.Process(text2, s);
			if (text2 != "ability_descriptions")
			{
				s = MachineTranslator.Handle(text2, s);
			}
		}
		finally
		{
			_inTranslation = false;
		}
	}

	private static bool IsNameField(TMP_Text tmp)
	{
		if ((Object)(object)tmp == (Object)null)
		{
			return false;
		}
		try
		{
			GameObject gameObject = ((Component)tmp).gameObject;
			if ((Object)(object)gameObject == (Object)null)
			{
				return false;
			}
			string text = ((Object)gameObject).name ?? string.Empty;
			if (text.IndexOf("CharaName", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (string.Equals(text, "TextName", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			Transform transform = gameObject.transform;
			Transform val = ((transform != null) ? transform.parent : null);
			if ((Object)(object)val != (Object)null && string.Equals(((Object)val).name, "Name", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	private static bool IsAbilityDescriptionField(TMP_Text tmp)
	{
		if ((Object)(object)tmp == (Object)null)
		{
			return false;
		}
		try
		{
			GameObject gameObject = ((Component)tmp).gameObject;
			if ((Object)(object)gameObject == (Object)null)
			{
				return false;
			}
			string haystack = ((Object)gameObject).name ?? string.Empty;
			if (ContainsAnyIgnoreCase(haystack, "SkillDesc", "SkillDescription", "ActionSkill", "ChainSkill", "LimitBreak", "BreakEffect", "TextEffect"))
			{
				return true;
			}
			if (ContainsAnyIgnoreCase(haystack, "Description") && !ContainsAnyIgnoreCase(haystack, "Item", "Flavor"))
			{
				return true;
			}
			Transform transform = gameObject.transform;
			Transform val = ((transform != null) ? transform.parent : null);
			while ((Object)(object)val != (Object)null)
			{
				if (ContainsAnyIgnoreCase(((Object)val).name ?? string.Empty, "LimitBreak", "BreakLimit", "ActionSkill", "ChainSkill", "SkillDetail", "SkillInfo", "CharaDetail", "CharacterDetail", "BreakThrough", "AbilityDetail"))
				{
					return true;
				}
				val = val.parent;
			}
		}
		catch
		{
		}
		return false;
	}

	private static bool ContainsAnyIgnoreCase(string haystack, params string[] needles)
	{
		foreach (string value in needles)
		{
			if (haystack.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SkillTextFormatter), "CreateActionSkill")]
	public static void OnCreateActionSkill(ref string description)
	{
		description = TextTranslator.Process("ability_descriptions", description);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SkillTextFormatter), "CreateChainSkill")]
	public static void OnCreateChainSkill(ref string description)
	{
		description = TextTranslator.Process("ability_descriptions", description);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SkillTextFormatter), "CreateAbility")]
	public static void OnCreateAbility(ref string skillText, ref string awakeText)
	{
		skillText = TextTranslator.Process("ability_descriptions", skillText);
		awakeText = TextTranslator.Process("ability_descriptions", awakeText);
	}
}
