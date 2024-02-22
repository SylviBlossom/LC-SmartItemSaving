﻿using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartItemSaving;
public class Config
{
	public static ConfigEntry<bool> FixItemIds { get; private set; }
	public static ConfigEntry<bool> RemoveIfNotFound { get; private set; }
	
	public static ConfigEntry<bool> BackupOnLoad { get; private set; }

	public Config(ConfigFile cfg)
	{
		FixItemIds = cfg.Bind("FixItemIds", "Enabled", true, "Attempts to fix changed item ids on load by comparing the saved item names.");
		RemoveIfNotFound = cfg.Bind("FixItemIds", "RemoveIfNotFound", true, "Removes items with missing names instead of replacing them with an item of the same ID.");

		BackupOnLoad = cfg.Bind("Misc", "BackupOnLoad", true, "Whether save files should be backed up on load incase the mod causes any destructive behaviour. NOTE: Only one backup is made!");
	}
}
