# Changelog

## 0.1.0 — First release

- **Disconnect drops:** drop consumed pickup tomes when a player leaves, gets kicked, or disconnects during a level.
- **Inventory recovery:** drop supported items from inventory slots, including equipped items and stacks, even when the player has no consumed tomes.
- **Reconnect protection:** remove successfully dropped entries from saved run data to reduce duplication.
- **Host controls:** add `Enabled`, `DropTomes`, `DropInventory`, and `ScatterRadius` settings.
- **Package:** add Thunderstore manifest, icon, dedicated README and validated release packaging script.

**Testing note:** Tome drops have been observed working. Complete multiplayer testing of inventory drops, kick handling and reconnect protection remains outstanding.
