using System.Collections.Generic;
using AbyssMod.Services;

namespace AbyssMod.Patches;

public static class TextTranslator
{
	public static string Process(string category, string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		TranslationManager trans = Plugin.Trans;
		Dictionary<string, string> dictionary = trans?.Names;
		Dictionary<string, string> dictionary2 = trans?.Texts;
		if (Config.Translation.Value && trans != null)
		{
			if (dictionary != null && dictionary.TryGetValue(text, out var value))
			{
				return value;
			}
			Dictionary<string, string> table = trans.GetTable("ui_texts");
			if (table != null && table.TryGetValue(text, out var value2))
			{
				return value2;
			}
			if ((category == "ability_descriptions" || LooksLikeAbility(text)) && AbilityTextMatcher.TryTranslate(text, out var result))
			{
				return result;
			}
			if (dictionary2 != null && dictionary2.TryGetValue(text, out var value3))
			{
				return value3;
			}
			Dictionary<string, string> titles = trans.Titles;
			if (titles != null && titles.TryGetValue(text, out var value4))
			{
				return value4;
			}
			Dictionary<string, string> descriptions = trans.Descriptions;
			if (descriptions != null && descriptions.TryGetValue(text, out var value5))
			{
				return value5;
			}
			if (TemplateTextMatcher.TryTranslate(text, out var result2))
			{
				return result2;
			}
		}
		if (Config.CollectText.Value && HasKana(text) && (dictionary2 == null || !dictionary2.ContainsKey(text)) && (dictionary == null || !dictionary.ContainsKey(text)))
		{
			TextCollector.Record(category, text);
		}
		return text;
	}

	private static bool LooksLikeAbility(string text)
	{
		return AbilityTextMatcher.LooksLikeAbility(text);
	}

	public static bool HasKana(string s)
	{
		foreach (char c in s)
		{
			if ((c >= '\u3040' && c <= 'ゟ') || (c >= '゠' && c <= 'ヿ'))
			{
				return true;
			}
		}
		return false;
	}
}
