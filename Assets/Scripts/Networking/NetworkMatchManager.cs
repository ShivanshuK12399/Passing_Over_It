using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PassingOverIt.Player;

namespace PassingOverIt.Networking
{
    public enum MatchState
    {
        WaitingForPlayers,
        Round1,
        Round2,
        Round3,
        GameOver
    }

    /// <summary>
    /// Server-authoritative Round State Machine & Match Manager.
    /// Controls round transitions, player eliminations, spectator state, and victory announcements.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkMatchManager : NetworkBehaviour
    {
        public static NetworkMatchManager Instance { get; private set; }

        private readonly SyncVar<MatchState> _currentMatchState = new SyncVar<MatchState>(MatchState.WaitingForPlayers);
        private readonly SyncVar<int> _activePlayerCount = new SyncVar<int>(0);
        private readonly SyncVar<string> _winnerPlayerName = new SyncVar<string>("");

        public event Action<MatchState> OnMatchStateChanged;
        public event Action<int> OnActivePlayerCountChanged;
        public event Action<string> OnWinnerAnnounced;

        private readonly List<PlayerController> _registeredPlayers = new List<PlayerController>(16);
        private readonly List<PlayerController> _survivingPlayers = new List<PlayerController>(16);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _currentMatchState.OnChange += HandleMatchStateChanged;
            _activePlayerCount.OnChange += HandlePlayerCountChanged;
            _winnerPlayerName.OnChange += HandleWinnerChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _currentMatchState.OnChange -= HandleMatchStateChanged;
            _activePlayerCount.OnChange -= HandlePlayerCountChanged;
            _winnerPlayerName.OnChange -= HandleWinnerChanged;
        }

        [Server]
        public void RegisterPlayerServer(PlayerController player)
        {
            if (!_registeredPlayers.Contains(player))
            {
                _registeredPlayers.Add(player);
                _survivingPlayers.Add(player);
                _activePlayerCount.Value = _survivingPlayers.Count;

                Debug.Log($"[NetworkMatchManager] Registered Player {player.OwnerId}. Total connected: {_registeredPlayers.Count}");

                // Auto-start match when at least 2 players are present
                if (_currentMatchState.Value == MatchState.WaitingForPlayers && _registeredPlayers.Count >= 2)
                {
                    StartMatchServer();
                }
            }
        }

        [Server]
        public void StartMatchServer()
        {
            _survivingPlayers.Clear();
            _survivingPlayers.AddRange(_registeredPlayers);
            _activePlayerCount.Value = _survivingPlayers.Count;

            _currentMatchState.Value = MatchState.Round1;
            Debug.Log("[NetworkMatchManager] Starting Match! ROUND 1");

            if (NetworkBombManager.Instance != null)
            {
                NetworkBombManager.Instance.StartRoundWithBomb(_survivingPlayers);
            }
        }

        [Server]
        public void EliminatePlayerServer(PlayerController player)
        {
            if (_survivingPlayers.Contains(player))
            {
                _survivingPlayers.Remove(player);
                _activePlayerCount.Value = _survivingPlayers.Count;

                // Notify client to enter Spectator Mode
                TargetSetSpectatorModeRpc(player.Owner, true);

                CheckRoundProgressionServer();
            }
        }

        [Server]
        private void CheckRoundProgressionServer()
        {
            if (_survivingPlayers.Count <= 1)
            {
                // Victory condition
                _currentMatchState.Value = MatchState.GameOver;
                PlayerController winner = _survivingPlayers.Count == 1 ? _survivingPlayers[0] : null;
                _winnerPlayerName.Value = winner != null ? $"Player {winner.OwnerId}" : "No Winner";

                Debug.Log($"[NetworkMatchManager] GAME OVER! Winner: {_winnerPlayerName.Value}");
                return;
            }

            // Progression check based on round
            if (_currentMatchState.Value == MatchState.Round1)
            {
                _currentMatchState.Value = MatchState.Round2;
                Debug.Log("[NetworkMatchManager] Round 1 Over -> Advanced to ROUND 2");
            }
            else if (_currentMatchState.Value == MatchState.Round2)
            {
                _currentMatchState.Value = MatchState.Round3;
                Debug.Log("[NetworkMatchManager] Round 2 Over -> Advanced to ROUND 3 (FINAL)");
            }

            // Start next round with new bomb assignment
            if (NetworkBombManager.Instance != null)
            {
                NetworkBombManager.Instance.StartRoundWithBomb(_survivingPlayers);
            }
        }

        [TargetRpc]
        private void TargetSetSpectatorModeRpc(FishNet.Connection.NetworkConnection target, bool isSpectator)
        {
            Debug.Log($"[NetworkMatchManager] Local player eliminated. Entering Spectator Mode: {isSpectator}");
            // Disable player control UI and active colliders for local eliminated player
            PlayerController localPlayer = FindFirstObjectByType<PlayerController>();
            if (localPlayer != null && localPlayer.IsOwner)
            {
                localPlayer.enabled = !isSpectator;
            }
        }

        private void HandleMatchStateChanged(MatchState prev, MatchState next, bool asServer)
        {
            OnMatchStateChanged?.Invoke(next);
        }

        private void HandlePlayerCountChanged(int prev, int next, bool asServer)
        {
            OnActivePlayerCountChanged?.Invoke(next);
        }

        private void HandleWinnerChanged(string prev, string next, bool asServer)
        {
            if (!string.IsNullOrEmpty(next))
            {
                OnWinnerAnnounced?.Invoke(next);
            }
        }
    }
}
