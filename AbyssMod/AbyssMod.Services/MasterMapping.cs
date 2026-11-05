using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Il2CppInterop.Runtime;

namespace AbyssMod.Services;

public static class MasterMapping
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		ReadCommentHandling = JsonCommentHandling.Skip
	};

	private const string ResourceName = "AbyssMod.master_mapping.json";

	private const string ResourceNameAlt = "AbyssMod.AbyssMod.master_mapping.json";

	public static Dictionary<string, TableMapping> Tables { get; private set; } = new Dictionary<string, TableMapping>();

	public static void Load()
	{
		try
		{
			using Stream stream = OpenMappingStream();
			if (stream == null)
			{
				Logger.Error("[MasterMapping] embedded resource not found: AbyssMod.master_mapping.json (also tried AbyssMod.AbyssMod.master_mapping.json)");
				Tables = new Dictionary<string, TableMapping>();
				return;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(stream);
			if (jsonDocument.RootElement.TryGetProperty("dict_types", out var value))
			{
				List<string> list = new List<string>();
				foreach (JsonElement item in value.EnumerateArray())
				{
					string text = item.GetString();
					if (!string.IsNullOrEmpty(text))
					{
						list.Add(text);
					}
				}
				TranslationPaths.SetContentTypes(list);
				Logger.Info($"[MasterMapping] registered {list.Count} dict types");
			}
			else
			{
				Logger.Warn("[MasterMapping] missing 'dict_types', no translation tables will load");
				TranslationPaths.SetContentTypes(new List<string>());
			}
			if (!jsonDocument.RootElement.TryGetProperty("tables", out var value2))
			{
				Logger.Error("[MasterMapping] missing 'tables' property");
				Tables = new Dictionary<string, TableMapping>();
				return;
			}
			Dictionary<string, TableMapping> dictionary = new Dictionary<string, TableMapping>();
			foreach (JsonProperty item2 in value2.EnumerateObject())
			{
				TableMapping tableMapping = BuildTable(item2.Name, item2.Value);
				if (tableMapping.Fields.Count > 0)
				{
					dictionary[item2.Name] = tableMapping;
				}
			}
			Tables = dictionary;
			Logger.Info($"[MasterMapping] loaded {dictionary.Count} tables, {CountFields(dictionary)} fields");
			ValidateDictReferences(dictionary);
		}
		catch (Exception value3)
		{
			Logger.Error($"[MasterMapping] failed to load: {value3}");
			Tables = new Dictionary<string, TableMapping>();
		}
	}

	private static Stream OpenMappingStream()
	{
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		string[] array = new string[2] { "AbyssMod.master_mapping.json", "AbyssMod.AbyssMod.master_mapping.json" };
		foreach (string name in array)
		{
			Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(name);
			if (manifestResourceStream != null)
			{
				return manifestResourceStream;
			}
		}
		return null;
	}

	private static TableMapping BuildTable(string tableName, JsonElement tableEl)
	{
		TableMapping tableMapping = new TableMapping();
		IntPtr intPtr = ResolveClassPtr(tableName);
		if (intPtr == IntPtr.Zero)
		{
			Logger.Warn("[MasterMapping] type not resolved: " + tableName + ", table skipped");
			return tableMapping;
		}
		foreach (JsonProperty item in tableEl.EnumerateObject())
		{
			List<FieldRule> list = ParseRules(item.Value);
			if (list.Count != 0)
			{
				IntPtr il2CppField = IL2CPP.GetIl2CppField(intPtr, item.Name);
				if (il2CppField == IntPtr.Zero)
				{
					Logger.Warn($"[MasterMapping] field not found: {tableName}.{item.Name}, skipped");
				}
				else
				{
					tableMapping.Fields.Add(new FieldEntry
					{
						Name = item.Name,
						FieldPtr = il2CppField,
						Offset = (int)IL2CPP.il2cpp_field_get_offset(il2CppField),
						Rules = list
					});
				}
			}
		}
		return tableMapping;
	}

	private static List<FieldRule> ParseRules(JsonElement el)
	{
		List<FieldRule> list = new List<FieldRule>();
		if (el.ValueKind == JsonValueKind.Object)
		{
			list.Add(ParseRule(el));
		}
		else if (el.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item in el.EnumerateArray())
			{
				list.Add(ParseRule(item));
			}
		}
		return list;
	}

	private static FieldRule ParseRule(JsonElement el)
	{
		string dict = "";
		bool seal = false;
		foreach (JsonProperty item in el.EnumerateObject())
		{
			if (item.NameEquals("dict"))
			{
				dict = item.Value.GetString() ?? "";
			}
			else if (item.NameEquals("seal"))
			{
				seal = item.Value.GetBoolean();
			}
		}
		return new FieldRule
		{
			Dict = dict,
			Seal = seal
		};
	}

	private static int CountFields(Dictionary<string, TableMapping> tables)
	{
		int num = 0;
		foreach (TableMapping value in tables.Values)
		{
			num += value.Fields.Count;
		}
		return num;
	}

	private static void ValidateDictReferences(Dictionary<string, TableMapping> tables)
	{
		HashSet<string> hashSet = new HashSet<string>(TranslationPaths.ContentTypes);
		HashSet<string> hashSet2 = new HashSet<string>();
		foreach (TableMapping value in tables.Values)
		{
			foreach (FieldEntry field in value.Fields)
			{
				foreach (FieldRule rule in field.Rules)
				{
					if (!hashSet.Contains(rule.Dict))
					{
						hashSet2.Add(rule.Dict);
					}
				}
			}
		}
		foreach (string item in hashSet2)
		{
			Logger.Warn("[MasterMapping] dict '" + item + "' used in tables but not in dict_types; translations referencing it will be skipped");
		}
	}

	private static IntPtr ResolveClassPtr(string tableName)
	{
		try
		{
			return IL2CPP.GetIl2CppClass("Project.dll", "Project.Master.NoaMessagePack", tableName);
		}
		catch (Exception ex)
		{
			Logger.Error("[MasterMapping] resolve " + tableName + " failed: " + ex.Message);
			return IntPtr.Zero;
		}
	}

	public unsafe static List<IntPtr> GetArrayElements(IntPtr arrayPtr)
	{
		if (arrayPtr == IntPtr.Zero)
		{
			return null;
		}
		int num = (int)IL2CPP.il2cpp_array_length(arrayPtr);
		List<IntPtr> list = new List<IntPtr>(num);
		IntPtr intPtr = arrayPtr + 4 * IntPtr.Size;
		for (int i = 0; i < num; i++)
		{
			list.Add(*(IntPtr*)(void*)(intPtr + i * IntPtr.Size));
		}
		return list;
	}

	public unsafe static string ReadField(IntPtr rowPtr, FieldEntry entry)
	{
		IntPtr intPtr = *(IntPtr*)(void*)(rowPtr + entry.Offset);
		if (!(intPtr == IntPtr.Zero))
		{
			return IL2CPP.Il2CppStringToManaged(intPtr);
		}
		return null;
	}

	public static void WriteField(IntPtr rowPtr, FieldEntry entry, string value)
	{
		IL2CPP.il2cpp_gc_wbarrier_set_field(rowPtr, rowPtr + entry.Offset, IL2CPP.ManagedStringToIl2Cpp(value));
	}
}
