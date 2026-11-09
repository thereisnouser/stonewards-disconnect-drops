# Disconnect Drops (Stonewards)

**Drops consumed tomes and the entire droppable inventory when a player leaves, gets kicked, or disconnects.** Host-only BepInEx mod.

## ✨ Features

- Drops every consumed **pickup tome**. Level-up rings are unaffected.
- Drops the disconnecting player's **inventory**, including equipped slots and item stacks, for items with a native pickup prefab.
- Dropped loot appears at the player's last position and is collectible by teammates.
- Removes only successfully spawned loot from that player's saved run data to prevent duplicate restoration after reconnecting.
- Ignores the Hub and run transitions; protects against duplicate disconnect processing.
- **Host-only** installation; no client mod required.

## 🎮 How to use

Install on the host and start a run. When a remote player leaves, is kicked, or loses the connection, their saved pickup tomes and supported inventory items are dropped.

## ⚙️ Configuration

File: `BepInEx/config/com.thereisnouser.stonewards.disconnectdrops.cfg`

- `General.Enabled = true`
- `General.DropTomes = true`
- `General.DropInventory = true`
- `General.ScatterRadius = 1.6` (metres, 0.25–4)

The build script migrates the previous `leavelootdrop.cfg` configuration if the new file does not exist.

## 📦 Installation / build from source

Requires .NET SDK, the installed Stonewards game and a BepInEx 5 profile. Game binaries are not included.

```powershell
$repo = 'X:\projects\stonewards-disconnect-drops'
$game = 'F:\Steam\steamapps\common\Stonewards'
$profile = "$env:APPDATA\r2modmanPlus-local\Stonewards\profiles\stonewards-test"

powershell -ExecutionPolicy Bypass -File "$repo\build-and-install.ps1" `
    -GameDir $game -ProfileDir $profile
```

Build output: `StonewardsDisconnectDrops.dll`. Installed at:

`BepInEx/plugins/thereisnouser-DisconnectDrops/StonewardsDisconnectDrops.dll`

After a successful build, the installer removes the earlier `LeaveTomeDrop` and `LeaveLootDrop` plugin DLLs from that profile.

## 🔧 Compatibility & known limitations

- **Unreleased prototype v0.1.0.** The previous iteration was built from analyzed Stonewards methods, but this rename itself has **not been compiled or tested in multiplayer**.
- Items without a native `pickableItemPrefab` are deliberately retained in saved inventory rather than deleted.
- Pickup spawning may not preserve slot-specific `fill` (charges/durability).
- Team-wide tome effects may persist and stack if the dropped tome is picked up again.
- Grabbed objects outside the inventory are not included.
- When the host closes the session, ground drops cannot survive in the closed world.

## 🛠️ Development

- Uses BepInEx 5, Harmony and Mirror; `netstandard2.1`.
- Prefix on `StoneWardsNetworkManager.OnServerDisconnect`.
- Tomes from `RunPlayerData.rogueUpgrades`, filtered to `UpgradeCategory.PICKUP`.
- Inventory from `FirstPersonController.InventorySystem.InventoryEntries`; remove via `ServerRemoveItemFromSlot` after a successful server-side pickup spawn.
- The code still needs local compilation and an in-game disconnect/kick test.

See [CHANGELOG.md](CHANGELOG.md).

*Made by thereisnouser*
