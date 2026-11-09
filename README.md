# Leave Tome Drop (Stonewards)

**Drops consumed tomes when a player leaves, is kicked, or disconnects. Host-only.**

## Features

- Drops each **picked-up tome** once, including repeated upgrades, around the disconnected dwarf's last position.
- Excludes **level-up rings** and other non-pickup upgrades.
- Uses Stonewards' normal, networked tome pickup prefab so other players see and can collect the tomes.
- Removes **successfully dropped** entries from that player's saved run data to prevent reconnect duplication. Entries that could not be spawned stay in the saved run data.
- Does nothing in the Hub; protects against duplicate disconnect notifications and resets when a new run begins.

## Install / build (Windows)

Requires the **.NET SDK** and an existing **BepInEx 5** Stonewards profile. No game or proprietary DLLs are included.

1. Clone the repository or download its ZIP.
2. Open PowerShell in the project folder.
3. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir 'F:\Steam\steamapps\common\Stonewards' `
  -ProfileDir 'C:\Users\user\AppData\Roaming\r2modmanPlus-local\Stonewards\profiles\YOUR_PROFILE'
```

4. If you installed the earlier prototype, remove `BepInEx/plugins/thereisnouser-DropConsumedTomes` first; the old plugin can otherwise load alongside the renamed one.
5. Start Stonewards through the profile. Only the **host** installs this mod.
6. Have a second player pick up two tomes, disconnect or get kicked, then verify that two tomes appear. Rejoin and verify they are not restored again.

### Configuration

`BepInEx/config/com.thereisnouser.stonewards.leavetomedrop.cfg`

- `General.Enabled = true`
- `General.ScatterRadius = 1.6` (metres, 0.25–4)

### Development

- C# / netstandard2.1, Harmony, BepInEx 5, Mirror.
- Stonewards references are supplied at build time from the installed game folder, and BepInEx references from the selected profile.
- Patched host method: `StoneWardsNetworkManager.OnServerDisconnect` (**Prefix** before Mirror destroys the player), plus add-player/run-reset callbacks.
- Authoritative saved list: `FirstPersonController.RunPlayerData.rogueUpgrades` (`RogueUpgradeData` has `upgradeID` and `rarity`).
- Filters `UpgradeCategory.PICKUP`, intentionally **not** `LEVELUP` (rings).
- Prefab lookup: `ItemManager.itemDataBase.items` → `PickupUpgradeItemDataSO` → `PickableUpgrade` prefab.
- Drop order follows `TreasureChest.SpawnItem`: instantiate, `NetworkInit`, `SelectPickupUpgrade` with one-element pool, `NetworkServer.Spawn`.

## Important limitations / status

- **Source-only prototype**. Method signatures and IL flow were verified against the user's `Assembly-CSharp.dll` dated 2026-11-06, but the mod **was not compiled or tested in-game** in this environment; a first multiplayer test is required.
- Upgrades with **team-wide/server-wide** effects may persist after a player leaves in the unmodded game. The mod does not reverse already-applied effects. Re-picking such a tome **may stack the team-wide effect again**. This is a known gameplay caveat that needs an in-game test (or a follow-up targeted rollback).
- The mod drops only tomes recorded in `RunPlayerData`; events not yet received by the host during a sudden network interruption cannot be recovered.
- When the host quits the session altogether, ground loot cannot persist into a closed session.
- Not yet checked against other mods that alter or share the saved upgrade list.

## Version

- **0.1.0** — first host-side disconnect/drop implementation.
