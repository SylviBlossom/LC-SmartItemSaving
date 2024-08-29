# SmartItemSaving
Improves how items save, attempting to prevent updating/removing/adding mods from replacing other modded items on the ship, and making items correctly save their positions and rotations.

**This requires you to both save and load using this mod!** If you plan to install this mod to do modpack changes, first do a launch and save with this mod installed *before* any modpack changes.

For fixing items falling through the floor and item rotation saving, all clients must have the mod for them to appear the same when joining.

If you encounter any problems please report the bug either to **sylviblossom** on the Lethal Company Modding discord or [open an issue](https://github.com/SylviBlossom/LC-SmartItemSaving/issues/new).

See also [SaveItemRotations](https://thunderstore.io/c/lethal-company/p/SylviBlossom/SaveItemRotations/) for just the item rotation saving feature of this mod.

### Current features
- Prevent corruption of items in ship when items are added/removed by mods
- Prevent corruption of ship unlockables/furniture too
- Prevent items falling through the floor on load
- Save item rotation
- Create backup when save is loaded (only one)

### Planned features
- Resolve name conflicts for ship unlockables
- *Maybe* Increase item save limit to 999 (configurable)

## Mod Compatibility

### Compatible
The following mods share a feature of this mod and have been tested to be compatible:

- <details>
  <summary>LethalLevelLoader</summary>

  - Contains a similar item ID fixing system which has additional checks with items registered for LethalLevelLoader itself.
  - This mod disables its own item ID fixing when LethalLevelLoader is detected, unless `ForceHandleFixItemIds` is enabled.
</details>

- <details>
  <summary>SaveItemRotations</summary>

  - A separate smaller mod by me which includes just the system for saving item rotations and syncing them.
  - This mod disables its own item rotation saving when SaveItemRotations is detected, unless `ForceHandleSaveItemRotation` is enabled.
</details>

### Incompatible
The following mods are known to conflict with a feature of this mod:

- <details>
  <summary>Remnants</summary>

  - Completely replaces how item saving works, making it incompatible with this mod's item ID fixing and potentially other features.
</details>

### Unknown Compatibility
The following mods or mod config options may conflict with a feature of this mod:

- <details>
  <summary>CupboardFix</summary>

  - Does the same thing as this mod's `FixItemFalling` option.
  - Compatibility not tested, but may conflict with this mod's `SaveItemRotation` option.
</details>

- <details>
  <summary>GeneralImprovements (config options)</summary>

  - `FixItemsFallingThrough` option
    - Does the same thing as this mod's `FixItemFalling` option.
    - Compatibility not tested, but should be fine.
  - `FixItemsLoadingSameRotation` option
    - Does the same thing as this mod's `SaveItemRotation` option.
    - Compatibility not tested, but should be fine.
</details>

- <details>
  <summary>LethalLib (config options)</summary>

  - `EnableItemSaveFix` option
    - Does a similar thing as this mod's `FixItemIds` option.
    - Compatible, but redundant.
    - May cause save corruption?
</details>