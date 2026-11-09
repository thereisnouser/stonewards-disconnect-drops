# Changelog

## 0.2.1 — Disconnect Drops

- Renamed the project, assembly, BepInEx plugin ID, configuration file and install directory to **Disconnect Drops**.
- Installer cleans up the previous Leave Loot Drop DLL and copies its configuration if needed.
- No intentional gameplay changes.

## 0.2.0 — Leave Loot Drop

- Renamed the plugin, project, assembly, build script and configuration from Leave Tome Drop to Leave Loot Drop.
- Added drop of all droppable inventory entries including equipped slots and stacks.
- Saved inventory is updated only after successful networked spawning; rollback on removal failure.
- Inventory drops even if the player has no consumed tomes.
- Added separate `DropTomes` and `DropInventory` toggles.
- Installer cleans up the previous DLL to avoid double-loaded mods.

## 0.1.0 — Leave Tome Drop

- Initial host-side drop of consumed pickup tomes on disconnect.
