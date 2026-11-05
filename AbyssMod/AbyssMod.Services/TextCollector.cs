using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using BepInEx;

namespace AbyssMod.Services;

public static class TextCollector
{
	private static readonly object Lock = new object();

	private static readonly Dictionary<string, HashSet<string>> Collected = new Dictionary<string, HashSet<string>>();

	private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		WriteIndented = true
	};

	private static string DumpPath(string category)
	{
		return Path.Combine(Paths.PluginPath, "AbyssMod", "dump", category + "_raw.json");
	}

	public static void Record(string category, string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		lock (Lock)
		{
			if (!Collected.TryGetValue(category, out var value))
			{
				value = new HashSet<string>();
				Collected[category] = value;
			}
			if (!value.Add(text))
			{
				return;
			}
			try
			{
				string path = DumpPath(category);
				string directoryName = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				Dictionary<string, string> dictionary = new Dictionary<string, string>();
				foreach (string item in value)
				{
					dictionary[item] = string.Empty;
				}
				File.WriteAllText(path, JsonSerializer.Serialize(dictionary, JsonOptions), Utf8);
			}
			catch
			{
			}
		}
	}
}
