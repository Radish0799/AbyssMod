using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AbyssMod.Services;

public static class AbilityTextMatcher
{
	private static Dictionary<string, string> _exact = new Dictionary<string, string>();

	private static Dictionary<string, string> _bySkeleton = new Dictionary<string, string>();

	private static readonly Regex KeySlot = new Regex("\\{\\[[^\\]]+\\]\\}", RegexOptions.Compiled);

	private static readonly Regex ValueSlot = new Regex("\\{[0-9]+(?:\\.[0-9]+)?%\\}|[0-9]+(?:\\.[0-9]+)?%", RegexOptions.Compiled);

	private static readonly Regex TranslationSlot = new Regex("\\{\\[[^\\]]+\\]\\}", RegexOptions.Compiled);

	public static void Rebuild(Dictionary<string, string> abilityDescriptions)
	{
		_exact = abilityDescriptions ?? new Dictionary<string, string>();
		_bySkeleton = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (KeyValuePair<string, string> item in _exact)
		{
			string text = ToKeySkeleton(item.Key);
			if (!string.IsNullOrEmpty(text))
			{
				_bySkeleton.TryAdd(text, item.Value);
			}
		}
	}

	public static bool LooksLikeAbility(string text)
	{
		if (string.IsNullOrEmpty(text) || !HasKana(text))
		{
			return false;
		}
		if (!text.Contains("ダメージ", StringComparison.Ordinal) && !text.Contains("HIT", StringComparison.OrdinalIgnoreCase) && !text.Contains("紋章", StringComparison.Ordinal) && !text.Contains("スキル", StringComparison.Ordinal) && !text.Contains("【効果】", StringComparison.Ordinal) && !text.Contains("【発動条件】", StringComparison.Ordinal))
		{
			return text.Contains("フォースチェイン", StringComparison.Ordinal);
		}
		return true;
	}

	public static bool TryTranslate(string text, out string result)
	{
		result = null;
		if (string.IsNullOrEmpty(text) || _exact.Count == 0)
		{
			return false;
		}
		text = NormalizeBrackets(text);
		if (_exact.TryGetValue(text, out result))
		{
			return true;
		}
		string key = ToValueSkeleton(text);
		if (!_bySkeleton.TryGetValue(key, out var value))
		{
			return false;
		}
		List<string> values = ExtractValues(text);
		result = ApplyValues(value, values);
		return !string.IsNullOrEmpty(result);
	}

	private static string NormalizeBrackets(string s)
	{
		return s.Replace('「', '【').Replace('」', '】');
	}

	private static string ToKeySkeleton(string s)
	{
		s = NormalizeBrackets(s);
		int i = 0;
		return KeySlot.Replace(s, (Match _) => "{" + i++ + "}");
	}

	private static string ToValueSkeleton(string s)
	{
		s = NormalizeBrackets(s);
		int i = 0;
		return ValueSlot.Replace(s, (Match _) => "{" + i++ + "}");
	}

	private static List<string> ExtractValues(string s)
	{
		s = NormalizeBrackets(s);
		List<string> list = new List<string>();
		foreach (Match item in ValueSlot.Matches(s))
		{
			string text = item.Value;
			if (text.StartsWith('{') && text.EndsWith('}'))
			{
				string text2 = text;
				text = text2.Substring(1, text2.Length - 1 - 1);
			}
			list.Add(text);
		}
		return list;
	}

	private static string ApplyValues(string translation, List<string> values)
	{
		if (values.Count == 0)
		{
			return translation;
		}
		int i = 0;
		bool ok = true;
		string result = TranslationSlot.Replace(translation, (Match _) =>
		{
			if (i >= values.Count)
			{
				ok = false;
				return _.Value;
			}
			return values[i++];
		});
		if (!ok)
		{
			return null;
		}
		return result;
	}

	private static bool HasKana(string s)
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
