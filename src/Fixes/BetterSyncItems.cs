using System;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SmartItemSaving.Fixes;

public static class BetterSyncItems
{
	// Yes copied from config syncing bc im lazy

	internal static void RequestSync()
	{
		if (!NetworkManager.Singleton.IsClient) return;

		using FastBufferWriter stream = new(4, Allocator.Temp);

		// Method `OnRequestSync` will then get called on host.
		NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage($"{PluginInfo.PLUGIN_GUID}_OnRequestItemSync", 0uL, stream);
	}

	internal static void OnRequestSync(ulong clientId, FastBufferReader _)
	{
		if (!NetworkManager.Singleton.IsHost) return;

		SendSyncTo(clientId);
	}

	internal static void SendSyncTo(ulong clientId)
	{
		if (!NetworkManager.Singleton.IsHost) return;

		var floorObjects = Object.FindObjectsOfType<GrabbableObject>().Where(IsValidObject).ToArray();
		var maxSavedObjects = Math.Min(floorObjects.Length, 999);

		using FastBufferWriter stream = new(4 + 4 + 32 * maxSavedObjects, Allocator.Temp, 65536);

		try
		{
			stream.WriteValueSafe(maxSavedObjects);
			for (var i = 0; i < maxSavedObjects; i++)
			{
				stream.WriteValueSafe<NetworkObjectReference>(floorObjects[i].NetworkObject);
				stream.WriteValueSafe(floorObjects[i].transform.position);
				stream.WriteValueSafe(floorObjects[i].transform.eulerAngles);
			}

			Plugin.Logger.LogInfo($"Item rotation sync: Sending packet\n- Buffer size: {stream.Capacity}\n- Bytes written: {stream.Length}\n- Objects synced: {maxSavedObjects}");

			NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage($"{PluginInfo.PLUGIN_GUID}_OnReceiveItemSync", clientId, stream);
		}
		catch (Exception e)
		{
			Plugin.Logger.LogError($"Error occured syncing item rotations with client: {clientId}\n{e}");
		}
	}

	internal static void OnReceiveSync(ulong _, FastBufferReader reader)
	{
		try
		{
			if (!reader.TryBeginRead(4))
			{
				Plugin.Logger.LogError("Item rotation sync error: Could not begin reading buffer");
				return;
			}

			reader.ReadValueSafe(out int length, default);

			if (!reader.TryBeginRead(32 * length))
			{
				Plugin.Logger.LogError("Item rotation sync error: Invalid buffer size");
				return;
			}

			for (var i = 0; i < length; i++)
			{
				reader.ReadValueSafe(out NetworkObjectReference objectRef);
				reader.ReadValueSafe(out Vector3 objectPosition);
				reader.ReadValueSafe(out Vector3 objectRotation);

				if (!objectRef.TryGet(out var networkObject))
				{
					Plugin.Logger.LogWarning($"Item rotation sync: Unknown object reference {objectRef.NetworkObjectId}");
					continue;
				}

				var grabbableObject = networkObject.gameObject.GetComponent<GrabbableObject>();
				if (grabbableObject != null && IsValidObject(grabbableObject))
				{
					ApplyPositionTo(grabbableObject, objectPosition);
					ApplyRotationTo(grabbableObject, objectRotation);
				}
			}
		}
		catch (Exception e)
		{
			Plugin.Logger.LogError($"Error occured receiving item rotation sync!\n{e}");
		}
	}

	public static void InitializeNetworkingAndSync()
	{
		if (!Config.BetterSyncItems.Value)
		{
			return;
		}

		if (NetworkManager.Singleton.IsHost)
		{
			NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler($"{PluginInfo.PLUGIN_GUID}_OnRequestItemSync", OnRequestSync);
			return;
		}

		NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler($"{PluginInfo.PLUGIN_GUID}_OnReceiveItemSync", OnReceiveSync);
		RequestSync();
	}

	private static bool IsValidObject(GrabbableObject grabbableObject)
	{
		return !grabbableObject.isHeld && grabbableObject.parentObject == null && grabbableObject.reachedFloorTarget;
	}

	private static void ApplyPositionTo(GrabbableObject grabbableObject, Vector3 position)
	{
		grabbableObject.fallTime = 1f;
		grabbableObject.hasHitGround = true;
		grabbableObject.transform.position = position;
		grabbableObject.targetFloorPosition = grabbableObject.transform.localPosition;
	}

	private static void ApplyRotationTo(GrabbableObject grabbableObject, Vector3 eulerAngles)
	{
		grabbableObject.floorYRot = -1;
		grabbableObject.transform.rotation = Quaternion.Euler(grabbableObject.transform.eulerAngles.x, eulerAngles.y, grabbableObject.transform.eulerAngles.z);
	}
}
