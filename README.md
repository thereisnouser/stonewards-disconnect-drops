# Leave Loot Drop (Stonewards)

**Drops consumed tomes and the entire droppable inventory when a player leaves, is kicked or disconnects.** Host-only BepInEx 5 mod.

## Features

- Drops every consumed **pickup tome**, excluding level-up rings.
- Drops the entire server-side inventory, including equipped slots and item stacks, provided the item has a native pickup prefab.
- Items drop near the departing dwarf and are collectible by teammates.
- Removes only successfully spawned loot from the player's saved run data to prevent reconnect duplication.
- Skips Hub and run transitions; guards against multiple disconnect callbacks.
- **Host-only installation**; no client mod required.

## Configuration

File: `BepInEx/config/com.thereisnouser.stonewards.leavelootdrop.cfg`

- `General.Enabled = true`
- `General.DropTomes = true`
- `General.DropInventory = true`
- `General.ScatterRadius = 1.6` (metres, 0.25–4)

## Build and install

Requires .NET SDK, Stonewards and BepInEx 5 in the selected profile. No proprietary game DLLs are included.

```powershell
$repo = 'X:\projects\stonewards-leave-loot-drop'
$game = 'F:\Steam\steamapps\common\Stonewards'
$profile = "$env:APPDATA\r2modmanPlus-local\Stonewards\profiles\stonewards-test"

powershell -ExecutionPolicy Bypass -File "$repo\build-and-install.ps1" `
    -GameDir $game -ProfileDir $profile
```

The installer puts `StonewardsLeaveLootDrop.dll` in `BepInEx/plugins/thereisnouser-LeaveLootDrop` and removes the old Leave Tome Drop DLL.

## Technical details

- Hooks `StoneWardsNetworkManager.OnServerDisconnect` before player destruction.
- Tome data comes from `RunPlayerData.rogueUpgrades` filtered for `UpgradeCategory.PICKUP`.
- Inventory comes from `FirstPersonController.InventorySystem.InventoryEntries` (all slots).
- Uses `ItemManager.ServerInstantiatePickableItem` for networked world loot, then `InventorySystem.ServerRemoveItemFromSlot` to synchronize the saved inventory.
- Rolls back the spawned pickup if the original inventory entry cannot be removed.

## Known limitations / status

- **Prototype v0.2.0**: method signatures checked against the user's 2026-11-06 Stonewards assembly; not compiled or tested in a multiplayer session yet.
- Items without `pickableItemPrefab` cannot be safely dropped. Their saved entries are deliberately retained and a warning is logged. Some special or bound items may fall into this category.
- Standard item drops may not preserve an item slot's `fill` state (charge/durability), matching the limitations of the native spawn method.
- Teamwide tome bonuses may remain applied after the player leaves and stack if the tome is recollected.
- Physically carried world objects outside the inventory aren't covered.
- When the host ends the game session, ground loot does not persist.

See [CHANGELOG.md](CHANGELOG.md).

*Made by thereisnouser*
