using System;
using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Connection;

namespace PassingOverIt.Networking
{
    /// <summary>
    /// Custom project player spawner that spawns player prefabs in GameScene when clients load in.
    /// Attached to [PlayerSpawner] in GameScene.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkPlayerSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private Transform[] spawnPoints;

        private int nextSpawnIndex = 0;

        private void OnEnable()
        {
            if (InstanceFinder.SceneManager != null)
            {
                InstanceFinder.SceneManager.OnClientLoadedStartScenes += HandleClientLoadedStartScenes;
                InstanceFinder.SceneManager.OnQueueEnd += HandleQueueEnd;
            }
        }

        private void OnDisable()
        {
            if (InstanceFinder.SceneManager != null)
            {
                InstanceFinder.SceneManager.OnClientLoadedStartScenes -= HandleClientLoadedStartScenes;
                InstanceFinder.SceneManager.OnQueueEnd -= HandleQueueEnd;
            }
        }

        private void Start()
        {
            if (InstanceFinder.IsServerStarted)
            {
                SpawnPlayersForConnectedClients();
            }
        }

        private void HandleQueueEnd()
        {
            if (InstanceFinder.IsServerStarted)
            {
                SpawnPlayersForConnectedClients();
            }
        }

        private void HandleClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (asServer)
            {
                SpawnPlayerForConnection(conn);
            }
        }

        public void SpawnPlayersForConnectedClients()
        {
            if (!InstanceFinder.IsServerStarted) return;

            foreach (NetworkConnection conn in InstanceFinder.ServerManager.Clients.Values)
            {
                SpawnPlayerForConnection(conn);
            }
        }

        private void SpawnPlayerForConnection(NetworkConnection conn)
        {
            if (playerPrefab == null)
            {
                Debug.LogWarning("[NetworkPlayerSpawner] Player Prefab is missing!");
                return;
            }

            // Don't spawn duplicate player for connection if already spawned
            if (conn.FirstObject != null) return;

            Vector3 pos = playerPrefab.transform.position;
            Quaternion rot = playerPrefab.transform.rotation;

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Transform sp = spawnPoints[nextSpawnIndex % spawnPoints.Length];
                if (sp != null)
                {
                    pos = sp.position;
                    rot = sp.rotation;
                }
                nextSpawnIndex++;
            }

            NetworkObject nob = InstanceFinder.NetworkManager.GetPooledInstantiated(playerPrefab, pos, rot, true);
            InstanceFinder.ServerManager.Spawn(nob, conn);
            InstanceFinder.SceneManager.AddOwnerToDefaultScene(nob);

            Debug.Log($"[NetworkPlayerSpawner] Successfully spawned player for Client {conn.ClientId} at position {pos}");
        }
    }
}
