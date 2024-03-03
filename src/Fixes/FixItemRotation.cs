using System.Collections.Generic;
using UnityEngine;

namespace SmartItemSaving.Fixes;

public static class FixItemRotation
{
	// Temporary variables
	public static Vector3[] LoadedItemRotations;
	public static Dictionary<GrabbableObject, Vector3> NeedsItemRotation = new();
	public static List<Vector3> SavedItemRotations;

	public static void PreSave()
	{
		SavedItemRotations = new();
	}

	public static void Save(GrabbableObject[] grabbableObjects, int i)
	{
		var grabbableObject = grabbableObjects[i];

		SavedItemRotations.Add(grabbableObject.transform.eulerAngles);
	}

	public static void PostSave(GameNetworkManager gameNetworkManager)
	{
		if (SavedItemRotations == null)
		{
			return;
		}

		ES3.Save(SaveKeys.ItemRotations, SavedItemRotations.ToArray(), gameNetworkManager.currentSaveFileName);
	}

	public static void PreLoad(int[] ids)
	{
		// Reset variable to null if loading fails
		LoadedItemRotations = null;
		NeedsItemRotation = new();

		var currentSaveFileName = GameNetworkManager.Instance.currentSaveFileName;

		// Skip if parity check failed
		if (!General.LoadedParityCheck)
		{
			return;
		}

		// Skip if disabled
		if (!Config.SaveItemRotation.Value)
		{
			Plugin.Logger.LogInfo("Load | Items | SaveItemRotation is disabled, skipping load item rotation");
			return;
		}

		// Make sure modded values exist
		if (!ES3.KeyExists(SaveKeys.ItemRotations, currentSaveFileName))
		{
			Plugin.Logger.LogWarning($"Load | Items | No item rotation save data found, skipping load item rotation");
			return;
		}

		// Load values for item rotations
		LoadedItemRotations = ES3.Load<Vector3[]>(SaveKeys.ItemRotations, currentSaveFileName);

		// Make sure our lists are the same size
		if (LoadedItemRotations.Length != ids.Length)
		{
			Plugin.Logger.LogError($"Load | Items | Item count mismatch (Expected {LoadedItemRotations.Length}, got {ids.Length}), likely outdated save, skipping load item rotation");

			LoadedItemRotations = null;
			return;
		}
	}

	public static void Load(int i, GrabbableObject grabbableObject)
	{
		// Skip if preload failed
		if (LoadedItemRotations == null)
		{
			return;
		}

		// Make sure our index is in-bounds (it always should be, unless some other mod tampers with item loading here)
		if (i >= LoadedItemRotations.Length)
		{
			Plugin.Logger.LogError($"Load | Items | Item index outside bounds of saved rotations, this shouldn't happen");
			return;
		}

		// Mark this item as needing to be rotated later in Update (after initial position/rotation update)
		NeedsItemRotation.Add(grabbableObject, LoadedItemRotations[i]);

		// Though we do this later, also do this now to sync values between clients
		ApplyRotationTo(grabbableObject, LoadedItemRotations[i]);
	}

	public static void Apply(GrabbableObject grabbableObject)
	{
		if (!grabbableObject.IsServer)
		{
			return;
		}

		if (!NeedsItemRotation.TryGetValue(grabbableObject, out var rotation))
		{
			return;
		}

		ApplyRotationTo(grabbableObject, rotation);

		NeedsItemRotation.Remove(grabbableObject);
	}

	private static void ApplyRotationTo(GrabbableObject grabbableObject, Vector3 eulerAngles)
	{
		grabbableObject.floorYRot = -1;
		grabbableObject.transform.rotation = Quaternion.Euler(grabbableObject.transform.eulerAngles.x, eulerAngles.y, grabbableObject.transform.eulerAngles.z);
	}
}
