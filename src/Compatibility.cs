using BepInEx.Bootstrap;

namespace SmartItemSaving;

public static class Compatibility
{
	public static bool HasSaveItemRotations()
		=> HasSaveItemRotations(out _);
	public static bool HasSaveItemRotations(out BepInEx.PluginInfo pluginInfo)
		=> Chainloader.PluginInfos.TryGetValue("moe.sylvi.SaveItemRotations", out pluginInfo);

	public static bool HasLethalLevelLoader()
		=> HasLethalLevelLoader(out _);
	public static bool HasLethalLevelLoader(out BepInEx.PluginInfo pluginInfo)
		=> Chainloader.PluginInfos.TryGetValue("imabatby.lethallevelloader", out pluginInfo);
}
