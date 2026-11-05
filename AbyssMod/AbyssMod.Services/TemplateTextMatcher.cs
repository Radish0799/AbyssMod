using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AbyssMod.Services;

public static class TemplateTextMatcher
{
	private static Dictionary<string, string> _exact = new Dictionary<string, string>();

	private static readonly Regex TagOrNumber = new Regex("<[^>]*>|[0-9]+(?:\\.[0-9]+)?", RegexOptions.Compiled);

	private static readonly Regex Placeholder = new Regex("\\{(\\d+)\\}", RegexOptions.Compiled);

	public static void Rebuild(params Dictionary<string, string>[] sources)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		if (sources != null)
		{
			foreach (Dictionary<string, string> dictionary2 in sources)
			{
				if (dictionary2 == null)
				{
					continue;
				}
				foreach (KeyValuePair<string, string> item in dictionary2)
				{
					dictionary[item.Key] = item.Value;
				}
			}
		}
		_exact = dictionary;
	}

	public static bool TryTranslate(string text, out string result)
	{
		result = null;
		if (string.IsNullOrEmpty(text) || _exact.Count == 0)
		{
			return false;
		}
		if (_exact.TryGetValue(text, out result))
		{
			return true;
		}
		var (text2, numbers) = Normalize(text);
		if (text2 == text || !_exact.TryGetValue(text2, out var value))
		{
			return false;
		}
		result = Fill(value, numbers);
		return !string.IsNullOrEmpty(result);
	}

	private static (string template, string[] numbers) Normalize(string text)
	{
		List<string> nums = new List<string>();
		int i = 0;
		return (template: TagOrNumber.Replace(text, (Match m) =>
		{
			if (m.Value.Length > 0 && m.Value[0] == '<')
			{
				return m.Value;
			}
			nums.Add(m.Value);
			return "{" + i++ + "}";
		}), numbers: nums.ToArray());
	}

	private static string Fill(string template, string[] numbers)
	{
		if (numbers.Length == 0)
		{
			return template;
		}
		bool ok = true;
		string result = Placeholder.Replace(template, (Match m) =>
		{
			int num = int.Parse(m.Groups[1].Value);
			if (num < 0 || num >= numbers.Length)
			{
				ok = false;
				return m.Value;
			}
			return numbers[num];
		});
		if (!ok)
		{
			return null;
		}
		return result;
	}
}
