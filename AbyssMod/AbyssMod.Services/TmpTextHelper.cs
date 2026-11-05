using Object = UnityEngine.Object;
using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace AbyssMod.Services;

public static class TmpTextHelper
{
	private static FieldInfo _mText;

	private static FieldInfo _havePropertiesChanged;

	private static bool _resolved;

	private static bool _loggedFallback;

	private static void EnsureFields()
	{
		if (!_resolved)
		{
			_resolved = true;
			_mText = AccessTools.Field(typeof(TMP_Text), "m_text") ?? AccessTools.Field(typeof(TMP_Text), "m_Text") ?? FindStringField("m_text");
			_havePropertiesChanged = AccessTools.Field(typeof(TMP_Text), "m_havePropertiesChanged") ?? AccessTools.Field(typeof(TMP_Text), "m_HasPropertyChanged");
		}
	}

	private static FieldInfo FindStringField(string preferredName)
	{
		FieldInfo[] fields = typeof(TMP_Text).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (FieldInfo fieldInfo in fields)
		{
			if (!(fieldInfo.FieldType != typeof(string)) && string.Equals(fieldInfo.Name, preferredName, StringComparison.OrdinalIgnoreCase))
			{
				return fieldInfo;
			}
		}
		return null;
	}

	public static bool TrySetTextDirect(TMP_Text tmp, string text)
	{
		if ((Object)(object)tmp == (Object)null || text == null)
		{
			return false;
		}
		EnsureFields();
		try
		{
			if (_mText != null)
			{
				_mText.SetValue(tmp, text);
				_havePropertiesChanged?.SetValue(tmp, true);
				tmp.ForceMeshUpdate(true, false);
				return true;
			}
			if (!_loggedFallback)
			{
				_loggedFallback = true;
				Logger.Info("TmpTextHelper: m_text field unavailable, using TMP_Text.text property fallback");
			}
			tmp.text = text;
			tmp.ForceMeshUpdate(true, false);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Warn("TrySetTextDirect failed: " + ex.Message);
			return false;
		}
	}
}
