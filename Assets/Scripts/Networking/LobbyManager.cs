using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace PassingOverIt.Networking
{
    /// <summary>
    /// Manages Unity Gaming Services (UGS) Lobby creation, room search, quick join,
    /// and heartbeat keep-alive pings.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        public const string KEY_RELAY_JOIN_CODE = "RelayJoinCode";

        public event Action<Lobby> OnLobbyJoined;
        public event Action OnLobbyLeft;
        public event Action<string> OnLobbyError;
        public event Action<List<Lobby>> OnLobbyListUpdated;

        public Lobby JoinedLobby { get; private set; }

        private float _heartbeatTimer;
        private const float HEARTBEAT_INTERVAL = 15f; // UGS lobby heartbeat required every 15-30s

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            HandleHeartbeat();
        }

        /// <summary>
        /// Sends periodic heartbeat to UGS to keep hosted lobby alive.
        /// </summary>
        private async void HandleHeartbeat()
        {
            if (JoinedLobby == null || JoinedLobby.HostId != Unity.Services.Authentication.AuthenticationService.Instance.PlayerId)
                return;

            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer >= HEARTBEAT_INTERVAL)
            {
                _heartbeatTimer = 0f;
                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(JoinedLobby.Id);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LobbyManager] Heartbeat failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Creates a public or private UGS lobby and attaches the Relay Join Code to metadata.
        /// </summary>
        public async Task<Lobby> CreateLobbyAsync(string lobbyName, int maxPlayers, string relayJoinCode, bool isPrivate = false)
        {
            try
            {
                CreateLobbyOptions options = new CreateLobbyOptions
                {
                    IsPrivate = isPrivate,
                    Data = new Dictionary<string, DataObject>
                    {
                        {
                            KEY_RELAY_JOIN_CODE,
                            new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode)
                        }
                    }
                };

                Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
                JoinedLobby = lobby;
                _heartbeatTimer = 0f;

                Debug.Log($"[LobbyManager] Created Lobby '{lobby.Name}' | Code: {lobby.LobbyCode} | Id: {lobby.Id}");
                OnLobbyJoined?.Invoke(lobby);
                return lobby;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyManager] Failed to create lobby: {ex.Message}");
                OnLobbyError?.Invoke(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Joins a lobby using a 6-character room code.
        /// </summary>
        public async Task<Lobby> JoinLobbyByCodeAsync(string lobbyCode)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                OnLobbyError?.Invoke("Lobby Code cannot be empty.");
                return null;
            }

            try
            {
                Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode.Trim().ToUpper());
                JoinedLobby = lobby;

                Debug.Log($"[LobbyManager] Successfully joined lobby '{lobby.Name}' via code '{lobbyCode}'");
                OnLobbyJoined?.Invoke(lobby);
                return lobby;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyManager] Failed to join lobby by code: {ex.Message}");
                OnLobbyError?.Invoke(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Quick joins any available open lobby.
        /// </summary>
        public async Task<Lobby> QuickJoinLobbyAsync()
        {
            try
            {
                Lobby lobby = await LobbyService.Instance.QuickJoinLobbyAsync();
                JoinedLobby = lobby;

                Debug.Log($"[LobbyManager] Quick joined lobby '{lobby.Name}'");
                OnLobbyJoined?.Invoke(lobby);
                return lobby;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyManager] Quick join failed: {ex.Message}");
                OnLobbyError?.Invoke(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Queries available open lobbies for listing in UI.
        /// </summary>
        public async Task<List<Lobby>> RefreshLobbyListAsync()
        {
            try
            {
                QueryLobbiesOptions options = new QueryLobbiesOptions
                {
                    Count = 25,
                    Filters = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                    }
                };

                QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
                List<Lobby> lobbies = response.Results;

                OnLobbyListUpdated?.Invoke(lobbies);
                return lobbies;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyManager] Refresh lobby list failed: {ex.Message}");
                OnLobbyError?.Invoke(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Extracts Relay Join Code from lobby data.
        /// </summary>
        public string GetRelayJoinCode(Lobby lobby)
        {
            if (lobby != null && lobby.Data != null && lobby.Data.TryGetValue(KEY_RELAY_JOIN_CODE, out DataObject dataObject))
            {
                return dataObject.Value;
            }
            return null;
        }

        /// <summary>
        /// Leaves the current lobby.
        /// </summary>
        public async Task LeaveLobbyAsync()
        {
            if (JoinedLobby == null) return;

            try
            {
                string playerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
                await LobbyService.Instance.RemovePlayerAsync(JoinedLobby.Id, playerId);
                JoinedLobby = null;

                OnLobbyLeft?.Invoke();
                Debug.Log("[LobbyManager] Left lobby.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyManager] Error leaving lobby: {ex.Message}");
            }
        }
    }
}
