using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartItemSaving;
public class Config
{
	// FixItemIds
	public static ConfigEntry<bool> FixItemIds { get; private set; }
	public static ConfigEntry<bool> RemoveIfNotFound { get; private set; }

	// Misc
	public static ConfigEntry<bool> SaveItemRotation { get; private set; }
	public static ConfigEntry<bool> FixItemFalling { get; private set; }
	public static ConfigEntry<bool> BetterSyncItems { get; private set; }
	public static ConfigEntry<bool> BackupOnLoad { get; private set; }

	// Compatibility
	public static ConfigEntry<bool> ForceHandleFixItemIds { get; private set; }
	public static ConfigEntry<bool> ForceHandleSaveItemRotation { get; private set; }

	public Config(ConfigFile cfg)
	{
		FixItemIds = cfg.Bind("FixItemIds", "Enabled", true, "Attempts to fix changed item ids on load by comparing the saved item names.");
		RemoveIfNotFound = cfg.Bind("FixItemIds", "RemoveIfNotFound", true, "Removes items with missing names instead of replacing them with an item of the same ID.");

		SaveItemRotation = cfg.Bind("Misc", "SaveItemRotation", true, "Saves and loads the rotation of items on the ship.");
		FixItemFalling = cfg.Bind("Misc", "FixItemFalling", true, "Fixes items falling through furniture on load.");
		BetterSyncItems = cfg.Bind("Misc", "BetterSyncItems", true, "[CLIENT AND HOST] Correctly synchronizes item positions and rotations upon joining (important for Fix Item Falling and Save Item Rotation).");
		BackupOnLoad = cfg.Bind("Misc", "BackupOnLoad", true, "Whether save files should be backed up on load incase the mod causes any destructive behaviour. NOTE: Only one backup is made!");

		ForceHandleFixItemIds = cfg.Bind("Compatibility", "ForceHandleFixItemIds", false, "Forces this mod to handle item ID fixing even if the mod LethalLevelLoader is active, which uses a generally more comprehensive ID fixing system.");
		ForceHandleSaveItemRotation = cfg.Bind("Compatibility", "ForceHandleSaveItemRotation", false, "Forces this mod to handle item rotation loading even if the mod SaveItemRotations is active.");
	}
}
