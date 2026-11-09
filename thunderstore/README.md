# Stonewards Disconnect Drops

**Drops a departing player's consumed tomes and inventory so teammates can recover the loot** when someone leaves, gets kicked, or disconnects.

## ✨ Features

- **Consumed tomes** — drops acquired pickup tomes, including duplicates. Level-up rings remain untouched.
- **Inventory recovery** — drops supported items from inventory slots, including equipped items and stacks.
- **Shared loot** — items appear near the departing player for teammates to pick up.
- **Reconnect protection** — successfully dropped items are removed from the departing player's saved inventory or upgrade record.

## 🎮 How to use

Install the mod on the **host**. During a level, it works automatically when another player leaves, is kicked, or loses their connection. No hotkeys are required. Nothing is dropped in the Hub.

## ⚙️ Configuration

Settings are stored in `BepInEx/config/com.thereisnouser.stonewards.disconnectdrops.cfg`:

- `Enabled = true` — enable or disable the mod.
- `DropTomes = true` — drop consumed pickup tomes.
- `DropInventory = true` — drop supported inventory items.
- `ScatterRadius = 1.6` — scatter radius in metres (range: `0.25`–`4.0`).

All settings are in the `General` section.

## 📦 Installation

Install using **r2modman** or another compatible Thunderstore mod manager on the host and launch Stonewards through that profile. BepInExPack is declared as a dependency.

## 🔧 Compatibility

- **Game:** Stonewards. The supported build range has not yet been verified.
- **Mod loader:** BepInEx 5 (Mono).
- **Multiplayer:** Designed for host-only installation.
- **Version:** `0.1.0`. Tome drops have been observed working; inventory-drop behavior still requires complete multiplayer testing.

## ⚠️ Known limitations

- Items without a standard pickup prefab remain in the player's saved inventory.
- Special item state, such as charges or durability, may not be preserved in dropped pickups.
- Team-wide tome effects might remain applied and stack after a dropped tome is collected.
- Objects carried outside the inventory system are not included.
- Ground loot cannot persist after the host closes the session.

## 🔗 Links

[GitHub](https://github.com/thereisnouser/stonewards-disconnect-drops) · [Report an issue](https://github.com/thereisnouser/stonewards-disconnect-drops/issues) · [Changelog](https://github.com/thereisnouser/stonewards-disconnect-drops/blob/main/CHANGELOG.md)

---

*Made by thereisnouser*
