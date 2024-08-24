using Mono.Cecil.Cil;
using MonoMod.Cil;
using SmartItemSaving.Fixes;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SmartItemSaving;

public class Patches
{
	public static void Initialize()
	{
		// Saving
		IL.GameNetworkManager.SaveGameValues += GameNetworkManager_SaveGameValues;
		IL.GameNetworkManager.SaveItemsInShip += GameNetworkManager_SaveItemsInShip;

		// Loading
		On.StartOfRound.SetTimeAndPlanetToSavedSettings += StartOfRound_SetTimeAndPlanetToSavedSettings;
		IL.StartOfRound.LoadUnlockables += StartOfRound_LoadUnlockables;
		IL.StartOfRound.LoadShipGrabbableItems += StartOfRound_LoadShipGrabbableItems;

		// Misc
		On.GrabbableObject.Start += GrabbableObject_Start;
		On.GrabbableObject.Update += GrabbableObject_Update;
		On.GameNetcodeStuff.PlayerControllerB.ConnectClientToPlayerObject += PlayerControllerB_ConnectClientToPlayerObject;
		IL.GameNetcodeStuff.PlayerControllerB.ThrowObjectClientRpc += PlayerControllerB_ThrowObjectClientRpc;
	}

	private static void GameNetworkManager_SaveGameValues(ILContext il)
	{
		var cursor = new ILCursor(il);

		var unlockListLoc = -1;

		if (!cursor.TryGotoNext(MoveType.Before,
				instr1 => instr1.MatchLdstr("UnlockedShipObjects"),
				instr2 => instr2.MatchLdloc(out unlockListLoc)))
		{
			Plugin.Logger.LogError($"Failed IL hook for GameNetworkManager.SaveGameValues @ Save unlocked ship objects list");
			return;
		}

		var beforeSaveUnlocksInstr = cursor.Next;

		if (!cursor.TryGotoPrev(MoveType.After, instr => instr.MatchStloc(unlockListLoc)))
		{
			Plugin.Logger.LogError($"Failed IL hook for GameNetworkManager.SaveGameValues @ After initialize unlocks list");
			return;
		}

		// diff:
		//		...
		//		StartOfRound startOfRound = Object.FindObjectOfType<StartOfRound>();
		//		if (startOfRound != null)
		//		{
		//			...
		//			List<int> unlocks = new List<int>();
		//	+		General.SaveInitialValues(this, startOfRound);
		//			...

		cursor.MoveAfterLabels();
		cursor.Emit(OpCodes.Ldarg_0);
		cursor.EmitDelegate(General.SaveInitialValues);

		//diff:
		//		...
		//		if (unlocks.Count > 0)
		//		{
		//	+		FixUnlockIds.Save(this, startOfRound, unlocks);
		//			ES3.Save<int[]>("UnlockedShipObjects", unlocks.ToArray(), this.currentSaveFileName);
		//		}
		//		ES3.Save<int>("DeadlineTime", (int)Mathf.Clamp(timeOfDay.timeUntilDeadline, 0f, 99999f), this.currentSaveFileName);
		//		...

		cursor.Goto(beforeSaveUnlocksInstr, MoveType.AfterLabel);

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.Emit(OpCodes.Ldloc, unlockListLoc);
		cursor.EmitDelegate(FixUnlockIds.SaveFixUnlockIds);
	}

	private static void GameNetworkManager_SaveItemsInShip(ILContext il)
	{
		var cursor = new ILCursor(il);

		if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallOrCallvirt<Object>("FindObjectsByType")))
		{
			Plugin.Logger.LogError("Failed IL hook for GameNetworkManager.SaveItemsInShip @ Find grabbable objects");
			return;
		}

		var grabbableObjectsLoc = -1;

		if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchStloc(out grabbableObjectsLoc)))
		{
			Plugin.Logger.LogError("Failed IL hook for GameNetworkManager.SaveItemsInShip @ Get grabbable objects local");
			return;
		}

		if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<ES3>("DeleteKey")))
		{
			Plugin.Logger.LogError("Failed IL hook for GameNetworkManager.SaveItemsInShip @ Delete empty keys");
			return;
		}

		// diff:
		//		...
		//		GrabbableObject[] array = Object.FindObjectsByType<GrabbableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		//		if (array == null || array.Length == 0)
		//		{
		//	+		General.DeleteItemKeys(this);
		//			ES3.DeleteKey("shipGrabbableItemIDs", this.currentSaveFileName);
		//			...

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.EmitDelegate(General.DeleteItemKeys);

		var listLocs = new int[4];

		for (var i = 0; i < listLocs.Length; i++)
		{
			var loc = -1;

			if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchStloc(out loc)))
			{
				Plugin.Logger.LogError($"Failed IL hook for GameNetworkManager.SaveItemsInShip @ Get list {i} local");
				return;
			}

			listLocs[i] = loc;
		}

		var idsLoc = listLocs[0];
		var posLoc = listLocs[1];
		var valuesLoc = listLocs[2];
		var dataLoc = listLocs[3];

		var iLoc = -1;

		if (!cursor.TryGotoNext(MoveType.Before,
				instr1 => instr1.MatchLdcI4(0),
				instr2 => instr2.MatchStloc(out iLoc),
				instr3 => instr3.MatchBr(out _)))
		{
			Plugin.Logger.LogError("Failed IL hook for GameNetworkManager.SaveItemsInShip @ Get loop 'i' local");
			return;
		}

		// diff:
		//		...
		//		List<int> data = new List<int>();
		//	+	FixItemRotation.PreSave();
		//		int i = 0;
		//		for (int i = 0; i < array.Length && i <= StartOfRound.Instance.maxShipItemCapacity; i++)
		//		{
		//			...

		cursor.EmitDelegate(FixItemRotation.PreSave);

		if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchLdloc(posLoc)))
		{
			Plugin.Logger.LogError("Failed IL hook for GameNetworkManager.SaveItemsInShip @ Before add item position");
			return;
		}

		// diff:
		//		...
		//		if (StartOfRound.Instance.allItemsList.itemsList[j] == grabbableObjects[i].itemProperties)
		//		{
		//			ids.Add(i);
		//	+		FixItemRotation.Save(grabbableObjects, i);
		//	-		positions.Add(grabbableObjects[i].transform.position);
		//	+		positions.Add(FixItemFalling.SaveFixItemFalling(grabbableObjects[i].transform.position, grabbableObjects, i));
		//			break;
		//		}
		//		...

		cursor.Emit(OpCodes.Ldloc, grabbableObjectsLoc);
		cursor.Emit(OpCodes.Ldloc, iLoc);
		cursor.EmitDelegate(FixItemRotation.Save);

		if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<Transform>("get_position")))
		{
			Plugin.Logger.LogError("Failed IL hook for GameNetworkManager.SaveItemsInShip @ Get item position");
			return;
		}

		cursor.Emit(OpCodes.Ldloc, grabbableObjectsLoc);
		cursor.Emit(OpCodes.Ldloc, iLoc);
		cursor.EmitDelegate(FixItemFalling.Save);

		if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<ES3>("Save")))
		{
			Plugin.Logger.LogError("Failed IL hook for GameNetworkManager.SaveItemsInShip @ First save call");
			return;
		}

		// diff:
		//		...
		//		ES3.Save<Vector3[]>("shipGrabbableItemPos", positions.ToArray(), this.currentSaveFileName);
		//	+	MyDelegate(this, ids);
		//		ES3.Save<int[]>("shipGrabbableItemIDs", ids.ToArray(), this.currentSaveFileName);
		//		...

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.Emit(OpCodes.Ldloc, idsLoc);
		cursor.EmitDelegate<Action<GameNetworkManager, List<int>>>((self, ids) =>
		{
			FixItemRotation.PostSave(self);
			FixItemIds.Save(self, ids);
		});
	}

	private static void StartOfRound_SetTimeAndPlanetToSavedSettings(On.StartOfRound.orig_SetTimeAndPlanetToSavedSettings orig, StartOfRound self)
	{
		General.LoadCreateBackup();

		orig(self);

		General.LoadInitialValues(self);
	}

	private static void StartOfRound_LoadUnlockables(ILContext il)
	{
		var cursor = new ILCursor(il);

		if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<ES3>("Load")))
		{
			Plugin.Logger.LogError($"Failed IL hook for StartOfRound.LoadUnlockables @ Load unlocks list");
			return;
		}

		//diff:
		//		if (ES3.KeyExists("UnlockedShipObjects", GameNetworkManager.Instance.currentSaveFileName))
		//		{
		//	-		int[] array = ES3.Load<int[]>("UnlockedShipObjects", GameNetworkManager.Instance.currentSaveFileName);
		//	+		int[] array = MyDelegate(ES3.Load<int[]>("UnlockedShipObjects", GameNetworkManager.Instance.currentSaveFileName), this);
		//			for (int i = 0; i < array.Length; i++)
		//			{h

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.EmitDelegate<Func<int[], StartOfRound, int[]>>((unlocks, self) =>
		{
			try
			{
				FixUnlockIds.LoadFixUnlockIds(self, ref unlocks);
			}
			catch (Exception e)
			{
                Plugin.Logger.LogError($"Load | Unlocks | Error occured during load");
                Plugin.Logger.LogError(e);
			}

			return unlocks;
		});
	}

	private static void StartOfRound_LoadShipGrabbableItems(ILContext il)
	{
		var cursor = new ILCursor(il);

		var idsLoc = -1;

		if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchLdstr("shipGrabbableItemIDs")) ||
			!cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchStloc(out idsLoc)))
		{
			Plugin.Logger.LogError("Failed IL hook for StartOfRound.LoadShipGrabbableItems @ Get item IDs local");
			return;
		}

		if (!cursor.TryGotoNext(MoveType.Before,
				instr1 => instr1.MatchBrfalse(out _),
				instr2 => instr2.MatchLdstr("shipScrapValues")))
		{
			Plugin.Logger.LogError("Failed IL hook for StartOfRound.LoadShipGrabbableItems @ Load values");
			return;
		}

		var valuesLoc = -1;

		if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchStloc(out valuesLoc)))
		{
			Plugin.Logger.LogError("Failed IL hook for StartOfRound.LoadShipGrabbableItems @ Get item values local");
			return;
		}

		var dataLoc = 1;

		if (!cursor.TryGotoNext(MoveType.After,
				instr1 => instr1.MatchCallOrCallvirt<ES3>("Load"),
				instr2 => instr2.MatchStloc(out dataLoc)))
		{
			Plugin.Logger.LogError("Failed IL hook for StartOfRound.LoadShipGrabbableItems @ Get item data local");
			return;
		}

		// diff:
		//		...
		//		if (ES3.KeyExists("shipItemSaveData", GameNetworkManager.Instance.currentSaveFileName))
		//		{
		//			hasData = true;
		//			data = ES3.Load<int[]>("shipItemSaveData", GameNetworkManager.Instance.currentSaveFileName);
		//		}
		//	+	(int[] newValues, int[] newData) = MyDelegate(this, ids, values, data);
		//	+	values = newValues;
		//	+	data = newData;
		//		int valueIndex = 0;
		//		int dataIndex = 0;
		//		...

		cursor.MoveAfterLabels();

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.Emit(OpCodes.Ldloc, idsLoc);
		cursor.Emit(OpCodes.Ldloc, valuesLoc);
		cursor.Emit(OpCodes.Ldloc, dataLoc);
		cursor.EmitDelegate<Func<StartOfRound, int[], int[], int[], (int[], int[])>>((self, ids, values, data) =>
		{
			try
			{
				FixItemFalling.PreLoad();
				FixItemRotation.PreLoad(ids);
				FixItemIds.Load(self, ids, ref values, ref data);
			}
			catch (Exception e)
			{
                Plugin.Logger.LogError($"Load | Items | Error occured during pre-load");
                Plugin.Logger.LogError(e);
			}

			return (values, data);
		});

		cursor.Emit(OpCodes.Dup);
		cursor.EmitDelegate<Func<(int[], int[]), int[]>>(tuple => tuple.Item1);
		cursor.Emit(OpCodes.Stloc, valuesLoc);
		cursor.EmitDelegate<Func<(int[], int[]), int[]>>(tuple => tuple.Item2);
		cursor.Emit(OpCodes.Stloc, dataLoc);

		var iLoc = -1;

		if (!cursor.TryGotoNext(MoveType.After,
				instr1 => instr1.MatchLdcI4(0),
				instr2 => instr2.MatchStloc(out iLoc),
				instr3 => instr3.MatchBr(out _)))
		{
			Plugin.Logger.LogError("Failed IL hook for StartOfRound.LoadShipGrabbableItems @ Get loop 'i' local");
			return;
		}

		if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallOrCallvirt<GameObject>("GetComponent")))
		{
			Plugin.Logger.LogError("Failed IL hook for StartOfRound.LoadShipGrabbableItems @ Grabbable object instantiation");
			return;
		}

		var grabbableObjectLoc = -1;

		if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchStloc(out grabbableObjectLoc)))
		{
			Plugin.Logger.LogError("Failed IL hook for StartOfRound.LoadShipGrabbableItems @ Get grabbable object local");
			return;
		}

		// diff:
		//		...
		//		GrabbableObject grabbableObject = Object.Instantiate<GameObject>(this.allItemsList.itemsList[array[i]].spawnPrefab, array2[i], Quaternion.identity, this.elevatorTransform).GetComponent<GrabbableObject>();
		//	+	MyDelegate(i, grabbableObject);
		//		grabbableObject.fallTime = 1f;
		//		...

		cursor.Emit(OpCodes.Ldloc, iLoc);
		cursor.Emit(OpCodes.Ldloc, grabbableObjectLoc);
		cursor.EmitDelegate<Action<int, GrabbableObject>>((i, grabbableObject) =>
		{
			try
			{
				FixItemFalling.Load(grabbableObject);
				FixItemRotation.Load(i, grabbableObject);
			}
			catch (Exception e)
			{
                Plugin.Logger.LogError($"Load | Items | Error occured during load");
                Plugin.Logger.LogError(e);
			}
		});
	}

	private static void GrabbableObject_Update(On.GrabbableObject.orig_Update orig, GrabbableObject self)
	{
		orig(self);

		FixItemRotation.Apply(self);
	}

	private static void GrabbableObject_Start(On.GrabbableObject.orig_Start orig, GrabbableObject self)
	{
		orig(self);

		FixItemFalling.Apply(self);
	}

	private static void PlayerControllerB_ConnectClientToPlayerObject(On.GameNetcodeStuff.PlayerControllerB.orig_ConnectClientToPlayerObject orig, GameNetcodeStuff.PlayerControllerB self)
	{
		orig(self);

		BetterSyncItems.InitializeNetworkingAndSync();
	}

	private static void PlayerControllerB_ThrowObjectClientRpc(ILContext il)
	{
		var cursor = new ILCursor(il);

		if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcI4(-1)))
		{
			Plugin.Logger.LogWarning($"Failed IL hook for PlayerControllerB.ThrowObjectClientRpc @ Pass -1 (Not a big deal)");
			return;
		}

		cursor.Emit(OpCodes.Ldarg, 5);
		cursor.EmitDelegate<Func<int, int, int>>((orig, floorYRot) =>
		{
			if (!Config.BetterSyncItems.Value)
			{
				return orig;
			}

			return floorYRot;
		});
	}
}
