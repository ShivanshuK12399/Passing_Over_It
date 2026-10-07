using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;

namespace PassingOverIt.Player
{
    /// <summary>
    /// Synchronized multiplayer player controller using Fish-Net.
    /// Handles local player input, locomotion, jump, dash, dive, and Cinemachine camera setup.
    /// Remote players receive position/rotation via NetworkTransform and actions via RPCs.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public float turningTime = 0.1f;
        public float jumpHeight = 20f;
        public float jumpGravity = -80f;
        public float fallGravity = -100f;

        [Header("Dash Settings")]
        public float dashSpeed = 20f;
        public float dashDuration = 0.3f;
        public float dashCooldown = 1.5f;

        [Header("Dive Settings")]
        public float diveHeight = 8f;
        public float diveDistance = 12f;
        public float slipDistance = 0.35f;
        public float diveGravity = -20f;

        [Header("Controller & Physics Settings")]
        public float normalControllerHeight = 2f;
        public float diveControllerHeight = 1f;
        [SerializeField] private float groundedGravity = -10f;
        [SerializeField] private LayerMask groundLayerMask = ~0;

        [Header("Camera Settings")]
        public CinemachineCamera freeLookCam;
        public float normalDamping = 1f;
        public float dashDamping = 0f;

        [Header("Camera Dead Zone Settings")]
        public bool useDeadZone = true;
        [Tooltip("Half-dimensions of dead zone (X: Horizontal, Y: Vertical, Z: Depth).")]
        public Vector3 deadZoneSize = new Vector3(2f, 1.5f, 2f);
        [Tooltip("If true, snaps camera target immediately to player when dashing.")]
        public bool snapDeadZoneOnDash = true;
        [Tooltip("Show dead zone bounds gizmo in Scene View when selected.")]
        public bool showDeadZoneGizmos = true;

        [Header("References")]
        public CharacterController characterController;
        public Transform cam;

        // Input & Camera references
        private InputSystemActions _inputActions;
        private CinemachineOrbitalFollow _orbitalFollow;
        private Transform _cameraTargetProxy;
        private PlayerAnimation _playerAnimation;

        // Locomotion state
        private float _turnVelocity;
        private Vector3 _move, _moveDir;

        // Jump & Vertical Movement state
        private bool _isJumpPressed, _isJumping;
        private Vector3 _verticalVelocity;
        private float _airTime = 0f;
        private const float DiveAirTimeThreshold = 0.2f;

        // Dash state
        private bool _isDashing = false, _canDash = true;
        private float _dashTimer = 0f, _dashCooldownTimer = 0f;
        private Vector3 _dashDir;

        // Dive state
        private bool _isDiving = false, _canDive = true, _isRecoveringRotation = false;
        private float _slipTimer = 0f, _currentDiveSpeed = 0f;
        private Vector3 _diveDir;

        private bool _isInitialized = false;

        #region Public API Properties

        /// <summary>
        /// Returns true if this instance is driven locally (local network owner or offline single-player).
        /// </summary>
        public bool IsLocalDriver => NetworkObject == null || !NetworkObject.IsSpawned || IsOwner;

        /// <summary>
        /// Returns true if character is standing on ground (combines CharacterController.isGrounded with Raycast fallback).
        /// </summary>
        public bool IsGrounded => CheckIsGrounded();

        /// <summary>
        /// Magnitude of the local movement input vector (0.0 to 1.0).
        /// </summary>
        public float MoveInputMagnitude => _move.magnitude;

        /// <summary>
        /// Returns true if player is currently in rising jump state.
        /// </summary>
        public bool IsJumping => _isJumping;

        /// <summary>
        /// Returns true if player is falling airborne.
        /// </summary>
        public bool IsFalling => !CheckIsGrounded() && _verticalVelocity.y < 0f && !_isDiving;

        /// <summary>
        /// Returns true if player is performing a dive.
        /// </summary>
        public bool IsDiving => _isDiving;

        /// <summary>
        /// Returns true if player is performing a dash.
        /// </summary>
        public bool IsDashing => _isDashing;

        #endregion

        #region Component Lifecycle

        private void Awake()
        {
            _playerAnimation = GetComponent<PlayerAnimation>();

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }
        }

        private void Start()
        {
            // Fallback for offline test scenes
            if (NetworkObject == null || !NetworkObject.IsSpawned)
            {
                InitializeLocalPlayer();
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
            {
                InitializeLocalPlayer();
            }
        }

        private void InitializeLocalPlayer()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            _inputActions = new InputSystemActions();
            _inputActions.Player.Enable();

            _inputActions.Player.Jump.started += OnJumpInput;
            _inputActions.Player.Jump.canceled += OnJumpInput;
            _inputActions.Player.Dash.started += OnDashInput;

            if (cam == null && Camera.main != null)
            {
                cam = Camera.main.transform;
            }

            if (freeLookCam == null)
            {
                freeLookCam = FindFirstObjectByType<CinemachineCamera>();
            }

            if (freeLookCam != null)
            {
                _orbitalFollow = freeLookCam.GetComponent<CinemachineOrbitalFollow>();

                int id = NetworkObject != null && NetworkObject.IsSpawned ? OwnerId : 0;
                GameObject targetObj = new GameObject($"CameraFollowTarget_{id}");
                _cameraTargetProxy = targetObj.transform;
                _cameraTargetProxy.position = transform.position;

                freeLookCam.Target.TrackingTarget = _cameraTargetProxy;
            }

            GameManager.OnControlModeChanged += HandleControlModeChanged;
            if (GameManager.Instance != null)
            {
                HandleControlModeChanged(GameManager.Instance.UseTouchControls);
            }
        }

        private void OnDestroy()
        {
            if (!_isInitialized) return;

            GameManager.OnControlModeChanged -= HandleControlModeChanged;

            if (_inputActions != null)
            {
                _inputActions.Player.Disable();
                _inputActions.Dispose();
            }

            if (_cameraTargetProxy != null)
            {
                Destroy(_cameraTargetProxy.gameObject);
            }
        }

        #endregion

        #region Physics & Ground Detection

        private bool CheckIsGrounded()
        {
            if (characterController != null && characterController.isGrounded)
                return true;

            Vector3 origin = transform.position + Vector3.up * 0.15f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.35f, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                Transform hitTransform = hit.collider.transform;
                if (hitTransform != transform && !hitTransform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Update Loops

        private void Update()
        {
            _airTime = CheckIsGrounded() ? 0f : _airTime + Time.deltaTime;

            if (_isDiving)
            {
                HandleDive();
                return;
            }

            if (_isDashing)
            {
                DashMovement();
                return;
            }

            if (IsLocalDriver)
            {
                HandleMovement();
                HandleDashCooldown();
            }

            if (_isRecoveringRotation)
            {
                RecoverRotation();
            }
        }

        private void LateUpdate()
        {
            if (IsLocalDriver)
            {
                UpdateCameraDeadZone();
            }
        }

        #endregion

        #region Locomotion & Input Handling

        private void HandleControlModeChanged(bool isTouchEnabled)
        {
            if (!IsLocalDriver || freeLookCam == null) return;

            CinemachineInputAxisController axisController = freeLookCam.GetComponent<CinemachineInputAxisController>();
            if (axisController != null)
            {
                axisController.enabled = !isTouchEnabled;
            }
        }

        private void OnJumpInput(InputAction.CallbackContext context)
        {
            if (!IsLocalDriver || !context.ReadValueAsButton()) return;

            if (!_isDiving && !_isDashing)
            {
                _isJumpPressed = true;
            }
        }

        private void OnDashInput(InputAction.CallbackContext context)
        {
            if (!IsLocalDriver || !_canDash || _isDashing) return;

            Vector3 chosenDir = _moveDir.magnitude > 0.1f ? _moveDir.normalized : transform.forward;
            StartDashLocal(chosenDir);

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                ServerDashRpc(chosenDir);
            }
        }

        private void HandleMovement()
        {
            if (_inputActions == null) return;

            Vector2 input = _inputActions.Player.Move.ReadValue<Vector2>();
            _move = new Vector3(input.x, 0, input.y);

            float camY = cam != null ? cam.eulerAngles.y : 0f;
            float targetAngle = Mathf.Atan2(_move.x, _move.z) * Mathf.Rad2Deg + camY;

            if (_move.magnitude >= 0.1f)
            {
                if (!_isRecoveringRotation)
                {
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnVelocity, turningTime);
                    transform.rotation = Quaternion.Euler(0, angle, 0);
                }
                else
                {
                    RecoverRotation();
                }

                _moveDir = Quaternion.Euler(0, targetAngle, 0) * Vector3.forward;
            }
            else
            {
                _moveDir = Quaternion.Euler(0, targetAngle, 0) * Vector3.zero;
            }

            if (characterController != null)
            {
                characterController.Move((_moveDir * moveSpeed + HandleVerticalMovement()) * Time.deltaTime);
            }
        }

        private Vector3 HandleVerticalMovement()
        {
            bool grounded = CheckIsGrounded();

            if (grounded)
            {
                if (_verticalVelocity.y < 0f)
                    _verticalVelocity.y = groundedGravity;

                _isJumping = false;
                _canDive = true;

                if (_isJumpPressed && !_isDiving)
                {
                    StartJump();
                }

                _isJumpPressed = false;
            }
            else
            {
                float gravityToUse = _verticalVelocity.y > 0f ? jumpGravity : fallGravity;
                _verticalVelocity.y += gravityToUse * Time.deltaTime;

                if (_isJumping && _verticalVelocity.y < jumpHeight)
                {
                    _isJumping = false;
                }

                // Double pressing Jump (mid-air jump press) triggers Dive instantly
                if (_isJumpPressed && !_isDiving && _canDive)
                {
                    _isJumpPressed = false;
                    StartDive();
                }

                _isJumpPressed = false;
            }

            return _verticalVelocity;
        }

        #endregion

        #region Actions & RPC Synchronization

        private void StartJump()
        {
            _verticalVelocity.y = jumpHeight;
            _isJumping = true;
            _canDash = false;

            TriggerJumpAnimationLocal();

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                ServerJumpRpc();
            }
        }

        private void TriggerJumpAnimationLocal()
        {
            if (_playerAnimation != null)
            {
                _playerAnimation.PlayJumpAnimation();
            }
        }

        [ServerRpc]
        private void ServerJumpRpc()
        {
            ObserversJumpRpc();
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void ObserversJumpRpc()
        {
            TriggerJumpAnimationLocal();
        }

        private void StartDashLocal(Vector3 dir)
        {
            _isDashing = true;
            _canDash = false;
            _dashTimer = dashDuration;
            _dashDir = dir;

            if (IsLocalDriver)
            {
                SetCameraDamping(dashDamping);
            }
        }

        [ServerRpc]
        private void ServerDashRpc(Vector3 dir)
        {
            ObserversDashRpc(dir);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void ObserversDashRpc(Vector3 dir)
        {
            StartDashLocal(dir);
        }

        private void StartDive()
        {
            Vector3 chosenDir = _moveDir.magnitude > 0.1f ? _moveDir.normalized : transform.forward;
            StartDiveLocal(chosenDir);

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                ServerDiveRpc(chosenDir);
            }
        }

        private void StartDiveLocal(Vector3 dir)
        {
            _isDiving = true;
            _canDive = false;
            _isJumpPressed = false;

            if (characterController != null)
            {
                characterController.height = diveControllerHeight;
            }

            _diveDir = dir;
            _slipTimer = slipDistance;
            _currentDiveSpeed = 0f;
        }

        [ServerRpc]
        private void ServerDiveRpc(Vector3 dir)
        {
            ObserversDiveRpc(dir);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void ObserversDiveRpc(Vector3 dir)
        {
            StartDiveLocal(dir);
        }

        private void DashMovement()
        {
            if (characterController != null)
            {
                characterController.Move(_dashDir * dashSpeed * Time.deltaTime);
            }

            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
            {
                _isDashing = false;
                _dashCooldownTimer = dashCooldown;
                if (IsOwner)
                {
                    SetCameraDamping(normalDamping);
                }
            }
        }

        private void HandleDive()
        {
            if (characterController == null) return;

            if (!CheckIsGrounded())
            {
                _currentDiveSpeed = Mathf.Lerp(_currentDiveSpeed, diveDistance, 6f * Time.deltaTime);
                _verticalVelocity.y += diveGravity * Time.deltaTime;

                Vector3 moveVec = (_diveDir * _currentDiveSpeed * Time.deltaTime) + (_verticalVelocity * Time.deltaTime);
                characterController.Move(moveVec);

                transform.rotation = Quaternion.Lerp(transform.rotation,
                    Quaternion.LookRotation(_diveDir + Vector3.down * 0.5f),
                    8f * Time.deltaTime);

                return;
            }

            if (_slipTimer > 0f)
            {
                characterController.Move(_diveDir * slipDistance * Time.deltaTime);
                _slipTimer -= Time.deltaTime;
                return;
            }

            if (_slipTimer <= 0f)
            {
                _isDiving = false;
                _canDive = true;
                _isDashing = false;
                _verticalVelocity = Vector3.zero;
                characterController.height = normalControllerHeight;
                _isRecoveringRotation = true;
            }
        }

        private void RecoverRotation()
        {
            Quaternion targetRot = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 20f * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, targetRot) < 0.5f)
                _isRecoveringRotation = false;
        }

        private void HandleDashCooldown()
        {
            if (_canDash) return;

            if (_dashCooldownTimer > 0)
            {
                _dashCooldownTimer -= Time.deltaTime;
            }

            if (_dashCooldownTimer <= 0f && CheckIsGrounded())
            {
                _canDash = true;
            }
        }

        #endregion

        #region Camera & Gizmos

        private void SetCameraDamping(float value)
        {
            if (_orbitalFollow != null)
            {
                _orbitalFollow.TrackerSettings.PositionDamping = new Vector3(value, value, value);
            }
        }

        private void UpdateCameraDeadZone()
        {
            if (_cameraTargetProxy == null) return;

            if (!useDeadZone || (_isDashing && snapDeadZoneOnDash))
            {
                _cameraTargetProxy.position = transform.position;
                return;
            }

            Vector3 playerPos = transform.position;
            Vector3 targetPos = _cameraTargetProxy.position;

            float deltaX = playerPos.x - targetPos.x;
            if (Mathf.Abs(deltaX) > deadZoneSize.x)
            {
                targetPos.x = playerPos.x - Mathf.Sign(deltaX) * deadZoneSize.x;
            }

            float deltaY = playerPos.y - targetPos.y;
            if (Mathf.Abs(deltaY) > deadZoneSize.y)
            {
                targetPos.y = playerPos.y - Mathf.Sign(deltaY) * deadZoneSize.y;
            }

            float deltaZ = playerPos.z - targetPos.z;
            if (Mathf.Abs(deltaZ) > deadZoneSize.z)
            {
                targetPos.z = playerPos.z - Mathf.Sign(deltaZ) * deadZoneSize.z;
            }

            _cameraTargetProxy.position = targetPos;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDeadZoneGizmos || !useDeadZone) return;

            Vector3 center = Application.isPlaying && _cameraTargetProxy != null ? _cameraTargetProxy.position : transform.position;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(center, deadZoneSize * 2f);
        }

        #endregion
    }
}