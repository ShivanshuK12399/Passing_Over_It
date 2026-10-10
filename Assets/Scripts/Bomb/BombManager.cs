using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PassingOverIt.Player;

namespace PassingOverIt.Bomb
{
    /// <summary>
    /// Core server-authoritative Bomb Manager for Passing Over It.
    /// Handles bomb ownership, network sync, pass validation (distance, FOV, 1.5s immunity),
    /// countdown timer end, explosion, scoring (Kills/Deaths), and victim respawning.
    /// </summary>
    [DisallowMultipleComponent]
    public class BombManager : NetworkBehaviour
    {
        public static BombManager Instance { get; private set; }

        [Header("Bomb Configuration")]
        [Tooltip("Prefab instantiated as the physical bomb held by the player.")]
        [SerializeField] private GameObject bombPrefab;
        [Tooltip("Initial countdown duration for a new bomb in seconds.")]
        [SerializeField] private float bombTimerDuration = 20f;
        [Tooltip("Maximum distance allowed for action button tag/pass.")]
        [SerializeField] private float maxPassDistance = 4.0f;
        [Tooltip("Maximum FOV angle (in degrees) in front of tagger for valid pass.")]
        [SerializeField] private float maxPassFOVAngle = 90f;
        [Tooltip("Tag immunity duration (seconds) to prevent passing back to the previous holder.")]
        [SerializeField] private float tagImmunityDuration = 1.5f;
        [Tooltip("Delay in seconds before victim respawns after bomb explosion.")]
        [SerializeField] private float respawnDelay = 2.0f;
        [Tooltip("If true, passing bomb resets countdown back to full duration. If false, remaining countdown carries over.")]
        [SerializeField] private bool resetTimerOnPass = false;

        [Header("Audio")]
        [SerializeField] private AudioClip passAudioClip;
        [SerializeField] private AudioClip explosionAudioClip;

        // Synchronized Network State
        public readonly SyncVar<int> CurrentHolderObjectId = new SyncVar<int>(-1);
        public readonly SyncVar<double> TimerEndTime = new SyncVar<double>(0);
        public readonly SyncVar<bool> IsTimerActive = new SyncVar<bool>(false);

        // Events for UI & Presentation decoupling
        public static event Action<PlayerController, PlayerController> OnBombPassed; // (fromPlayer, toPlayer)
        public static event Action<PlayerController> OnBombHolderChanged;           // (newHolder)
        public static event Action<PlayerController, PlayerController> OnBombExploded; // (victim, killer)
        public static event Action<int> OnTimerSecondChanged;                        // (remainingWholeSeconds)

        // Server-only runtime state
        private int _lastHolderObjectId = -1;
        private int _lastPasserObjectId = -1;
        private float _lastPassTime = -999f;
        private AudioSource _audioSource;
        private int _lastDisplayedSecond = -1;

        public GameObject BombPrefab => bombPrefab;
        public float MaxPassDistance => maxPassDistance;
        public float MaxPassFOVAngle => maxPassFOVAngle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            CurrentHolderObjectId.OnChange += OnHolderIdChanged;
            TimerEndTime.OnChange += OnTimerEndTimeChanged;
        }

        private void OnDisable()
        {
            CurrentHolderObjectId.OnChange -= OnHolderIdChanged;
            TimerEndTime.OnChange -= OnTimerEndTimeChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            StartCoroutine(AutoAssignBombWhenPlayersReady());
        }

        private IEnumerator AutoAssignBombWhenPlayersReady()
        {
            // Wait for at least one player to spawn
            while (NetworkManager == null || ServerManager.Clients.Count == 0)
            {
                yield return new WaitForSeconds(0.5f);
            }

            yield return new WaitForSeconds(1.0f);
            AssignRandomBomb();
        }

        private void Update()
        {
            UpdateLocalTimerEvent();

            if (!IsServerStarted || !IsTimerActive.Value) return;

            // Server-side check for bomb explosion
            double remaining = TimerEndTime.Value - Time.timeAsDouble;
            if (remaining <= 0)
            {
                ExplodeBombServer();
            }
        }

        private void UpdateLocalTimerEvent()
        {
            if (!IsTimerActive.Value || TimerEndTime.Value <= 0) return;

            double remaining = Math.Max(0.0, TimerEndTime.Value - Time.timeAsDouble);
            int currentSecond = Mathf.CeilToInt((float)remaining);

            if (currentSecond != _lastDisplayedSecond)
            {
                _lastDisplayedSecond = currentSecond;
                OnTimerSecondChanged?.Invoke(currentSecond);
            }
        }

        #region Bomb Transfer & Validation (Server)

        /// <summary>
        /// Request to pass the bomb from a tagger to a target player.
        /// Authoritatively validated on the server.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerRequestPassBombRpc(int taggerObjectId, int targetObjectId)
        {
            // If offline test mode (no active server started yet), allow execution
            bool isNetworkedServer = IsServerStarted;

            // Auto-assign bomb to tagger if no holder exists yet
            if (CurrentHolderObjectId.Value < 0)
            {
                CurrentHolderObjectId.Value = taggerObjectId;
                TimerEndTime.Value = Time.timeAsDouble + bombTimerDuration;
                IsTimerActive.Value = true;
                Debug.Log($"[BombManager] Auto-assigned bomb to tagger Player {taggerObjectId} before pass.");
            }

            // Validate tagger is current holder
            if (taggerObjectId != CurrentHolderObjectId.Value)
            {
                Debug.LogWarning($"[BombManager] Pass rejected: Client {taggerObjectId} is not current bomb holder ({CurrentHolderObjectId.Value}).");
                return;
            }

            // Find player network objects
            PlayerController tagger = GetPlayerByObjectId(taggerObjectId);
            PlayerController target = GetPlayerByObjectId(targetObjectId);

            if (tagger == null || target == null || target.IsEliminated.Value)
            {
                Debug.LogWarning("[BombManager] Pass rejected: Tagger or Target player object not found or target eliminated.");
                return;
            }

            // Validate tag immunity (cannot pass back to last holder within immunity duration)
            if (targetObjectId == _lastHolderObjectId && (Time.time - _lastPassTime) < tagImmunityDuration)
            {
                Debug.Log($"[BombManager] Pass rejected: Target {targetObjectId} has tag immunity (Time remaining: {tagImmunityDuration - (Time.time - _lastPassTime):F2}s).");
                return;
            }

            // Validate physical 2D horizontal distance
            Vector3 toTarget = target.transform.position - tagger.transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance > maxPassDistance)
            {
                Debug.Log($"[BombManager] Pass rejected: Distance {distance:F2}m exceeds max {maxPassDistance}m.");
                return;
            }

            // Validate FOV angle in front of tagger
            Vector3 directionToTarget = distance > 0.001f ? toTarget / distance : tagger.transform.forward;
            float angle = Vector3.Angle(tagger.transform.forward, directionToTarget);
            if (angle > maxPassFOVAngle * 0.5f)
            {
                Debug.Log($"[BombManager] Pass rejected: Angle {angle:F1}° outside FOV cone ({maxPassFOVAngle * 0.5f}°).");
                return;
            }

            // Line of sight obstacle check (ignoring triggers like camera deadzones)
            if (Physics.Linecast(tagger.transform.position + Vector3.up * 1f, target.transform.position + Vector3.up * 1f, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(target.transform) && !hit.transform.IsChildOf(tagger.transform))
                {
                    Debug.Log($"[BombManager] Pass rejected: Line of sight blocked by collider '{hit.collider.name}'.");
                    return;
                }
            }

            // All checks passed! Execute Bomb Transfer
            ExecuteBombTransferServer(tagger, target);
        }

        private void ExecuteBombTransferServer(PlayerController tagger, PlayerController target)
        {
            _lastHolderObjectId = tagger.ObjectId;
            _lastPasserObjectId = tagger.ObjectId;
            _lastPassTime = Time.time;
            CurrentHolderObjectId.Value = target.ObjectId;

            if (resetTimerOnPass)
            {
                TimerEndTime.Value = Time.timeAsDouble + bombTimerDuration;
            }

            ObserversPlayPassSoundRpc();
            ObserversNotifyBombPassedRpc(tagger.ObjectId, target.ObjectId);
            Debug.Log($"[BombManager] Bomb passed from Player {tagger.ObjectId} to Player {target.ObjectId}!");
        }

        #endregion

        #region Explosion & Reassignment (Server)

        private void ExplodeBombServer()
        {
            IsTimerActive.Value = false;

            PlayerController victim = GetPlayerByObjectId(CurrentHolderObjectId.Value);
            PlayerController killer = GetPlayerByObjectId(_lastPasserObjectId);

            if (victim != null)
            {
                victim.AddDeath();
            }

            if (killer != null && killer != victim)
            {
                killer.AddKill();
            }

            int victimId = victim != null ? victim.ObjectId : -1;
            int killerId = killer != null ? killer.ObjectId : -1;

            ObserversPlayExplosionSoundRpc();
            ObserversNotifyBombExplodedRpc(victimId, killerId);

            CurrentHolderObjectId.Value = -1;
            _lastHolderObjectId = -1;

            if (victim != null)
            {
                StartCoroutine(RespawnVictimAndAssignNewBomb(victim));
            }
            else
            {
                StartCoroutine(DelayAssignNewBomb(respawnDelay));
            }
        }

        private IEnumerator RespawnVictimAndAssignNewBomb(PlayerController victim)
        {
            victim.ServerSetEliminated(true);

            yield return new WaitForSeconds(respawnDelay);

            // Teleport to random spawn position
            Vector3 spawnPos = GetRandomSpawnPosition();
            victim.ServerRespawnAt(spawnPos);

            yield return new WaitForSeconds(0.2f);
            AssignRandomBomb();
        }

        private IEnumerator DelayAssignNewBomb(float delay)
        {
            yield return new WaitForSeconds(delay);
            AssignRandomBomb();
        }

        public void AssignRandomBomb()
        {
            if (!IsServerStarted) return;

            List<PlayerController> players = GetAllActivePlayers();
            if (players.Count == 0) return;

            int randomIndex = UnityEngine.Random.Range(0, players.Count);
            PlayerController newHolder = players[randomIndex];

            CurrentHolderObjectId.Value = newHolder.ObjectId;
            _lastHolderObjectId = -1;
            _lastPasserObjectId = -1;
            _lastPassTime = -999f;
            TimerEndTime.Value = Time.timeAsDouble + bombTimerDuration;
            IsTimerActive.Value = true;

            Debug.Log($"[BombManager] New Bomb assigned to Player {newHolder.ObjectId}!");
        }

        #endregion

        #region Client Synchronization Callbacks & RPCs

        private void OnHolderIdChanged(int oldVal, int newVal, bool asServer)
        {
            PlayerController holder = GetPlayerByObjectId(newVal);
            OnBombHolderChanged?.Invoke(holder);
        }

        private void OnTimerEndTimeChanged(double oldVal, double newVal, bool asServer)
        {
            _lastDisplayedSecond = -1;
        }

        [ObserversRpc]
        private void ObserversNotifyBombPassedRpc(int taggerId, int targetId)
        {
            PlayerController tagger = GetPlayerByObjectId(taggerId);
            PlayerController target = GetPlayerByObjectId(targetId);
            OnBombPassed?.Invoke(tagger, target);
        }

        [ObserversRpc]
        private void ObserversNotifyBombExplodedRpc(int victimId, int killerId)
        {
            PlayerController victim = GetPlayerByObjectId(victimId);
            PlayerController killer = GetPlayerByObjectId(killerId);
            OnBombExploded?.Invoke(victim, killer);
        }

        [ObserversRpc]
        private void ObserversPlayPassSoundRpc()
        {
            if (passAudioClip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(passAudioClip);
            }
        }

        [ObserversRpc]
        private void ObserversPlayExplosionSoundRpc()
        {
            if (explosionAudioClip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(explosionAudioClip);
            }
        }

        #endregion

        #region Helpers

        private readonly List<PlayerController> _activePlayersCache = new List<PlayerController>(16);

        public PlayerController GetPlayerByObjectId(int objectId)
        {
            if (objectId < 0) return null;

            if (PlayerController.PlayersByObjectId.TryGetValue(objectId, out var player))
                return player;

            for (int i = 0; i < PlayerController.AllPlayers.Count; i++)
            {
                PlayerController p = PlayerController.AllPlayers[i];
                if (p != null && p.ObjectId == objectId)
                    return p;
            }

            return null;
        }

        public List<PlayerController> GetAllActivePlayers()
        {
            _activePlayersCache.Clear();
            for (int i = 0; i < PlayerController.AllPlayers.Count; i++)
            {
                PlayerController p = PlayerController.AllPlayers[i];
                if (p != null && !p.IsEliminated.Value)
                    _activePlayersCache.Add(p);
            }
            return _activePlayersCache;
        }

        private Vector3 GetRandomSpawnPosition()
        {
            GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].transform.position;
            }
            return Vector3.up * 1.0f;
        }

        #endregion
    }
}
