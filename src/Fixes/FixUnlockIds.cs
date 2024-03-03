using System;
using System.Collections.Generic;

namespace SmartItemSaving.Fixes;

public static class FixUnlockIds
{
	public static void SaveFixUnlockIds(GameNetworkManager gameNetworkManager, StartOfRound startOfRound, List<int> unlocks)
	{
		// List of unlock names correlating to unlock ids
		var names = new string[unlocks.Count];

		for (var i = 0; i < unlocks.Count; i++)
		{
			// TODO: Resolve name conflicts (troublesome, as potentially conflicting names are already saved)
			names[i] = startOfRound.unlockablesList.unlockables[unlocks[i]].unlockableName;
		}

		// Save our modded values
		ES3.Save(SaveKeys.UnlockNames, names, gameNetworkManager.currentSaveFileName);

		Plugin.Logger.LogInfo($"Save | Unlockables | Successfully saved {names.Length} unlocks");
	}

	public static void LoadFixUnlockIds(StartOfRound startOfRound, ref int[] unlocks)
	{
		var currentSaveFileName = GameNetworkManager.Instance.currentSaveFileName;

		// Skip if parity check failed
		if (!General.LoadedParityCheck)
		{
			return;
		}

		// Skip if disabled
		if (!Config.FixItemIds.Value)
		{
			Plugin.Logger.LogInfo("Load | Unlockables | FixItemIds is disabled, skipping unlockable id fixing");
			return;
		}

		// Make sure modded values exist
		if (!ES3.KeyExists(SaveKeys.UnlockNames, currentSaveFileName))
		{
			Plugin.Logger.LogWarning($"Load | Unlockables | No unlock name save data found, skipping unlockable id fixing");
			return;
		}

		// Load values for unlock id fixing
		var loadedNames = ES3.Load(SaveKeys.UnlockNames, currentSaveFileName, new string[0]);

		// Make sure our lists are the same size
		if (loadedNames.Length != unlocks.Length)
		{
			Plugin.Logger.LogError($"Load | Unlockables | Unlocks count mismatch (Expected {loadedNames.Length}, got {unlocks.Length}), likely outdated save, skipping unlockable id fixing");
			return;
		}

		// Fix unlock id mismatches
		var newUnlocks = new List<int>();

		for (var i = 0; i < loadedNames.Length; i++)
		{
			var name = loadedNames[i];
			var id = unlocks[i];

			if (string.IsNullOrEmpty(name))
			{
				Plugin.Logger.LogWarning($"Load | Unlockables | Found empty unlock name for unlock id {id}, loading normally");

				newUnlocks.Add(id);
				continue;
			}

			var asNameSuffix = "";
			if (startOfRound.unlockablesList.unlockables.Count > id && !string.IsNullOrEmpty(startOfRound.unlockablesList.unlockables[id].unlockableName))
			{
				asNameSuffix = $"as \"{startOfRound.unlockablesList.unlockables[id].unlockableName}\"";
			}

			// Get all unlockable ids whos name matches the saved name
			var matchingIds = new List<int>();

			for (var j = 0; j < startOfRound.unlockablesList.unlockables.Count; j++)
			{
				var unlockable = startOfRound.unlockablesList.unlockables[j];

				if (unlockable.unlockableName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
				{
					matchingIds.Add(j);
				}
			}

			// Get the correct unlockable id and add it
			if (matchingIds.Count == 0)
			{
				if (Config.RemoveIfNotFound.Value)
				{
					Plugin.Logger.LogWarning($"Load | Unlockables | No unlock id found for unlock \"{name}\", removing item");
					continue;
				}
				else
				{
					Plugin.Logger.LogWarning($"Load | Unlockables | No unlock id found for unlock \"{name}\", loading normally with id {id} {asNameSuffix}");

					newUnlocks.Add(id);
					continue;
				}
			}

			// TODO: Resolve name conflicts (troublesome, as potentially conflicting names are already saved)
			if (matchingIds.Count > 1)
			{
				var idsList = string.Join(",", matchingIds);

				if (matchingIds.Contains(id))
				{
					Plugin.Logger.LogWarning($"Load | Unlockables | Multiple ids ({idsList}) found for unlock \"{name}\", loading normally with id {id} {asNameSuffix}");

					newUnlocks.Add(id);
					continue;
				}
				else
				{
					Plugin.Logger.LogWarning($"Load | Unlockables | Multiple ids ({idsList}) found for unlock \"{name}\", arbitrarily loading {matchingIds[0]}");
				}
			}

			if (id != matchingIds[0])
			{
				if (id < startOfRound.unlockablesList.unlockables.Count && id >= 0)
				{
					Plugin.Logger.LogInfo($"Load | Unlockables | Fixed unlock mismatch ({id}, \"{startOfRound.unlockablesList.unlockables[id].unlockableName}\" -> {matchingIds[0]}, \"{name}\")");
				}
				else
				{
					Plugin.Logger.LogInfo($"Load | Unlockables | Fixed unlock mismatch ({id}, unknown -> {matchingIds[0]}, \"{name}\")");
				}

				newUnlocks.Add(matchingIds[0]);
				continue;
			}

			newUnlocks.Add(id);
		}

		// Return the modified list of unlockables
		unlocks = newUnlocks.ToArray();

		Plugin.Logger.LogInfo($"Load | Unlockables | Loaded {newUnlocks.Count} unlocks");
	}
}
