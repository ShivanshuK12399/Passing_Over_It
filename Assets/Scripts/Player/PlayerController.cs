using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PassingOverIt.Bomb;

namespace PassingOverIt.Player
{
    /// <summary>
    /// Fish-Net player controller: locomotion, jump, dash, dive and Cinemachine camera setup.
    /// The owner simulates from input; remote copies get transforms from NetworkTransform
    /// and one-shot actions (jump/dash/dive) via RPCs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float turningTime = 0.1f;
        [SerializeField] private float jumpHeight = 20f;   // Used as the initial jump velocity.
        [SerializeField] private float jumpGravity = -80f;
        [SerializeField] private float fallGravity = -100f;
        [SerializeField] private float groundedGravity = -10f;

        [Header("Dash")]
        [SerializeField] private float dashSpeed = 20f;
        [SerializeField] private float dashDuration = 0.3f;
        [SerializeField] private float dashCooldown = 1.5f;

        [Header("Dive")]
        [SerializeField] private float diveDistance = 12f; // Target horizontal dive speed.
        [SerializeField] private float diveGravity = -20f;
        [SerializeField] private float diveSlideDuration = 1.0f; // Belly slide duration slowing down to stop.
        [SerializeField, Min(0.1f)] private float diveGetUpAnimSpeed = 1f; // Speed multiplier for Dive_GetUp animation.

        [Header("Character Controller")]
        [SerializeField] private float normalControllerHeight = 2f;
        [SerializeField] private float diveControllerHeight = 1f;
        [SerializeField] private LayerMask groundLayerMask = ~0;
        [SerializeField] private LayerMask playerLayerMask = ~0;

        [Header("Explosion Knockback")]
        [SerializeField] private float knockbackVerticalForce = 15f;
        [SerializeField] private float knockbackHorizontalSpeed = 8.5f;
        [SerializeField] private float knockbackGravity = -25f;
        [SerializeField] private float slideInitialSpeed = 4f;
        [SerializeField] private float slideDeceleration = 4f;
        [SerializeField] private float slideDuration = 1.0f;
        [SerializeField] private float lyingStillDuration = 0.5f;
        [SerializeField, Min(0.1f)] private float explosionGetUpAnimSpeed = 1f; // Speed multiplier for Explosion_GetUp animation.

        [Header("Camera")]
        public CinemachineCamera freeLookCam;
        [SerializeField] private float normalDamping = 1f;
        [SerializeField] private float dashDamping = 0f;

        [Header("Camera Dead Zone")]
        [SerializeField] private bool useDeadZone = true;
        [Tooltip("Half-extents of the dead zone (X: horizontal, Y: vertical, Z: depth).")]
        [SerializeField] private Vector3 deadZoneSize = new Vector3(2f, 1.5f, 2f);
        [Tooltip("Snap the camera target to the player while dashing.")]
        [SerializeField] private bool snapDeadZoneOnDash = true;
        [SerializeField] private bool showDeadZoneGizmos = true;

        [Header("References")]
        public CharacterController characterController;
        public Transform cam;

        private const float MoveInputThreshold = 0.1f;
        private const float DiveSpeedLerpRate = 6f;
        private const float DiveRotationRate = 8f;
        private const float RecoverRotationRate = 20f;
        private const float RecoverAngleTolerance = 0.5f;
        private const float BaseDiveGetUpClipDuration = 2.8f;       // 80 frames @ 30 fps (Dive_GetUp)
        private const float BaseExplosionGetUpClipDuration = 1.8f;        // 54 frames @ 30 fps (Explosion_GetUp)
        private const float GetUpRunExitTime = 0.85f;                     // Transition exit time to Run in Animator

        // Input & camera
        private InputSystemActions _inputActions;
        private CinemachineOrbitalFollow _orbitalFollow;
        private CinemachineInputAxisController _axisController;
        private Transform _cameraTargetProxy;
        private PlayerAnimation _playerAnimation;
        private bool _isInitialized;

        // Locomotion
        private Vector2 _moveInput;
        private Vector3 _moveDir;
        private float _turnVelocity;
        private bool _isGrounded;

        // Vertical movement
        private float _verticalSpeed;
        private bool _isJumpPressed, _isJumping;

        // Dash
        private bool _isDashing, _canDash = true;
        private float _dashTimer, _dashCooldownTimer;
        private Vector3 _dashDir;

        // Dive
        public enum DivePhase
        {
            None,
            Airborne,
            Sliding,
            GettingUp
        }

        private DivePhase _divePhase = DivePhase.None;
        private bool _isDiving, _canDive = true, _isRecoveringRotation;
        private float _currentDiveSpeed, _currentDiveSlideSpeed, _diveTimer;
        private Vector3 _diveDir;

        // Explosion Knockback
        private enum KnockbackPhase
        {
            None,
            Airborne,
            Sliding,
            LyingStill,
            GettingUp
        }

        private KnockbackPhase _knockbackPhase = KnockbackPhase.None;
        private Vector3 _knockbackDir;
        private float _currentKnockbackVerticalSpeed;
        private float _currentSlideSpeed;
        private float _knockbackTimer;
        private float _airborneTime;

        // Bomb & Stats
        public readonly SyncVar<int> Kills = new SyncVar<int>();
        public readonly SyncVar<int> Deaths = new SyncVar<int>();
        public readonly SyncVar<bool> IsEliminated = new SyncVar<bool>();

        private readonly Collider[] _passOverlapResults = new Collider[16];

        #region Static Player Registry

        public static readonly System.Collections.Generic.List<PlayerController> AllPlayers = new System.Collections.Generic.List<PlayerController>(16);
        public static readonly System.Collections.Generic.Dictionary<int, PlayerController> PlayersByObjectId = new System.Collections.Generic.Dictionary<int, PlayerController>(16);
        public static PlayerController LocalInstance { get; private set; }

        private void RegisterPlayer()
        {
            if (!AllPlayers.Contains(this))
                AllPlayers.Add(this);

            if (ObjectId >= 0)
                PlayersByObjectId[ObjectId] = this;

            if (IsLocalDriver)
                LocalInstance = this;
        }

        private void UnregisterPlayer()
        {
            AllPlayers.Remove(this);

            if (ObjectId >= 0)
                PlayersByObjectId.Remove(ObjectId);

            if (LocalInstance == this)
                LocalInstance = null;
        }

        #endregion

        #region Public API

        /// <summary>True for the owning client driving local input and simulation.</summary>
        public bool IsLocalDriver => IsOwner;

        public bool IsGrounded => _isGrounded;
        public bool IsJumping => _isJumping;
        public bool IsFalling => !_isGrounded && _verticalSpeed < 0f && !_isDiving;
        public bool IsDiving => _isDiving;
        public bool IsDashing => _isDashing;
        public bool IsKnockedBack => _knockbackPhase != KnockbackPhase.None && _knockbackPhase != KnockbackPhase.GettingUp;
        public float MoveSpeed => moveSpeed;
        public float DiveGetUpAnimSpeed => diveGetUpAnimSpeed;
        public float ExplosionGetUpAnimSpeed => explosionGetUpAnimSpeed;
        public int Score => Kills.Value - Deaths.Value;

        public bool HasBomb => BombManager.Instance != null && BombManager.Instance.CurrentHolderObjectId.Value == ObjectId;

        /// <summary>Local move input magnitude (0-1). Blends into run when get-up reaches 85% exit time.</summary>
        public float MoveInputMagnitude
        {
            get
            {
                if (_knockbackPhase != KnockbackPhase.None)
                {
                    if (_knockbackPhase == KnockbackPhase.GettingUp)
                    {
                        float total = BaseExplosionGetUpClipDuration / Mathf.Max(0.1f, explosionGetUpAnimSpeed);
                        if (_knockbackTimer <= total * (1f - GetUpRunExitTime))
                            return _moveInput.magnitude;
                    }
                    return 0f;
                }

                if (_divePhase != DivePhase.None)
                {
                    if (_divePhase == DivePhase.GettingUp)
                    {
                        float total = BaseDiveGetUpClipDuration / Mathf.Max(0.1f, diveGetUpAnimSpeed);
                        if (_diveTimer <= total * (1f - GetUpRunExitTime))
                            return _moveInput.magnitude;
                    }
                    return 0f;
                }

                return _moveInput.magnitude;
            }
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            _playerAnimation = GetComponent<PlayerAnimation>();

            if (characterController == null)
                characterController = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            RegisterPlayer();
        }

        private void OnDisable()
        {
            UnregisterPlayer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            RegisterPlayer();

            if (IsOwner)
                InitializeLocalPlayer();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            UnregisterPlayer();
        }

        private void InitializeLocalPlayer()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            _inputActions = new InputSystemActions();
            _inputActions.Player.Enable();
            _inputActions.Player.Jump.started += OnJumpInput;
            _inputActions.Player.Dash.started += OnDashInput;

            SetupCamera();

            GameManager.OnControlModeChanged += HandleControlModeChanged;
            if (GameManager.Instance != null)
                HandleControlModeChanged(GameManager.Instance.UseTouchControls);
        }

        private void SetupCamera()
        {
            if (cam == null && Camera.main != null)
                cam = Camera.main.transform;

            if (freeLookCam == null)
                freeLookCam = FindFirstObjectByType<CinemachineCamera>();

            if (freeLookCam == null) return;

            _orbitalFollow = freeLookCam.GetComponent<CinemachineOrbitalFollow>();
            _axisController = freeLookCam.GetComponent<CinemachineInputAxisController>();

            _cameraTargetProxy = new GameObject($"CameraFollowTarget_{OwnerId}").transform;
            _cameraTargetProxy.position = transform.position;
            freeLookCam.Target.TrackingTarget = _cameraTargetProxy;
        }

        private void OnDestroy()
        {
            UnregisterPlayer();

            if (!_isInitialized) return;

            GameManager.OnControlModeChanged -= HandleControlModeChanged;

            _inputActions.Player.Disable();
            _inputActions.Dispose();

            if (_cameraTargetProxy != null)
                Destroy(_cameraTargetProxy.gameObject);
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            _isGrounded = CheckIsGrounded();

            if (_knockbackPhase != KnockbackPhase.None)
            {
                HandleKnockback();
                return;
            }

            if (IsEliminated.Value)
                return;

            if (_divePhase != DivePhase.None)
            {
                HandleDive();
                return;
            }

            if (_isDashing)
            {
                HandleDash();
                return;
            }

            if (IsLocalDriver)
            {
                HandleMovement();
                HandleDashCooldown();

                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    TryPassBomb();
                }
            }

            if (_isRecoveringRotation)
                RecoverRotation();
        }

        private void LateUpdate() => UpdateCameraTarget();

        private bool CheckIsGrounded()
        {
            if (characterController.isGrounded)
                return true;

            Vector3 origin = transform.position + Vector3.up * 0.15f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.35f, groundLayerMask, QueryTriggerInteraction.Ignore))
                return false;

            // Ignore our own colliders.
            return !hit.collider.transform.IsChildOf(transform);
        }

        #endregion

        #region Input

        private void HandleControlModeChanged(bool isTouchEnabled)
        {
            if (_axisController != null)
                _axisController.enabled = !isTouchEnabled;
        }

        private void OnJumpInput(InputAction.CallbackContext context)
        {
            if (!_isDiving && !_isDashing && _divePhase == DivePhase.None && _knockbackPhase == KnockbackPhase.None)
                _isJumpPressed = true;
        }

        private void OnDashInput(InputAction.CallbackContext context)
        {
            if (!_canDash || _isDashing || _isDiving || _divePhase != DivePhase.None || _knockbackPhase != KnockbackPhase.None) return;

            Vector3 dir = GetActionDirection();
            StartDash(dir);
            ServerDashRpc(dir);
        }

        /// <summary>Current move direction, or the facing direction when standing still.</summary>
        private Vector3 GetActionDirection()
        {
            return _moveDir.sqrMagnitude > MoveInputThreshold * MoveInputThreshold
                ? _moveDir.normalized
                : transform.forward;
        }

        #endregion

        #region Locomotion

        private void HandleMovement()
        {
            _moveInput = _inputActions.Player.Move.ReadValue<Vector2>();

            if (_moveInput.magnitude >= MoveInputThreshold)
            {
                float camY = cam != null ? cam.eulerAngles.y : 0f;
                float targetAngle = Mathf.Atan2(_moveInput.x, _moveInput.y) * Mathf.Rad2Deg + camY;

                if (!_isRecoveringRotation)
                {
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnVelocity, turningTime);
                    transform.rotation = Quaternion.Euler(0f, angle, 0f);
                }

                _moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            }
            else
            {
                _moveDir = Vector3.zero;
            }

            Vector3 velocity = _moveDir * moveSpeed + Vector3.up * UpdateVerticalSpeed();
            characterController.Move(velocity * Time.deltaTime);
        }

        /// <summary>Applies gravity, and resolves buffered jump input into a jump (grounded) or dive (airborne).</summary>
        private float UpdateVerticalSpeed()
        {
            if (_isGrounded)
            {
                if (_verticalSpeed < 0f)
                    _verticalSpeed = groundedGravity;

                _isJumping = false;
                if (_divePhase == DivePhase.None)
                    _canDive = true;

                if (_isJumpPressed)
                    StartJump();
            }
            else
            {
                float gravity = _verticalSpeed > 0f ? jumpGravity : fallGravity;
                _verticalSpeed += gravity * Time.deltaTime;

                if (_isJumping && _verticalSpeed < jumpHeight)
                    _isJumping = false;

                // A second jump press while airborne triggers a dive.
                if (_isJumpPressed && _canDive && _divePhase == DivePhase.None)
                    StartDive();
            }

            _isJumpPressed = false;
            return _verticalSpeed;
        }

        private void RecoverRotation()
        {
            Quaternion upright = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, upright, RecoverRotationRate * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, upright) < RecoverAngleTolerance)
                _isRecoveringRotation = false;
        }

        #endregion

        #region Jump

        private void StartJump()
        {
            _verticalSpeed = jumpHeight;
            _isJumping = true;
            _canDash = false;

            _playerAnimation?.PlayJumpAnimation();
            ServerJumpRpc();
        }

        [ServerRpc]
        private void ServerJumpRpc() => ObserversJumpRpc();

        [ObserversRpc(ExcludeOwner = true)]
        private void ObserversJumpRpc() => _playerAnimation?.PlayJumpAnimation();

        #endregion

        #region Dash

        private void StartDash(Vector3 dir)
        {
            _isDashing = true;
            _canDash = false;
            _dashTimer = dashDuration;
            _dashDir = dir;

            SetCameraDamping(dashDamping);
            _playerAnimation?.PlayDashAnimation();
        }

        private void HandleDash()
        {
            characterController.Move(_dashDir * dashSpeed * Time.deltaTime);

            _dashTimer -= Time.deltaTime;
            if (_dashTimer > 0f) return;

            _isDashing = false;
            _dashCooldownTimer = dashCooldown;
            SetCameraDamping(normalDamping);
        }

        private void HandleDashCooldown()
        {
            if (_canDash) return;

            _dashCooldownTimer -= Time.deltaTime;
            if (_dashCooldownTimer <= 0f && _isGrounded)
                _canDash = true;
        }

        [ServerRpc]
        private void ServerDashRpc(Vector3 dir) => ObserversDashRpc(dir);

        [ObserversRpc(ExcludeOwner = true)]
        private void ObserversDashRpc(Vector3 dir) => StartDash(dir);

        #endregion

        #region Dive

        private void StartDive()
        {
            Vector3 dir = GetActionDirection();
            BeginDive(dir);
            ServerDiveRpc(dir);
        }

        private void BeginDive(Vector3 dir)
        {
            _divePhase = DivePhase.Airborne;
            _isDiving = true;
            _canDive = false;
            _isJumpPressed = false;
            _isDashing = false;

            characterController.height = diveControllerHeight;

            _diveDir = dir;
            _currentDiveSpeed = 0f;

            _playerAnimation?.PlayDiveAnimation();
        }

        private void HandleDive()
        {
            switch (_divePhase)
            {
                case DivePhase.Airborne:
                    _currentDiveSpeed = Mathf.Lerp(_currentDiveSpeed, diveDistance, DiveSpeedLerpRate * Time.deltaTime);
                    _verticalSpeed += diveGravity * Time.deltaTime;

                    Vector3 airVelocity = _diveDir * _currentDiveSpeed + Vector3.up * _verticalSpeed;
                    characterController.Move(airVelocity * Time.deltaTime);

                    Quaternion pitched = Quaternion.LookRotation(_diveDir + Vector3.down * 0.5f);
                    transform.rotation = Quaternion.Lerp(transform.rotation, pitched, DiveRotationRate * Time.deltaTime);

                    if (CheckIsGrounded())
                    {
                        _divePhase = DivePhase.Sliding;
                        _currentDiveSlideSpeed = _currentDiveSpeed;
                        _diveTimer = diveSlideDuration;
                        _verticalSpeed = groundedGravity;
                    }
                    break;

                case DivePhase.Sliding:
                    _diveTimer -= Time.deltaTime;
                    float slideProgress = Mathf.Clamp01(1f - (_diveTimer / diveSlideDuration));
                    _currentDiveSlideSpeed = Mathf.Lerp(diveDistance, 0f, slideProgress);

                    Vector3 slideVelocity = (_diveDir * _currentDiveSlideSpeed) + (Vector3.up * groundedGravity);
                    characterController.Move(slideVelocity * Time.deltaTime);

                    Quaternion upright = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, upright, DiveRotationRate * Time.deltaTime);

                    if (_diveTimer <= 0f)
                    {
                        _isDiving = false;
                        _divePhase = DivePhase.GettingUp;
                        _diveTimer = BaseDiveGetUpClipDuration / Mathf.Max(0.1f, diveGetUpAnimSpeed);
                    }
                    break;

                case DivePhase.GettingUp:
                    _diveTimer -= Time.deltaTime;

                    if (IsLocalDriver && _inputActions != null)
                        _moveInput = _inputActions.Player.Move.ReadValue<Vector2>();

                    float totalDiveDuration = BaseDiveGetUpClipDuration / Mathf.Max(0.1f, diveGetUpAnimSpeed);
                    if (_diveTimer <= totalDiveDuration * (1f - GetUpRunExitTime) && _moveInput.magnitude >= MoveInputThreshold)
                    {
                        EndDive();
                        break;
                    }

                    characterController.Move(Vector3.up * (groundedGravity * Time.deltaTime));

                    if (_diveTimer <= 0f)
                    {
                        EndDive();
                    }
                    break;
            }
        }

        private void EndDive()
        {
            _divePhase = DivePhase.None;
            _isDiving = false;
            _canDive = true;
            _verticalSpeed = 0f;
            _isRecoveringRotation = true;
            characterController.height = normalControllerHeight;
        }

        [ServerRpc]
        private void ServerDiveRpc(Vector3 dir) => ObserversDiveRpc(dir);

        [ObserversRpc(ExcludeOwner = true)]
        private void ObserversDiveRpc(Vector3 dir) => BeginDive(dir);

        #endregion

        #region Camera

        private void SetCameraDamping(float value)
        {
            if (_orbitalFollow != null)
                _orbitalFollow.TrackerSettings.PositionDamping = Vector3.one * value;
        }

        /// <summary>Keeps the camera target within the dead zone around the player (local owner only).</summary>
        private void UpdateCameraTarget()
        {
            if (_cameraTargetProxy == null) return;

            if (!useDeadZone || (_isDashing && snapDeadZoneOnDash))
            {
                _cameraTargetProxy.position = transform.position;
                return;
            }

            Vector3 offset = _cameraTargetProxy.position - transform.position;
            offset.x = Mathf.Clamp(offset.x, -deadZoneSize.x, deadZoneSize.x);
            offset.y = Mathf.Clamp(offset.y, -deadZoneSize.y, deadZoneSize.y);
            offset.z = Mathf.Clamp(offset.z, -deadZoneSize.z, deadZoneSize.z);

            _cameraTargetProxy.position = transform.position + offset;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDeadZoneGizmos || !useDeadZone) return;

            Vector3 center = Application.isPlaying && _cameraTargetProxy != null
                ? _cameraTargetProxy.position
                : transform.position;

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(center, deadZoneSize * 2f);
        }

        #endregion

        #region Bomb & Stats Methods

        /// <summary>
        /// Called locally via Left Mouse Click or UI Pass Button to attempt passing the bomb.
        /// Scans for closest valid player in front within range and requests server validation.
        /// </summary>
        public void TryPassBomb()
        {
            if (!IsLocalDriver || IsEliminated.Value)
            {
                return;
            }

            if (BombManager.Instance == null)
            {
                Debug.LogWarning("[PlayerController] TryPassBomb failed: BombManager.Instance is null! Ensure [BombManager] exists in scene.");
                return;
            }

            if (!HasBomb)
            {
                return;
            }

            float searchRadius = BombManager.Instance.MaxPassDistance;
            float maxAngle = BombManager.Instance.MaxPassFOVAngle * 0.5f;

            PlayerController bestTarget = null;
            float closestDist = float.MaxValue;

            // 1. Try non-alloc physics query across configured player layers
            int count = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, _passOverlapResults, playerLayerMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider col = _passOverlapResults[i];
                if (col == null || col.transform.IsChildOf(transform)) continue;

                PlayerController targetPlayer = col.GetComponentInParent<PlayerController>();
                if (targetPlayer == null || targetPlayer.IsEliminated.Value || targetPlayer == this) continue;

                Vector3 toTarget = targetPlayer.transform.position - transform.position;
                toTarget.y = 0f; // Horizontal 2D distance
                float dist = toTarget.magnitude;
                if (dist > searchRadius) continue;

                Vector3 dir = dist > 0.001f ? toTarget / dist : transform.forward;
                float angle = Vector3.Angle(transform.forward, dir);

                if (angle <= maxAngle && dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = targetPlayer;
                }
            }

            // 2. Fallback: Search registered players directly if physics colliders missed
            if (bestTarget == null)
            {
                for (int i = 0; i < AllPlayers.Count; i++)
                {
                    PlayerController p = AllPlayers[i];
                    if (p == null || p == this || p.IsEliminated.Value) continue;

                    Vector3 toTarget = p.transform.position - transform.position;
                    toTarget.y = 0f;
                    float dist = toTarget.magnitude;
                    if (dist > searchRadius) continue;

                    Vector3 dir = dist > 0.001f ? toTarget / dist : transform.forward;
                    float angle = Vector3.Angle(transform.forward, dir);

                    if (angle <= maxAngle && dist < closestDist)
                    {
                        closestDist = dist;
                        bestTarget = p;
                    }
                }
            }

            if (bestTarget != null)
            {
                BombManager.Instance.ServerRequestPassBombRpc(ObjectId, bestTarget.ObjectId);
            }
        }

        [Server]
        public void AddKill()
        {
            Kills.Value++;
        }

        [Server]
        public void AddDeath()
        {
            Deaths.Value++;
        }

        [Server]
        public void ServerSetEliminated(bool eliminated)
        {
            IsEliminated.Value = eliminated;
            ObserversSetEliminatedRpc(eliminated);
        }

        [ObserversRpc]
        private void ObserversSetEliminatedRpc(bool eliminated)
        {
            if (eliminated)
            {
                TriggerKnockback();
            }
        }

        public void TriggerKnockback()
        {
            _knockbackPhase = KnockbackPhase.Airborne;
            _knockbackDir = -transform.forward;
            _currentKnockbackVerticalSpeed = knockbackVerticalForce;
            _airborneTime = 0f;
            _moveInput = Vector2.zero;
            _moveDir = Vector3.zero;
            _isJumpPressed = false;
            _isDashing = false;
            _isDiving = false;
            _canDive = false;
            _canDash = false;
            _divePhase = DivePhase.None;

            if (characterController != null)
            {
                characterController.enabled = true;
                characterController.height = normalControllerHeight;
            }

            _playerAnimation?.PlayExplosionAnimation(true);
        }

        private void HandleKnockback()
        {
            switch (_knockbackPhase)
            {
                case KnockbackPhase.Airborne:
                    _airborneTime += Time.deltaTime;
                    _currentKnockbackVerticalSpeed += knockbackGravity * Time.deltaTime;

                    if (IsLocalDriver)
                    {
                        Vector3 velocity = (_knockbackDir * knockbackHorizontalSpeed) + (Vector3.up * _currentKnockbackVerticalSpeed);
                        characterController.Move(velocity * Time.deltaTime);
                    }

                    // Check for ground impact after a brief initial liftoff window (0.15s)
                    if (_airborneTime > 0.15f && _currentKnockbackVerticalSpeed < 0f && CheckIsGrounded())
                    {
                        _knockbackPhase = KnockbackPhase.Sliding;
                        _currentSlideSpeed = slideInitialSpeed;
                        _knockbackTimer = slideDuration;
                    }
                    break;

                case KnockbackPhase.Sliding:
                    _knockbackTimer -= Time.deltaTime;
                    _currentSlideSpeed = Mathf.MoveTowards(_currentSlideSpeed, 0f, slideDeceleration * Time.deltaTime);

                    if (IsLocalDriver)
                    {
                        Vector3 velocity = (_knockbackDir * _currentSlideSpeed) + (Vector3.up * groundedGravity);
                        characterController.Move(velocity * Time.deltaTime);
                    }

                    if (_knockbackTimer <= 0f)
                    {
                        _knockbackPhase = KnockbackPhase.LyingStill;
                        _knockbackTimer = lyingStillDuration;
                    }
                    break;

                case KnockbackPhase.LyingStill:
                    _knockbackTimer -= Time.deltaTime;

                    if (IsLocalDriver)
                    {
                        characterController.Move(Vector3.up * (groundedGravity * Time.deltaTime));
                    }

                    if (_knockbackTimer <= 0f)
                    {
                        _knockbackPhase = KnockbackPhase.GettingUp;
                        _knockbackTimer = BaseExplosionGetUpClipDuration / Mathf.Max(0.1f, explosionGetUpAnimSpeed);

                        // KnockedBack = false triggers transition from Explosion_End to Explosion_GetUp in Animator
                        _playerAnimation?.PlayExplosionAnimation(false);
                    }
                    break;

                case KnockbackPhase.GettingUp:
                    _knockbackTimer -= Time.deltaTime;

                    if (IsLocalDriver && _inputActions != null)
                        _moveInput = _inputActions.Player.Move.ReadValue<Vector2>();

                    float totalKnockbackDuration = BaseExplosionGetUpClipDuration / Mathf.Max(0.1f, explosionGetUpAnimSpeed);
                    if (_knockbackTimer <= totalKnockbackDuration * (1f - GetUpRunExitTime) && _moveInput.magnitude >= MoveInputThreshold)
                    {
                        EndKnockback();
                        break;
                    }

                    if (IsLocalDriver)
                    {
                        characterController.Move(Vector3.up * (groundedGravity * Time.deltaTime));
                    }

                    if (_knockbackTimer <= 0f)
                    {
                        EndKnockback();
                    }
                    break;
            }
        }

        public void EndKnockback()
        {
            _knockbackPhase = KnockbackPhase.None;
            _verticalSpeed = 0f;
            _canDive = true;
            _canDash = true;
            _isRecoveringRotation = true;
            _playerAnimation?.PlayExplosionAnimation(false);

            if (IsServerStarted)
            {
                IsEliminated.Value = false;
            }
            else if (IsOwner)
            {
                ServerClearEliminatedRpc();
            }
        }

        [ServerRpc]
        private void ServerClearEliminatedRpc()
        {
            IsEliminated.Value = false;
        }

        [Server]
        public void ServerRespawnAt(Vector3 position)
        {
            ServerSetEliminated(false);
            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position = position;
                characterController.enabled = true;
            }
            else
            {
                transform.position = position;
            }
            ObserversRespawnAtRpc(position);
        }

        [ObserversRpc]
        private void ObserversRespawnAtRpc(Vector3 position)
        {
            gameObject.SetActive(true);
            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position = position;
                characterController.enabled = true;
            }
            else
            {
                transform.position = position;
            }
            _playerAnimation?.PlayExplosionAnimation(false);
        }

        #endregion
    }
}