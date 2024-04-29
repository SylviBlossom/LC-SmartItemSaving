# SmartItemSaving
Improves how items save, attempting to prevent updating/removing/adding mods from replacing other modded items on the ship, and making items correctly save their positions and rotations.

**This requires you to both save and load using this mod!** If you plan to install this mod to do modpack changes, first do a launch and save with this mod installed *before* any modpack changes.

For fixing items falling through the floor and item rotation saving, all clients must have the mod for them to appear the same when joining.

If you encounter any problems please report the bug either on discord or [open an issue](https://github.com/SylviBlossom/LC-SmartItemSaving/issues/new).

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
The following mods or mod config options either have compatibility issues or do the same thing as a feature from this mod:

- [Remnants](https://thunderstore.io/c/lethal-company/p/KawaiiBone/Remnants/)
  - Completely replaces how item saving works, making it incompatible with this mod and others.
- [CupboardFix](https://thunderstore.io/c/lethal-company/p/Rocksnotch/CupboardFix/)
  - Does the same thing as this mod's "FixItemFalling" option.
  - Compatibility not tested, but may conflict with this mod's "SaveItemRotation" option.
- [GeneralImprovements's](https://thunderstore.io/c/lethal-company/p/ShaosilGaming/GeneralImprovements/) `FixItemsFallingThrough` option
  - Does the same thing as this mod's "FixItemFalling" option.
  - Compatibility not tested, but should be fine.
- [LethalLib's](https://thunderstore.io/c/lethal-company/p/Evaisa/LethalLib/) `EnableItemSaveFix` option
  - Does a similar thing as this mod's "FixItemIds" option.
  - Compatible, but redundant.
  - May cause save corruption?