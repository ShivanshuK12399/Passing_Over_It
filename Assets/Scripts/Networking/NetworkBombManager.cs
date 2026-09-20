using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PassingOverIt.Player;

namespace PassingOverIt.Networking
{
    /// <summary>
    /// Server-authoritative Bomb Tag Manager.
    /// Handles bomb assignment, countdown timer, tag collision transfers,
    /// explosion eliminations, and visual bomb attachment across clients.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkBombManager : NetworkBehaviour
    {
        public static NetworkBombManager Instance { get; private set; }

        [Header("Bomb Settings")]
        [SerializeField] private GameObject bombPrefab;
        [SerializeField] private Vector3 bombOffset = new Vector3(0f, 2.2f, 0f);
        [SerializeField] private float defaultBombTimer = 20f;
        [SerializeField] private float tagCooldownDuration = 1.0f; // prevents instant tag-back

        [Header("Audio & VFX Prefabs")]
        [SerializeField] private GameObject explosionVfxPrefab;
        [SerializeField] private GameObject tagVfxPrefab;

        // Synced Variables (Server -> Clients)
        private readonly SyncVar<int> _currentHolderNetworkId = new SyncVar<int>(-1);
        private readonly SyncVar<float> _remainingTimer = new SyncVar<float>(20f);
        private readonly SyncVar<bool> _isBombActive = new SyncVar<bool>(false);

        public event Action<PlayerController> OnBombHolderChanged;
        public event Action<float> OnTimerUpdated;
        public event Action<Vector3> OnBombExploded;

        private GameObject _spawnedBombInstance;
        private float _tagCooldownTimer;
        private readonly List<PlayerController> _activePlayers = new List<PlayerController>(16);

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
            _currentHolderNetworkId.OnChange += HandleHolderChanged;
            _remainingTimer.OnChange += HandleTimerChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _currentHolderNetworkId.OnChange -= HandleHolderChanged;
            _remainingTimer.OnChange -= HandleTimerChanged;
        }

        private void Update()
        {
            if (IsServerStarted && _isBombActive.Value)
            {
                UpdateServerTimer();
            }

            if (_tagCooldownTimer > 0f)
            {
                _tagCooldownTimer -= Time.deltaTime;
            }

            UpdateBombVisualPosition();
        }

        [Server]
        public void StartRoundWithBomb(List<PlayerController> players)
        {
            _activePlayers.Clear();
            _activePlayers.AddRange(players);

            if (_activePlayers.Count == 0) return;

            // Select random player to hold bomb at round start
            int randomIndex = UnityEngine.Random.Range(0, _activePlayers.Count);
            PlayerController initialHolder = _activePlayers[randomIndex];

            _currentHolderNetworkId.Value = initialHolder.ObjectId;
            _remainingTimer.Value = defaultBombTimer;
            _isBombActive.Value = true;

            Debug.Log($"[NetworkBombManager] Round started! Bomb assigned to Player {initialHolder.OwnerId}");
        }

        [Server]
        private void UpdateServerTimer()
        {
            if (_remainingTimer.Value > 0f)
            {
                _remainingTimer.Value -= Time.deltaTime;
                if (_remainingTimer.Value <= 0f)
                {
                    _remainingTimer.Value = 0f;
                    ExplodeBombServer();
                }
            }
        }

        [Server]
        private void ExplodeBombServer()
        {
            _isBombActive.Value = false;
            PlayerController holder = GetPlayerByObjectId(_currentHolderNetworkId.Value);

            Vector3 explodePos = holder != null ? holder.transform.position : transform.position;
            ObserversExplodeVfxRpc(explodePos);

            if (holder != null)
            {
                Debug.Log($"[NetworkBombManager] BOMB EXPLODED! Player {holder.OwnerId} is eliminated.");
                _activePlayers.Remove(holder);

                // Notify game manager of elimination
                if (NetworkMatchManager.Instance != null)
                {
                    NetworkMatchManager.Instance.EliminatePlayerServer(holder);
                }
            }
        }

        [Server]
        public void AttemptTagServer(PlayerController attacker, PlayerController target)
        {
            if (!_isBombActive.Value || _tagCooldownTimer > 0f) return;
            if (attacker == null || target == null) return;

            // Verify attacker currently holds the bomb
            if (attacker.ObjectId != _currentHolderNetworkId.Value) return;

            // Transfer bomb
            _currentHolderNetworkId.Value = target.ObjectId;
            _remainingTimer.Value = defaultBombTimer;
            _tagCooldownTimer = tagCooldownDuration;

            ObserversTagVfxRpc(target.transform.position);
            Debug.Log($"[NetworkBombManager] BOMB PASSED! Player {attacker.OwnerId} tagged Player {target.OwnerId}");
        }

        [ObserversRpc]
        private void ObserversTagVfxRpc(Vector3 pos)
        {
            if (tagVfxPrefab != null)
            {
                Instantiate(tagVfxPrefab, pos, Quaternion.identity);
            }
        }

        [ObserversRpc]
        private void ObserversExplodeVfxRpc(Vector3 pos)
        {
            if (explosionVfxPrefab != null)
            {
                Instantiate(explosionVfxPrefab, pos, Quaternion.identity);
            }
            OnBombExploded?.Invoke(pos);
        }

        private void HandleHolderChanged(int prevId, int nextId, bool asServer)
        {
            PlayerController holder = GetPlayerByObjectId(nextId);
            OnBombHolderChanged?.Invoke(holder);
        }

        private void HandleTimerChanged(float prevTime, float nextTime, bool asServer)
        {
            OnTimerUpdated?.Invoke(nextTime);
        }

        private void UpdateBombVisualPosition()
        {
            if (_currentHolderNetworkId.Value < 0)
            {
                if (_spawnedBombInstance != null) _spawnedBombInstance.SetActive(false);
                return;
            }

            PlayerController holder = GetPlayerByObjectId(_currentHolderNetworkId.Value);
            if (holder != null)
            {
                if (_spawnedBombInstance == null && bombPrefab != null)
                {
                    _spawnedBombInstance = Instantiate(bombPrefab);
                }

                if (_spawnedBombInstance != null)
                {
                    _spawnedBombInstance.SetActive(true);
                    _spawnedBombInstance.transform.position = holder.transform.position + bombOffset;
                    _spawnedBombInstance.transform.rotation = holder.transform.rotation;
                }
            }
        }

        private PlayerController GetPlayerByObjectId(int objectId)
        {
            if (objectId < 0) return null;
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].ObjectId == objectId)
                    return players[i];
            }
            return null;
        }
    }
}
