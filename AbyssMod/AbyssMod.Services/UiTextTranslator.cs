using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AbyssMod.Services;

public static class UiTextTranslator
{
	private static bool _loadRequested;

	public static string Translate(TMP_Text text, string value)
	{
		if (!Config.Translation.Value || Plugin.Trans == null || value == null)
		{
			return value;
		}
		if (value.Length == 0)
		{
			return value;
		}
		Dictionary<string, string> table = GetTable();
		if (table == null || table.Count == 0)
		{
			return value;
		}
		if (IsAlreadyTranslated(table, value))
		{
			return value;
		}
		if (table.TryGetValue(value, out var value2) && !string.IsNullOrEmpty(value2))
		{
			return value2;
		}
		string transformPath = GetTransformPath(((Object)(object)text != (Object)null) ? text.transform : null);
		if (!string.IsNullOrEmpty(transformPath) && table.TryGetValue(transformPath, out value2) && !string.IsNullOrEmpty(value2))
		{
			return value2;
		}
		return value;
	}

	private static bool IsAlreadyTranslated(Dictionary<string, string> table, string value)
	{
		foreach (string value2 in table.Values)
		{
			if (string.Equals(value2, value, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private static Dictionary<string, string> GetTable()
	{
		if (!_loadRequested)
		{
			_loadRequested = true;
			try
			{
				Plugin.Trans.EnsureStaticTranslationsLoadedAsync();
			}
			catch (Exception ex)
			{
				Logger.Warn("UI text translation load request skipped: " + ex.Message);
			}
		}
		return Plugin.Trans.GetTable("ui_texts");
	}

	private static string GetTransformPath(Transform transform)
	{
		if ((Object)(object)transform == (Object)null)
		{
			return null;
		}
		Stack<string> stack = new Stack<string>();
		Transform val = transform;
		while ((Object)(object)val != (Object)null)
		{
			stack.Push(((Object)val).name);
			val = val.parent;
		}
		return string.Join("/", stack);
	}
}
