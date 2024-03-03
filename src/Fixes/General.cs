namespace SmartItemSaving.Fixes;

public static class General
{
	// Temporary variables
	public static int LoadedFormatVersion = 0;
	public static bool LoadedParityCheck = false;

	public static void SaveInitialValues(GameNetworkManager gameNetworkManager, StartOfRound startOfRound)
	{
		ES3.Save(SaveKeys.FormatVersion, Plugin.FormatVersion, gameNetworkManager.currentSaveFileName);
		ES3.Save(SaveKeys.ParityStepsTaken, startOfRound.gameStats.allStepsTaken, gameNetworkManager.currentSaveFileName);
	}

	public static void LoadInitialValues(StartOfRound startOfRound)
	{
		var currentSaveFileName = GameNetworkManager.Instance.currentSaveFileName;

		if (!ES3.KeyExists(SaveKeys.FormatVersion, currentSaveFileName))
		{
			LoadedFormatVersion = 0;
			LoadedParityCheck = false;

			Plugin.Logger.LogWarning($"Load | General | No {PluginInfo.PLUGIN_NAME} save data found, skipping all");
			return;
		}

		LoadedFormatVersion = ES3.Load(SaveKeys.FormatVersion, currentSaveFileName, Plugin.FormatVersion);
		var loadedStepsTaken = ES3.Load(SaveKeys.ParityStepsTaken, currentSaveFileName, startOfRound.gameStats.allStepsTaken);

		if (loadedStepsTaken != startOfRound.gameStats.allStepsTaken)
		{
			LoadedParityCheck = false;

			Plugin.Logger.LogWarning($"Load | General | Steps Taken mismatch (Expected {loadedStepsTaken}, got {startOfRound.gameStats.allStepsTaken}), likely outdated save, skipping all");
			return;
		}

		LoadedParityCheck = true;
	}

	public static void LoadCreateBackup()
	{
		var currentSaveFileName = GameNetworkManager.Instance.currentSaveFileName;

		if (Config.BackupOnLoad.Value && ES3.FileExists(currentSaveFileName))
		{
			Plugin.Logger.LogInfo("Load | General | Creating save backup");

			ES3.CreateBackup(currentSaveFileName);
		}
	}

	public static void DeleteItemKeys(GameNetworkManager gameNetworkManager)
	{
		ES3.DeleteKey(SaveKeys.ItemNames, gameNetworkManager.currentSaveFileName);
		ES3.DeleteKey(SaveKeys.ItemHasValue, gameNetworkManager.currentSaveFileName);
		ES3.DeleteKey(SaveKeys.ItemHasData, gameNetworkManager.currentSaveFileName);
		ES3.DeleteKey(SaveKeys.ItemRotations, gameNetworkManager.currentSaveFileName);
	}
}
