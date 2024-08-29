using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartItemSaving.Fixes;

public static class FixItemIds
{
	public static void Save(GameNetworkManager gameNetworkManager, List<int> ids)
	{
		// List of item names correlating to item ids
		var names = new string[ids.Count];

		// Whether each item has scrap value or save data (for safely removing)
		var hasValue = new bool[ids.Count];
		var hasData = new bool[ids.Count];

		for (var i = 0; i < ids.Count; i++)
		{
			var id = ids[i];
			var itemsList = StartOfRound.Instance.allItemsList.itemsList;

			if (itemsList.Count < id || string.IsNullOrEmpty(itemsList[id].itemName))
			{
				Plugin.Logger.LogWarning($"Save | Items | No item name found for item id {id}");

				names[i] = "";
				hasValue[i] = false;
				hasData[i] = false;
			}
			else
			{
				var item = itemsList[id];
				var name = item.itemName;

				var allMatching = itemsList.Where(x => x.itemName.Equals(name, StringComparison.InvariantCultureIgnoreCase)).ToList();
				var matchIndex = allMatching.IndexOf(itemsList[id]);

				if (allMatching.Count > 1 && matchIndex >= 0)
				{
					name += $"##ID{matchIndex}";
				}

				names[i] = name;
				hasValue[i] = item.isScrap;
				hasData[i] = item.saveItemVariable;
			}
		}

		// Save our modded values
		ES3.Save(SaveKeys.ItemNames, names, gameNetworkManager.currentSaveFileName);
		ES3.Save(SaveKeys.ItemHasValue, hasValue, gameNetworkManager.currentSaveFileName);
		ES3.Save(SaveKeys.ItemHasData, hasData, gameNetworkManager.currentSaveFileName);

		Plugin.Logger.LogInfo($"Save | Items | Successfully saved {names.Length} items");
	}

	public static void Load(StartOfRound startOfRound, int[] ids, ref int[] values, ref int[] data)
	{
		var currentSaveFileName = GameNetworkManager.Instance.currentSaveFileName;

		// Skip if parity check failed
		if (!General.LoadedParityCheck)
		{
			return;
		}

		// Make sure modded values exist
		if (!ES3.KeyExists(SaveKeys.ItemNames, currentSaveFileName))
		{
			Plugin.Logger.LogWarning($"Load | Items | No item name save data found, skipping item id fixing");
			return;
		}

		// Load values for item id fixing
		var loadedNames = ES3.Load(SaveKeys.ItemNames, currentSaveFileName, new string[0]);
		var loadedHasValue = ES3.Load(SaveKeys.ItemHasValue, currentSaveFileName, new bool[ids.Length]);
		var loadedHasData = ES3.Load(SaveKeys.ItemHasData, currentSaveFileName, new bool[ids.Length]);

		// Skip if disabled
		if (!Config.FixItemIds.Value)
		{
			Plugin.Logger.LogInfo("Load | Items | FixItemIds is disabled, skipping item id fixing");
			return;
		}

		// Skip if LethalLevelLoader is active
		if (Compatibility.HasLethalLevelLoader(out var pluginInfo) && !Config.ForceHandleFixItemIds.Value)
		{
			Plugin.Logger.LogInfo($"Load | Items | Found mod {pluginInfo.Metadata.Name} v{pluginInfo.Metadata.Version}, skipping item id fixing");
			return;
		}

		// Make sure our lists are the same size
		if (loadedNames.Length != ids.Length)
		{
			Plugin.Logger.LogError($"Load | Items | Item count mismatch (Expected {loadedNames.Length}, got {ids.Length}), likely outdated save, skipping item id fixing");
			return;
		}

		// Calculate the loaded value total from the vanilla values
		var valueTotal = 0;
		if (values != null)
		{
			for (var i = 0; i < values.Length; i++)
			{
				valueTotal += values[i];
			}
		}

		// Modify item ids to match their names
		for (var i = 0; i < ids.Length; i++)
		{
			var id = ids[i];
			var name = loadedNames[i];

			if (string.IsNullOrEmpty(name))
			{
				Plugin.Logger.LogWarning($"Load | Items | Found empty item name for item id {id}, loading normally");
				continue;
			}

			var asNameSuffix = "";
			if (startOfRound.allItemsList.itemsList.Count > id && !string.IsNullOrEmpty(startOfRound.allItemsList.itemsList[id].itemName))
			{
				asNameSuffix = $"as \"{startOfRound.allItemsList.itemsList[id].itemName}\"";
			}

			// Get the index of the item in the list of matching names, if there were multiple on save
			var nameId = 0;
			var hasNameId = false;

			var nameIdIndex = name.IndexOf("##ID");
			if (nameIdIndex != -1)
			{
				if (!int.TryParse(name.Substring(nameIdIndex + 4), out nameId))
				{
					Plugin.Logger.LogError($"Load | Items | Failed to parse item name {name}");
				};
				name = name.Substring(0, nameIdIndex);
				hasNameId = true;
			}

			// Get all item ids whos name matches the saved name
			var matchingIds = new List<int>();

			for (var j = 0; j < startOfRound.allItemsList.itemsList.Count; j++)
			{
				var item = startOfRound.allItemsList.itemsList[j];

				if (item.itemName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
				{
					matchingIds.Add(j);
				}
			}

			if (matchingIds.Count == 0)
			{
				if (Config.RemoveIfNotFound.Value)
				{
					Plugin.Logger.LogWarning($"Load | Items | No item id found for item \"{name}\", removing item");

					ids[i] = int.MaxValue;
					continue;
				}
				else
				{
					Plugin.Logger.LogWarning($"Load | Items | No item id found for item \"{name}\", loading normally with id {id} {asNameSuffix}");
					continue;
				}
			}

			if (matchingIds.Count <= nameId)
			{
				Plugin.Logger.LogWarning($"Load | Items | Saved {id} as \"{name}\" #{nameId + 1}, but only found {matchingIds.Count} name duplicates, loading as #{matchingIds.Count}");
				nameId = matchingIds.Count - 1;
			}

			if (matchingIds.Count > 1 && !hasNameId)
			{
				var idsList = string.Join(",", matchingIds);

				if (matchingIds.Contains(id))
				{
					Plugin.Logger.LogWarning($"Load | Items | Multiple ids ({idsList}) found for item \"{name}\", loading normally with id {id} {asNameSuffix}");
					continue;
				}
				else
				{
					Plugin.Logger.LogWarning($"Load | Items | Multiple ids ({idsList}) found for item \"{name}\", arbitrarily loading {matchingIds[0]}");
				}
			}

			if (id != matchingIds[nameId])
			{
				if (id < startOfRound.allItemsList.itemsList.Count && id >= 0)
				{
					Plugin.Logger.LogInfo($"Load | Items | Fixed item mismatch ({id}, \"{startOfRound.allItemsList.itemsList[id].itemName}\" -> {matchingIds[nameId]}, \"{name}\")");
				}
				else
				{
					Plugin.Logger.LogInfo($"Load | Items | Fixed item mismatch ({id}, unknown -> {matchingIds[nameId]}, \"{name}\")");
				}
				ids[i] = matchingIds[nameId];
			}
		}

		// Remake item values and data lists for potentially changed/removed items
		var newValues = new List<int>();
		var newData = new List<int>();

		var origValueIndex = 0;
		var origDataIndex = 0;

		var newValueTotal = 0;

		for (var i = 0; i < ids.Length; i++)
		{
			if (ids[i] < startOfRound.allItemsList.itemsList.Count)
			{
				var item = startOfRound.allItemsList.itemsList[ids[i]];

				if (item.isScrap)
				{
					if (loadedHasValue[i])
					{
						newValues.Add(values[origValueIndex]);

						newValueTotal += values[origValueIndex];
					}
					else
					{
						var randomValue = (int)(UnityEngine.Random.Range(item.minValue, item.maxValue - 1) * RoundManager.Instance.scrapValueMultiplier);
						newValues.Add(randomValue);

						newValueTotal += randomValue;

						Plugin.Logger.LogWarning($"Load | Items | Assigning random value of {randomValue} to \"{item.itemName}\"");
					}
				}

				if (item.saveItemVariable)
				{
					if (loadedHasData[i])
					{
						newData.Add(data[origDataIndex]);
					}
					else
					{
						newData.Add(0);

						Plugin.Logger.LogWarning($"Load | Items | Loaded item \"{item.itemName}\" without its associated save data");
					}
				}
			}

			if (loadedHasValue[i])
			{
				origValueIndex++;
			}

			if (loadedHasData[i])
			{
				origDataIndex++;
			}
		}

		var valueDiffStr = (newValueTotal - valueTotal).ToString();
		if (newValueTotal > valueTotal)
		{
			valueDiffStr = "+" + valueDiffStr;
		}

		Plugin.Logger.LogInfo($"Load | Items | Loaded {loadedNames.Length} items with a total value of {newValueTotal} ({valueDiffStr})");

		values = newValues.ToArray();
		data = newData.ToArray();
	}
}
