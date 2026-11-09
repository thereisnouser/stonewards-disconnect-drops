using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mirror;
using MyBox;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StonewardsLeaveTomeDrop
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.thereisnouser.stonewards.leavetomedrop";
        public const string PluginName = "Leave Tome Drop";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> ScatterRadius;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Drop consumed pickup tomes when a remote player disconnects or is kicked.");
            ScatterRadius = Config.Bind("General", "ScatterRadius", 1.6f,
                new ConfigDescription("Horizontal radius of dropped tomes in metres.",
                    new AcceptableValueRange<float>(0.25f, 4.0f)));
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
            TomeDropper.OnDisconnect(__instance, __0);
        }
    }

    [HarmonyPatch(typeof(StoneWardsNetworkManager), nameof(StoneWardsNetworkManager.OnServerAddPlayer))]
    internal static class PlayerAddedPatch
    {
        private static void Postfix(NetworkConnectionToClient __0)
        {
            TomeDropper.OnPlayerAdded(__0);
        }
    }

    [HarmonyPatch(typeof(StoneWardsNetworkManager), "ClearAllRunData")]
    internal static class RunClearedPatch
    {
        private static void Postfix()
        {
            TomeDropper.OnRunCleared();
        }
    }

    internal static class TomeDropper
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

            // No book rain in the Hub or during normal end-of-run scene changes.
            if (GameManager.Instance == null || GameManager.Instance.SceneType != GameSceneType.Level)
                return;

            try
            {
                FirstPersonController player = FindPlayer(manager, conn);
                if (player == null) return;

                ulong playerId = player.playerID;
                if (ProcessedPlayers.Contains(playerId)) return;

                RunPlayerData record = player.RunPlayerData;
                // The game stores this exact object inside the SaveManager dictionary.
                if (record == null)
                {
                    var saveManager = Singleton<SaveManager>.Instance;
                    var records = saveManager != null && saveManager.saveContainer != null
                        ? saveManager.saveContainer.runPlayersData : null;
                    if (records == null || !records.TryGetValue(playerId, out record)) return;
                }
                if (record.rogueUpgrades == null || record.rogueUpgrades.Count == 0) return;

                var upgradeManager = RogueLikeUpgradeManager.Instance;
                var itemData = GetTomeItemData();
                if (upgradeManager == null || itemData == null)
                {
                    Plugin.Log.LogWarning("Cannot drop tomes: upgrade database or tome pickup prefab not found. Save data was not touched.");
                    return;
                }

                // Each saved entry represents exactly one consumed upgrade, including stacks.
                var tomes = new List<Tuple<RogueUpgradeData, RogueLikeUpgradeSO>>();
                foreach (RogueUpgradeData entry in record.rogueUpgrades)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.upgradeID)) continue;
                    RogueLikeUpgradeSO upgrade = upgradeManager.GetUpgradeSOByID(entry.upgradeID);
                    if (upgrade == null) continue;
                    if (upgrade.UpgradeCategory != UpgradeCategory.PICKUP) continue;
                    tomes.Add(Tuple.Create(entry, upgrade));
                }
                if (tomes.Count == 0) return;

                ProcessedPlayers.Add(playerId);
                Vector3 center = player.transform.position + Vector3.up * 0.9f;
                int spawnedCount = 0;
                for (int i = 0; i < tomes.Count; ++i)
                {
                    RogueUpgradeData entry = tomes[i].Item1;
                    RogueLikeUpgradeSO upgrade = tomes[i].Item2;
                    Vector3 position = Scatter(center, i);
                    if (!SpawnTome(itemData, upgrade, position)) continue;

                    // Commit only after the networked object was spawned.
                    // Otherwise reconnect would restore the same tome and duplicate it.
                    record.rogueUpgrades.Remove(entry);
                    spawnedCount++;
                }
                Plugin.Log.LogInfo("Player " + playerId + " disconnected: dropped " + spawnedCount +
                    "/" + tomes.Count + " consumed tomes (rings untouched).");
            }
            catch (Exception e)
            {
                // A disconnect must always be allowed to finish even if a mod fails.
                Plugin.Log.LogError("Tome drop failed; vanilla disconnect continues. " + e);
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
