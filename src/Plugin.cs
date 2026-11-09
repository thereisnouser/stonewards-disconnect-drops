using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mirror;
using MyBox;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StonewardsLeaveLootDrop
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.thereisnouser.stonewards.leavelootdrop";
        public const string PluginName = "Leave Loot Drop";
        public const string PluginVersion = "0.2.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> ScatterRadius;
        internal static ConfigEntry<bool> DropTomes;
        internal static ConfigEntry<bool> DropInventory;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Drop picked-up tomes and inventory items when a player leaves, is kicked or disconnects.");
            ScatterRadius = Config.Bind("General", "ScatterRadius", 1.6f,
                new ConfigDescription("Horizontal radius of dropped loot in metres.",
                    new AcceptableValueRange<float>(0.25f, 4.0f)));
            DropTomes = Config.Bind("General", "DropTomes", true,
                "Drop books consumed from the current run; excludes level-up rings.");
            DropInventory = Config.Bind("General", "DropInventory", true,
                "Drop all inventory items, including equipped slots and stacks, on disconnect.");
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded (host-only).");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }

    [HarmonyPatch(typeof(StoneWardsNetworkManager), nameof(StoneWardsNetworkManager.OnServerDisconnect))]
    internal static class DisconnectPatch
    {
        private static void Prefix(StoneWardsNetworkManager __instance, NetworkConnectionToClient __0)
        {
            LootDropper.OnDisconnect(__instance, __0);
        }
    }

    [HarmonyPatch(typeof(StoneWardsNetworkManager), nameof(StoneWardsNetworkManager.OnServerAddPlayer))]
    internal static class PlayerAddedPatch
    {
        private static void Postfix(NetworkConnectionToClient __0)
        {
            LootDropper.OnPlayerAdded(__0);
        }
    }

    [HarmonyPatch(typeof(StoneWardsNetworkManager), "ClearAllRunData")]
    internal static class RunClearedPatch
    {
        private static void Postfix()
        {
            LootDropper.OnRunCleared();
        }
    }

    internal static class LootDropper
    {
        // Prevents repeated disconnection callbacks from dropping the same player's books.
        private static readonly HashSet<ulong> ProcessedPlayers = new HashSet<ulong>();
        private static readonly System.Reflection.FieldInfo ConnectedField =
            AccessTools.Field(typeof(StoneWardsNetworkManager), "playerConnected");
        private static readonly System.Reflection.FieldInfo DatabaseField =
            AccessTools.Field(typeof(ItemManager), "itemDataBase");

        internal static void OnPlayerAdded(NetworkConnectionToClient conn)
        {
            if (!NetworkServer.active || conn == null || conn.identity == null) return;
            var player = conn.identity.GetComponent<FirstPersonController>();
            if (player != null) ProcessedPlayers.Remove(player.playerID);
        }

        internal static void OnRunCleared()
        {
            ProcessedPlayers.Clear();
        }

        internal static void OnDisconnect(StoneWardsNetworkManager manager, NetworkConnectionToClient conn)
        {
            if (!Plugin.Enabled.Value || !NetworkServer.active || manager == null || conn == null)
                return;

            // Do not generate loot in the Hub or during normal end-of-run scene transitions.
            if (GameManager.Instance == null || GameManager.Instance.SceneType != GameSceneType.Level)
                return;

            try
            {
                FirstPersonController player = FindPlayer(manager, conn);
                if (player == null) return;

                ulong playerId = player.playerID;
                if (ProcessedPlayers.Contains(playerId)) return;

                // Mark before spawning to prevent duplicate disconnect callbacks.
                ProcessedPlayers.Add(playerId);
                Vector3 center = player.transform.position + Vector3.up * 0.9f;
                int scatterIndex = 0;

                // Inventory and tome lists are independent; players without tomes
                // must still drop everything they carry.
                int stacksDropped = 0, itemsDropped = 0, tomesDropped = 0;
                if (Plugin.DropInventory.Value)
                    DropInventoryItems(player, center, ref scatterIndex, out stacksDropped, out itemsDropped);
                if (Plugin.DropTomes.Value)
                    tomesDropped = DropConsumedTomes(GetRunPlayerData(player), center, ref scatterIndex);
                Plugin.Log.LogInfo("Player " + playerId + " disconnected: dropped " +
                    tomesDropped + " consumed tomes and " + itemsDropped +
                    " inventory items in " + stacksDropped + " pickups (rings untouched).");
            }
            catch (Exception e)
            {
                // A disconnect must always be allowed to finish even if a mod fails.
                Plugin.Log.LogError("Loot drop failed; vanilla disconnect continues. " + e);
            }
        }

        private static RunPlayerData GetRunPlayerData(FirstPersonController player)
        {
            RunPlayerData record = player.RunPlayerData;
            if (record != null) return record;
            var saveManager = Singleton<SaveManager>.Instance;
            var records = saveManager != null && saveManager.saveContainer != null
                ? saveManager.saveContainer.runPlayersData : null;
            if (records == null) return null;
            records.TryGetValue(player.playerID, out record);
            return record;
        }

        private static int DropConsumedTomes(RunPlayerData record, Vector3 center, ref int scatterIndex)
        {
            if (record == null || record.rogueUpgrades == null || record.rogueUpgrades.Count == 0)
                return 0;
            var manager = RogueLikeUpgradeManager.Instance;
            var itemData = GetTomeItemData();
            if (manager == null || itemData == null)
            {
                Plugin.Log.LogWarning("Cannot drop tomes: manager or pickup prefab unavailable. Saved tomes kept.");
                return 0;
            }

            var tomes = new List<Tuple<RogueUpgradeData, RogueLikeUpgradeSO>>();
            foreach (RogueUpgradeData entry in record.rogueUpgrades)
            {
                if (entry == null || string.IsNullOrEmpty(entry.upgradeID)) continue;
                RogueLikeUpgradeSO upgrade = manager.GetUpgradeSOByID(entry.upgradeID);
                if (upgrade != null && upgrade.UpgradeCategory == UpgradeCategory.PICKUP)
                    tomes.Add(Tuple.Create(entry, upgrade));
            }
            int dropped = 0;
            foreach (var pair in tomes)
            {
                if (!SpawnTome(itemData, pair.Item2, Scatter(center, scatterIndex++))) continue;
                record.rogueUpgrades.Remove(pair.Item1);
                dropped++;
            }
            return dropped;
        }

        private static void DropInventoryItems(FirstPersonController player, Vector3 center,
            ref int scatterIndex, out int stacksDropped, out int itemsDropped)
        {
            stacksDropped = 0;
            itemsDropped = 0;
            InventorySystem inventory = player.InventorySystem;
            ItemManager itemManager = ItemManager.Instance;
            if (inventory == null || !inventory.isServer || itemManager == null)
            {
                Plugin.Log.LogWarning("Inventory unavailable on server at disconnect; saved items kept.");
                return;
            }

            // ServerRemoveItemFromSlot calls UpdateSaveDataSlot, updating
            // RunPlayerData.playersInventoryData before the player is destroyed.
            for (int slot = 0; slot < inventory.InventoryEntries.Count; ++slot)
            {
                InventorySystem.InventoryEntry entry = inventory.InventoryEntries[slot];
                if (entry.IsEmpty || string.IsNullOrEmpty(entry.itemID) || entry.count <= 0)
                    continue;
                ItemDataSO data = itemManager.GetItemDataSoByID(entry.itemID);
                if (data == null || data.pickableItemPrefab == null)
                {
                    Plugin.Log.LogWarning("Item '" + entry.itemID + "' at slot " + slot +
                        " has no pickup prefab; keeping saved inventory entry.");
                    continue;
                }

                // Nonstackable items are forced to count 1 by PickableItem.NetworkInit.
                // Spawn them separately so multiples are not lost.
                int remaining = entry.count;
                while (remaining > 0)
                {
                    int quantity = data.isStackable ? remaining : 1;
                    PickableItem pickup = null;
                    try
                    {
                        pickup = itemManager.ServerInstantiatePickableItem(
                            entry.itemID, quantity, Scatter(center, scatterIndex++), true, true);
                        if (pickup == null)
                        {
                            Plugin.Log.LogWarning("Failed spawning '" + entry.itemID +
                                "' from slot " + slot + "; saved entry unchanged.");
                            break;
                        }
                        if (!inventory.ServerRemoveItemFromSlot(slot, quantity))
                        {
                            DestroySpawnedPickup(pickup);
                            Plugin.Log.LogWarning("Failed removing '" + entry.itemID +
                                "' from slot " + slot + "; pickup rolled back.");
                            break;
                        }
                        remaining -= quantity;
                        itemsDropped += quantity;
                        stacksDropped++;
                    }
                    catch (Exception ex)
                    {
                        if (pickup != null) DestroySpawnedPickup(pickup);
                        Plugin.Log.LogError("Failed dropping '" + entry.itemID + "': " + ex);
                        break;
                    }
                }
            }
        }

        private static void DestroySpawnedPickup(PickableItem pickup)
        {
            if (pickup == null) return;
            try
            {
                if (pickup.netIdentity != null && pickup.netIdentity.netId != 0)
                    NetworkServer.Destroy(pickup.gameObject);
                else
                    UnityEngine.Object.Destroy(pickup.gameObject);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Failed to roll back an inventory pickup: " + ex);
            }
        }

        private static FirstPersonController FindPlayer(StoneWardsNetworkManager manager,
            NetworkConnectionToClient conn)
        {
            var online = ConnectedField != null
                ? ConnectedField.GetValue(manager) as Dictionary<int, FirstPersonController>
                : null;
            FirstPersonController player;
            if (online != null && online.TryGetValue(conn.connectionId, out player) && player != null)
                return player;
            return conn.identity != null
                ? conn.identity.GetComponent<FirstPersonController>() : null;
        }

        private static PickupUpgradeItemDataSO GetTomeItemData()
        {
            var itemManager = ItemManager.Instance;
            var database = itemManager != null && DatabaseField != null
                ? DatabaseField.GetValue(itemManager) as ItemDatabaseSO : null;
            if (database == null || database.items == null) return null;

            foreach (ItemDataSO item in database.items)
            {
                var tome = item as PickupUpgradeItemDataSO;
                if (tome != null && tome.pickableItemPrefab is PickableUpgrade)
                    return tome;
            }
            return null;
        }

        private static Vector3 Scatter(Vector3 center, int i)
        {
            float angle = i * 2.39996323f; // golden angle, avoids identical positions
            float radius = Mathf.Min(Plugin.ScatterRadius.Value, 0.3f + Mathf.Sqrt(i) * 0.24f);
            return center + new Vector3(Mathf.Cos(angle) * radius, 0f,
                Mathf.Sin(angle) * radius);
        }

        private static bool SpawnTome(PickupUpgradeItemDataSO itemData,
            RogueLikeUpgradeSO upgrade, Vector3 position)
        {
            PickableItem instance = null;
            bool spawned = false;
            try
            {
                instance = UnityEngine.Object.Instantiate(itemData.pickableItemPrefab,
                    position, Quaternion.identity);
                var tome = instance as PickableUpgrade;
                if (tome == null) return false;

                // Same order as TreasureChest.SpawnItem in the current game assembly:
                // NetworkInit -> SelectPickupUpgrade -> NetworkServer.Spawn.
                instance.NetworkInit(itemData, 1, false);
                tome.SelectPickupUpgrade(0f, new List<RogueLikeUpgradeSO> { upgrade });
                // A one-element pool causes GetRandomPickupUpgrade to return that exact tome.
                if (tome.NetworkSyncUpgradeID != upgrade.ID)
                {
                    Plugin.Log.LogWarning("Tome selection mismatch: " + upgrade.ID);
                    return false;
                }
                NetworkServer.Spawn(instance.gameObject);
                spawned = true;
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not create tome " + upgrade.ID + ": " + ex);
                return false;
            }
            finally
            {
                if (!spawned && instance != null)
                    UnityEngine.Object.Destroy(instance.gameObject);
            }
        }
    }
}
