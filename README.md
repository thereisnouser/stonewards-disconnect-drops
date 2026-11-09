# Stonewards Disconnect Drops

**Automatically drops a departing player's consumed tomes and inventory so the rest of the party can recover their loot.** The mod runs on the host when a player leaves, is kicked, or loses their connection.

## ✨ Features

- **Consumed tome drops** — returns acquired pickup tomes to the world, including repeated upgrades. Level-up rings are not dropped.
- **Inventory drops** — spawns standard pickup items from all inventory slots, including equipped items and stacks.
- **Party recovery** — places networked loot near the disconnected player's last position for remaining players to collect.
- **Reconnect protection** — removes successfully dropped items from the departing player's saved run data to avoid restoring them again on reconnect.
- **Host-side controls** — enable or disable tome and inventory drops independently and adjust their scatter radius.

## 🎮 How to use

Install the mod in the **host's** BepInEx profile and start a run. No buttons or in-game menu are needed.

When another player leaves, gets kicked, or disconnects during a level, the host attempts to drop their consumed pickup tomes and inventory items near where they were standing. The mod does not drop loot in the Hub.

## ⚙️ Configuration

BepInEx creates the configuration after the plugin loads:

`BepInEx/config/com.thereisnouser.stonewards.disconnectdrops.cfg`

| Setting | Default | Description |
| --- | --- | --- |
| `General.Enabled` | `true` | Enables disconnect drops. |
| `General.DropTomes` | `true` | Drops consumed pickup tomes, not level-up rings. |
| `General.DropInventory` | `true` | Drops supported items from the player's inventory. |
| `General.ScatterRadius` | `1.6` | Loot scatter radius in metres (allowed: `0.25`–`4.0`). |

## 📦 Installation

### Mod manager

For the published package, install **Disconnect Drops** on the host using **r2modman** or another compatible Thunderstore mod manager, then launch Stonewards through that profile. Until the package is live, use **Build from source** below.

### Build from source

To build locally, you need the **.NET SDK**, an installed copy of **Stonewards**, and a **BepInEx 5** profile.

Clone the repository, open PowerShell in its root and specify your own paths:

```powershell
$gameDir = "C:\Games\Stonewards"
$profileDir = "C:\ModManager\Stonewards\profiles\MyHostProfile"

powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
    -GameDir $gameDir `
    -ProfileDir $profileDir
```

The script compiles `StonewardsDisconnectDrops.dll` and installs it into:

`BepInEx/plugins/thereisnouser-DisconnectDrops/`

It also removes old pre-release **Leave Tome Drop / Leave Loot Drop** plugin DLLs from the selected profile after a successful build.

## 🔧 Compatibility

- **Game:** Stonewards. A supported game build range has not been established.
- **Mod loader:** BepInEx 5 (Mono).
- **Multiplayer:** Host-side mod; designed to require installation on the host only.
- **Version:** `0.1.0` (first release). Tome drops have been observed working; expanded inventory drops still need a complete multiplayer test.

## ⚠️ Known limitations

- **Unsupported inventory items** — items without a standard pickup prefab are kept in the saved inventory instead of being deleted.
- **Item state** — special state such as charges or durability may not survive recreation as a world pickup.
- **Team-wide upgrades** — effects granted by a tome may remain active after the player disconnects; collecting the dropped tome could apply them again.
- **Other carried objects** — objects being carried outside the inventory system are not included.
- **Closed sessions** — drops cannot remain in the world after the host ends the session.

## 🛠️ Development

### Implementation

The mod uses **Harmony** to intercept `StoneWardsNetworkManager.OnServerDisconnect` before the disconnected player's object is removed. It runs only while the Mirror server is active.

Tomes are selected from the player's saved `rogueUpgrades` with category `PICKUP`. Inventory entries are read from `InventorySystem`; supported items are spawned using the game's server-side pickup creation method, then removed from the original inventory to update save data. Failed drops are left in the saved state where possible.

The `StonewardsDisconnectDrops.csproj` targets `netstandard2.1` and references assemblies from the local game and BepInEx installation. The build and installation commands are in the section above.

For a release ZIP, run `package-thunderstore.ps1 -GameDir <game-path> -ProfileDir <profile-path>` from the project root. It builds the plugin and validates the icon, versions, dependency, README, and ZIP contents before writing `dist/DisconnectDrops-0.1.0.zip`. No upload or tag is created automatically.

## 🔗 Links

[Report an issue](https://github.com/thereisnouser/stonewards-disconnect-drops/issues) · [Changelog](CHANGELOG.md)

---

*Made by thereisnouser*
