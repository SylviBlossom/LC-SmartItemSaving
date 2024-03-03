using System.Collections.Generic;
using UnityEngine;

namespace SmartItemSaving.Fixes;

public static class FixItemFalling
{
	// Temporary variables
	public static HashSet<GrabbableObject> NeedsFixItemFalling = new();

	public static Vector3 Save(Vector3 position, GrabbableObject[] grabbableObjects, int i)
	{
		var grabbableObject = grabbableObjects[i];

		if (grabbableObject.isHeld || grabbableObject.parentObject != null)
		{
			return grabbableObject.GetItemFloorPosition();
		}

		if (!grabbableObject.reachedFloorTarget)
		{
			var newPosition = grabbableObject.targetFloorPosition;

			if (grabbableObject.transform.parent != null)
			{
				newPosition = grabbableObject.transform.parent.TransformPoint(newPosition);
			}

			return newPosition;
		}

		return position;
	}

	public static void PreLoad()
	{
		NeedsFixItemFalling = new();
	}

	public static void Load(GrabbableObject grabbableObject)
	{
		// Skip if disabled
		if (!Config.FixItemFalling.Value)
		{
			return;
		}

		NeedsFixItemFalling.Add(grabbableObject);
	}

	public static void Apply(GrabbableObject grabbableObject)
	{
		if (!StartOfRound.Instance.IsServer)
		{
			return;
		}

		if (!NeedsFixItemFalling.Contains(grabbableObject))
		{
			return;
		}

		if (grabbableObject.itemProperties.itemSpawnsOnGround)
		{
			grabbableObject.fallTime = 1f;
			grabbableObject.hasHitGround = true;
			grabbableObject.targetFloorPosition = grabbableObject.transform.localPosition;
		}
	}
}
