using BepInEx;
using BepInEx.Logging;

namespace SmartItemSaving;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
	// Constant variables
	public const int FormatVersion = 2;

	// Plugin variables
	public static Plugin Instance { get; private set; }
	public static new Config Config { get; private set; }
	public static new ManualLogSource Logger { get; private set; }

	private void Awake()
	{
		Instance = this;
		Config = new(base.Config);
		Logger = base.Logger;

		Patches.Initialize();

		Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} is loaded!");
	}
}