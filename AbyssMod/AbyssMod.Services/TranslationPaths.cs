using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AbyssMod.Services;

public static class TranslationPaths
{
	public const string Manifest = "manifest";

	public const string Names = "names";

	public const string Titles = "titles";

	public const string Descriptions = "descriptions";

	public const string AnotherName = "another_name";

	public const string AbilityDescriptions = "ability_descriptions";

	public const string Novels = "novels";

	public const string UiTexts = "ui_texts";

	public const string Items = "items";

	public const string Ui = "ui";

	public const string Other = "other";

	public const string AddOn = "add-on";

	public const string Legacy = "legacy";

	public const string LegacyUiMisc = "ui_misc";

	public static readonly HashSet<string> ReservedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"manifest", "names", "titles", "descriptions", "another_name", "ability_descriptions", "novels", "other", "add-on", "legacy",
		"ui_texts"
	};

	public static IReadOnlyList<string> ContentTypes { get; private set; } = Array.Empty<string>();

	public static void SetContentTypes(List<string> types)
	{
		ContentTypes = types;
	}

	public static bool IsCdnFlatType(string type)
	{
		if (!ContentTypes.Contains(type))
		{
			return ReservedTypes.Contains(type);
		}
		return true;
	}

	public static string BuildRemoteUrl(string cdn, string type, string language, string id = null)
	{
		if (type == "novels")
		{
			if (id == null)
			{
				throw new ArgumentException("Novel ID is required for novels type");
			}
			return $"{cdn}/{"novels"}/{id}/{language}.json";
		}
		return $"{cdn}/{type}/{language}.json";
	}

	public static string BuildAddOnRemoteUrl(string cdn, string category, string language)
	{
		return $"{cdn.TrimEnd('/')}/{"add-on"}/{category}/{language}.json";
	}

	public static string BuildAddOnCachePath(string cacheDir, string category, string language)
	{
		return Path.Combine(cacheDir, "add-on", category, language + ".json");
	}

	public static string BuildLegacyAddOnRemoteUrl(string cdn, string category, string language)
	{
		return $"{cdn.TrimEnd('/')}/{"legacy"}/{"add-on"}/{category}/{language}.json";
	}

	public static string BuildLegacyAddOnCachePath(string cacheDir, string category, string language)
	{
		return Path.Combine(cacheDir, "legacy", "add-on", category, language + ".json");
	}

	public static string BuildOtherRemoteUrl(string cdn, string category, string language)
	{
		return $"{cdn.TrimEnd('/')}/{"other"}/{category}/{language}.json";
	}

	public static string BuildOtherCachePath(string cacheDir, string category, string language)
	{
		return Path.Combine(cacheDir, "other", category, language + ".json");
	}

	public static string BuildCachePath(string cacheDir, string type, string language, string id = null)
	{
		if (type == "novels" && id != null)
		{
			return Path.Combine(cacheDir, "novels", id, language + ".json");
		}
		if (type == "novels")
		{
			throw new ArgumentException("Novel ID is required for novels type");
		}
		if (!IsCdnFlatType(type))
		{
			return Path.Combine(cacheDir, "add-on", type, language + ".json");
		}
		return Path.Combine(cacheDir, type, language + ".json");
	}

	public static IEnumerable<string> EnumerateLocalCategories(string cacheDir, string language)
	{
		string path = Path.Combine(cacheDir, "add-on");
		if (!Directory.Exists(path))
		{
			yield break;
		}
		string[] directories = Directory.GetDirectories(path);
		foreach (string text in directories)
		{
			if (File.Exists(Path.Combine(text, language + ".json")))
			{
				yield return Path.GetFileName(text);
			}
		}
	}

	public static IEnumerable<string> EnumerateOtherCategories(string cacheDir, string language)
	{
		string path = Path.Combine(cacheDir, "other");
		if (!Directory.Exists(path))
		{
			yield break;
		}
		string[] directories = Directory.GetDirectories(path);
		foreach (string text in directories)
		{
			if (File.Exists(Path.Combine(text, language + ".json")))
			{
				yield return Path.GetFileName(text);
			}
		}
	}

	public static IEnumerable<string> EnumerateMasterDictTypes()
	{
		return ContentTypes.Where((string t) => !string.Equals(t, "novels", StringComparison.OrdinalIgnoreCase)).Where(IsNotMasterDataType).Where(TranslationPaths.IsNotMasterDataType);
	}

	private static bool IsNotMasterDataType(string t)
	{
		return !t.StartsWith("m_", StringComparison.Ordinal);
	}

	private static bool IsNotMasterDataType(string t)
	{
		return !t.StartsWith("m_", StringComparison.Ordinal);
	}
}
