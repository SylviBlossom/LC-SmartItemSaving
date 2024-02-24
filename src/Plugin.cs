using BepInEx;
using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartItemSaving;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
	public const int FormatVersion = 0;

	public const string SaveKey_FormatVersion = $"{PluginInfo.PLUGIN_GUID}_formatVersion";
	public const string SaveKey_ItemNames = $"{PluginInfo.PLUGIN_GUID}_itemNames";
	public const string SaveKey_ItemHasValue = $"{PluginInfo.PLUGIN_GUID}_itemHasValue";
	public const string SaveKey_ItemHasData = $"{PluginInfo.PLUGIN_GUID}_itemHasData";
	public const string SaveKey_ItemIDsCopy = $"{PluginInfo.PLUGIN_GUID}_itemIDsCopy";
	public const string SaveKey_ValueTotal = $"{PluginInfo.PLUGIN_GUID}_itemValueTotal";

	public static Plugin Instance { get; private set; }
	public static new Config Config { get; private set; }
	public static new ManualLogSource Logger { get; private set; }

	private void Awake()
	{
		Instance = this;
		Config = new(base.Config);
		Logger = base.Logger;

		IL.GameNetworkManager.SaveItemsInShip += GameNetworkManager_SaveItemsOnShip;

		On.StartOfRound.LoadShipGrabbableItems += StartOfRound_LoadShipGrabbableItems;
		IL.StartOfRound.LoadShipGrabbableItems += StartOfRound_LoadShipGrabbableItems_IL;

		// Plugin startup logic
		Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} is loaded!");
	}

	private static void GameNetworkManager_SaveItemsOnShip(ILContext il)
	{
		var cursor = new ILCursor(il);

		if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<ES3>("DeleteKey")))
		{
			Logger.LogError("Failed IL hook for GameNetworkManager.SaveItemsInShip @ Delete empty keys");
			return;
		}

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.EmitDelegate<Action<GameNetworkManager>>(self =>
		{
			ES3.DeleteKey(SaveKey_FormatVersion, self.currentSaveFileName);
			ES3.DeleteKey(SaveKey_ItemNames, self.currentSaveFileName);
			ES3.DeleteKey(SaveKey_ItemHasValue, self.currentSaveFileName);
			ES3.DeleteKey(SaveKey_ItemHasData, self.currentSaveFileName);
			ES3.DeleteKey(SaveKey_ItemIDsCopy, self.currentSaveFileName);
			ES3.DeleteKey(SaveKey_ValueTotal, self.currentSaveFileName);
		});

		var listLocs = new int[4];

		for (var i = 0; i < listLocs.Length; i++)
		{
			var loc = -1;

			if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchStloc(out loc)))
			{
				Logger.LogError($"Failed IL hook for GameNetworkManager.SaveItemsInShip @ Get list {i} local");
				return;
			}

			listLocs[i] = loc;
		}

		var idsLoc = listLocs[0];
		var posLoc = listLocs[1];
		var valuesLoc = listLocs[2];
		var dataLoc = listLocs[3];

		if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<ES3>("Save")))
		{
			Logger.LogError("Failed IL hook for GameNetworkManager.SaveItemsInShip @ First save call");
			return;
		}

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.Emit(OpCodes.Ldloc, idsLoc);
		cursor.Emit(OpCodes.Ldloc, valuesLoc);
		cursor.EmitDelegate<Action<GameNetworkManager, List<int>, List<int>>>((self, ids, values) =>
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
					Logger.LogWarning($"Save - No item name found for item id {id}");

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

			// Value total (for savedata verification)
			var valueTotal = 0;

			for (var i = 0; i < values.Count; i++)
			{
				valueTotal += values[i];
			}

			// Save our modded values
			ES3.Save(SaveKey_FormatVersion, FormatVersion, self.currentSaveFileName);
			ES3.Save(SaveKey_ItemNames, names, self.currentSaveFileName);
			ES3.Save(SaveKey_ItemHasValue, hasValue, self.currentSaveFileName);
			ES3.Save(SaveKey_ItemHasData, hasData, self.currentSaveFileName);
			ES3.Save(SaveKey_ItemIDsCopy, ids.ToArray(), self.currentSaveFileName);
			ES3.Save(SaveKey_ValueTotal, valueTotal, self.currentSaveFileName);

			Logger.LogInfo($"Save - Saved {names.Length} items with a total value of {valueTotal}");
		});
	}

	private void StartOfRound_LoadShipGrabbableItems(On.StartOfRound.orig_LoadShipGrabbableItems orig, StartOfRound self)
	{
		if (Config.BackupOnLoad.Value)
		{
			Logger.LogInfo("Creating save backup");
			ES3.CreateBackup(GameNetworkManager.Instance.currentSaveFileName);
		}

		orig(self);
	}

	private static void StartOfRound_LoadShipGrabbableItems_IL(ILContext il)
	{
		var cursor = new ILCursor(il);

		var idsLoc = -1;

		if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchLdstr("shipGrabbableItemIDs")) ||
			!cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchStloc(out idsLoc)))
		{
			Logger.LogError("Failed IL hook for StartOfRound.LoadShipGrabbableItems @ Get item IDs local");
			return;
		}

		if (!cursor.TryGotoNext(MoveType.Before,
				instr1 => instr1.MatchBrfalse(out _),
				instr2 => instr2.MatchLdstr("shipScrapValues")))
		{
			Logger.LogError("Failed IL hook for StartOfRound.LoadShipGrabbableItems @ Load values");
			return;
		}

		var valuesLoc = -1;

		if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchStloc(out valuesLoc)))
		{
			Logger.LogError("Failed IL hook for StartOfRound.LoadShipGrabbableItems @ Get item values local");
			return;
		}

		var dataLoc = 1;

		if (!cursor.TryGotoNext(MoveType.After,
				instr1 => instr1.MatchCallOrCallvirt<ES3>("Load"),
				instr2 => instr2.MatchStloc(out dataLoc)))
		{
			Logger.LogError("Failed IL hook for StartOfRound.LoadShipGrabbableItems @ Get item data local");
			return;
		}

		cursor.MoveAfterLabels();

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.Emit(OpCodes.Ldloc, idsLoc);
		cursor.Emit(OpCodes.Ldloc, valuesLoc);
		cursor.Emit(OpCodes.Ldloc, dataLoc);
		cursor.EmitDelegate<Func<StartOfRound, int[], int[], int[], (int[], int[]) >>((self, ids, values, data) =>
		{
			try
			{
				var currentSaveFileName = GameNetworkManager.Instance.currentSaveFileName;

				// Make sure modded values exist
				if (!ES3.KeyExists(SaveKey_FormatVersion, currentSaveFileName))
				{
					Logger.LogWarning($"Load - No {PluginInfo.PLUGIN_NAME} save data found, skipping all");
					return (values, data);
				}

				// Load values for item id fixing
				var loadedFormat = ES3.Load<int>(SaveKey_FormatVersion, currentSaveFileName);
				var loadedNames = ES3.Load<string[]>(SaveKey_ItemNames, currentSaveFileName);
				var loadedHasValue = ES3.Load<bool[]>(SaveKey_ItemHasValue, currentSaveFileName) ?? new bool[ids.Length];
				var loadedHasData = ES3.Load<bool[]>(SaveKey_ItemHasData, currentSaveFileName) ?? new bool[ids.Length];
				var loadedIDs = ES3.Load<int[]>(SaveKey_ItemIDsCopy, currentSaveFileName);
				var loadedValueTotal = ES3.Load<int>(SaveKey_ValueTotal, currentSaveFileName);

				// Skip if disabled
				if (!Config.FixItemIds.Value)
				{
					Logger.LogInfo("Load - Item id fixing is disabled, skipping");
				}

				if (loadedNames == null)
				{
					// Probably unreachable but vanilla code checks this so
					Logger.LogWarning("Load - No item names found, skipping item id fixing");
					return (values, data);
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

				// Calculate whether item ids are equal
				var idsEqual = true;
				if (loadedIDs != null)
				{
					for (var i = 0; i < ids.Length; i++)
					{
						if (loadedIDs[i] != ids[i])
						{
							idsEqual = false;
							break;
						}
					}
				}

				// Validate data to make sure there's no gaps in the mod's knowledge
				if (loadedIDs.Length != ids.Length)
				{
					Logger.LogError($"Load - Item count mismatch (Expected {loadedNames.Length}, got {ids.Length}), likely outdated save, skipping item id fixing");
					return (values, data);
				}

				if (valueTotal != loadedValueTotal)
				{
					Logger.LogError($"Load - Item values mismatch (Expected {loadedValueTotal}, got {valueTotal}), likely outdated save, skipping item id fixing");
					return (values, data);
				}

				if (!idsEqual)
				{
					Logger.LogError($"Load - Item id mismatch, likely outdated save, skipping item id fixing");
					return (values, data);
				}

				// Modify item ids to match their names
				for (var i = 0; i < ids.Length; i++)
				{
					var id = ids[i];
					var name = loadedNames[i];

					if (string.IsNullOrEmpty(name))
					{
						Logger.LogWarning($"Load - Found empty item name for item id {id}, loading normally");
						continue;
					}

					var asNameSuffix = "";
					if (self.allItemsList.itemsList.Count > id && !string.IsNullOrEmpty(self.allItemsList.itemsList[id].itemName))
					{
						asNameSuffix = $"as \"{self.allItemsList.itemsList[id].itemName}\"";
					}

					// Get the index of the item in the list of matching names, if there were multiple on save
					var nameId = 0;
					var hasNameId = false;

					var nameIdIndex = name.IndexOf("##ID");
					if (nameIdIndex != -1)
					{
						if (!int.TryParse(name.Substring(nameIdIndex + 4), out nameId))
						{
							Logger.LogError($"Load - Failed to parse item name {name}");
						};
						name = name.Substring(0, nameIdIndex);
						hasNameId = true;
					}

					// Get all item ids whos name matches the saved name
					var matchingIds = new List<int>();

					for (var j = 0; j < self.allItemsList.itemsList.Count; j++)
					{
						var item = self.allItemsList.itemsList[j];

						if (item.itemName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
						{
							matchingIds.Add(j);
						}
					}

					if (matchingIds.Count == 0)
					{
						if (Config.RemoveIfNotFound.Value)
						{
							Logger.LogWarning($"Load - No item id found for item \"{name}\", removing item");

							ids[i] = int.MaxValue;
							continue;
						}
						else
						{
							Logger.LogWarning($"Load - No item id found for item \"{name}\", loading normally with id {id} {asNameSuffix}");
							continue;
						}
					}

					if (matchingIds.Count <= nameId)
					{
						Logger.LogWarning($"Load - Saved {id} as \"{name}\" #{nameId + 1}, but only found {matchingIds.Count} name duplicates, loading as #{matchingIds.Count}");
						nameId = matchingIds.Count - 1;
					}

					if (matchingIds.Count > 1 && !hasNameId)
					{
						var idsList = string.Join(",", matchingIds);

						if (matchingIds.Contains(id))
						{
							Logger.LogWarning($"Load - Multiple ids ({idsList}) found for item \"{name}\", loading normally with id {id} {asNameSuffix}");
							continue;
						}
						else
						{
							Logger.LogWarning($"Load - Multiple ids ({idsList}) found for item \"{name}\", arbitrarily loading {matchingIds[0]}");
						}
					}

					if (id != matchingIds[nameId])
					{
						if (id < self.allItemsList.itemsList.Count && id >= 0)
						{
							Logger.LogInfo($"Load - Fixed item mismatch ({id}, \"{self.allItemsList.itemsList[id].itemName}\" -> {matchingIds[nameId]}, \"{name}\")");
						}
						else
						{
							Logger.LogInfo($"Load - Fixed item mismatch ({id}, unknown -> {matchingIds[nameId]}, \"{name}\")");
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
					if (ids[i] < self.allItemsList.itemsList.Count)
					{
						var item = self.allItemsList.itemsList[ids[i]];

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

								Logger.LogWarning($"Load - Assigning random value of {randomValue} to \"{item.itemName}\"");
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

								Logger.LogWarning($"Load - Loaded item \"{item.itemName}\" without its associated save data");
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

				var valueDiffStr = (newValueTotal - loadedValueTotal).ToString();
				if (newValueTotal > loadedValueTotal)
				{
					valueDiffStr = "+" + valueDiffStr;
				}

				Logger.LogInfo($"Load - Loaded {loadedNames.Length} items with a total value of {newValueTotal} ({valueDiffStr})");

				return (newValues.ToArray(), newData.ToArray());
			}
			catch (Exception e)
			{
				Logger.LogError($"Load - Error occured during load");
				Logger.LogError(e);

				return (values, data);
			}
		});

		cursor.Emit(OpCodes.Dup);
		cursor.EmitDelegate<Func<(int[], int[]), int[]>>(tuple => tuple.Item1);
		cursor.Emit(OpCodes.Stloc, valuesLoc);
		cursor.EmitDelegate<Func<(int[], int[]), int[]>>(tuple => tuple.Item2);
		cursor.Emit(OpCodes.Stloc, dataLoc);
	}
}