using System;
using System.IO;
using System.Net.Http;
using System.Text;
using AbyssMod.Patches;
using AbyssMod.Services;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using Utility.Fonts;
using Utility.Toast;

namespace AbyssMod;

[BepInPlugin("AbyssMod", "AbyssMod", "2.1.0")]
public class Plugin : BasePlugin
{
	private const int HttpTimeoutSeconds = 10;

	private const int PooledConnectionLifetimeMinutes = 5;

	private const int PooledConnectionIdleTimeoutMinutes = 2;

	public static ConfigFile ConfigFile;

	public new static ManualLogSource Log;

	public static MonoBehaviour Instance;

	public static TranslationManager Trans;

	public override void Load()
	{
		try
		{
			Console.OutputEncoding = Encoding.UTF8;
		}
		catch
		{
		}
		Log = base.Log;
		ConfigFile = Config;
		ManualLogSource log = Log;
		BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(18, 1, out var isEnabled);
		if (isEnabled)
		{
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("Plugin ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted("AbyssMod");
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" is loaded!");
		}
		log.LogInfo(bepInExInfoLogInterpolatedStringHandler);
		base.AddComponent<ToastUI>();
		AbyssMod.Config.Initialize();
		Instance = (MonoBehaviour)(object)base.AddComponent<Hotkey>();
		MasterMapping.Load();
		Initialize();
		PatchManager.Initialize();
		Trans.Initialize();
		Toast.Success("AbyssMod", "Mod 加载成功，版本: 2.1.0");
	}

	private static void Initialize()
	{
		HttpClient httpClient = new HttpClient(new CryptoHandler(new SocketsHttpHandler
		{
			PooledConnectionLifetime = TimeSpan.FromMinutes(5.0),
			PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2.0)
		}))
		{
			Timeout = TimeSpan.FromSeconds(10.0)
		};
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AbyssMod/2.1.0");
		TranslationCache cache = new TranslationCache(AbyssMod.Config.TranslationCDN.Value, Path.Combine(Paths.PluginPath, "AbyssMod", "cache", "translations"), AbyssMod.Config.TranslationLanguage.Value, httpClient);
		string value = AbyssMod.Config.FontBundlePath.Value;
		FontHelper font = new FontHelper(Path.IsPathRooted(value) ? value : Path.Combine(Paths.PluginPath, value));
		Trans = new TranslationManager(cache, font);
		MachineTranslator.Initialize(Path.Combine(Paths.PluginPath, "AbyssMod", "cache", "translations", "other"), AbyssMod.Config.TranslationLanguage.Value);
	}

	public override bool Unload()
	{
		Toast.Clear();
		MachineTranslator.Save();
		return base.Unload();
	}
}
