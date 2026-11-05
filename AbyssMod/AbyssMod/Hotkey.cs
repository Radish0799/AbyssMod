using System;
using System.Collections.Generic;
using AbyssMod.Patches;
using BepInEx.Configuration;
using UnityEngine;

namespace AbyssMod;

public class Hotkey : MonoBehaviour
{
	private const float DebounceInterval = 0.15f;

	private const float RefreshInterval = 0.5f;

	private float _lastRefreshTime;

	private readonly Dictionary<KeyCode, float> _lastPressTime = new Dictionary<KeyCode, float>();

	private void Update()
	{
		CheckToggle((KeyCode)289, () => Config.Translation);
		CheckToggle((KeyCode)290, () => Config.VoiceInterruption);
		if (Input.GetKeyDown((KeyCode)291) && CanTrigger((KeyCode)291))
		{
			Plugin.ConfigFile.Reload();
			Logger.Info("Config reloaded");
		}
		if (Config.Translation.Value && Time.unscaledTime - _lastRefreshTime >= 0.5f)
		{
			_lastRefreshTime = Time.unscaledTime;
			GeneralTextPatch.RefreshVisibleText();
		}
	}

	private void CheckToggle(KeyCode key, Func<ConfigEntry<bool>> getter)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		if (Input.GetKeyDown(key) && CanTrigger(key))
		{
			ConfigEntry<bool> configEntry = getter();
			configEntry.Value = !configEntry.Value;
		}
	}

	private bool CanTrigger(KeyCode key)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		float time = Time.time;
		if (_lastPressTime.TryGetValue(key, out var value) && time - value < 0.15f)
		{
			return false;
		}
		_lastPressTime[key] = time;
		return true;
	}

	private static bool IsAltPressed()
	{
		if (!Input.GetKey((KeyCode)308))
		{
			return Input.GetKey((KeyCode)307);
		}
		return true;
	}
}
