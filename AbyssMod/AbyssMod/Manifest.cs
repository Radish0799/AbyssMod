using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbyssMod;

public class Manifest
{
	[JsonPropertyName("hash")]
	public string Hash { get; set; }

	[JsonPropertyName("names")]
	public string Names { get; set; }

	[JsonPropertyName("titles")]
	public string Titles { get; set; }

	[JsonPropertyName("descriptions")]
	public string Descriptions { get; set; }

	[JsonPropertyName("another_name")]
	public string AnotherName { get; set; }

	[JsonPropertyName("ability_descriptions")]
	public string AbilityDescriptions { get; set; }

	[JsonPropertyName("ui_texts")]
	public string UiTexts { get; set; }

	[JsonPropertyName("novels")]
	public Dictionary<string, string> Novels { get; set; }

	[JsonPropertyName("other")]
	public Dictionary<string, string> Other { get; set; }

	[JsonPropertyName("add_on")]
	public Dictionary<string, string> AddOn { get; set; }

	[JsonExtensionData]
	public Dictionary<string, JsonElement> Extra { get; set; }

	public string GetFileHash(string type)
	{
		string text = type switch
		{
			"names" => Names, 
			"titles" => Titles, 
			"descriptions" => Descriptions, 
			"another_name" => AnotherName, 
			"ability_descriptions" => AbilityDescriptions, 
			"ui_texts" => UiTexts, 
			_ => null, 
		};
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		if (Extra == null || !Extra.TryGetValue(type, out var value))
		{
			return null;
		}
		if (value.ValueKind != JsonValueKind.String)
		{
			return null;
		}
		return value.GetString();
	}
}
