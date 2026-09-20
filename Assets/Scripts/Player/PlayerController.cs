using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace PassingOverIt.Player
{
    /// <summary>
    /// Synchronized multiplayer player controller using Fish-Net.
    /// Handles local player input, movement, dash, dive, and local Cinemachine camera setup.
    /// Remote players receive position/rotation updates automatically via NetworkTransform.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement Variables")]
        public float moveSpeed = 5f;
        public float turningTime = 0.1f;
        public float jumpHeight = 20f;
        public float jumpGravity = -80f;
        public float fallGravity = -100f;

        [Header("Dash Variables")]
        public float dashSpeed = 20f;
        public float dashDuration = 0.3f;
        public float dashCooldown = 1.5f;

        [Header("Dive Variables")]
        public float diveHeight = 8f;       // how high the dive arc goes
        public float diveDistance = 12f;    // forward velocity
        public float slipDistance = 0.35f;  // sliding distance after landing
        public float diveGravity = -20f;

        [Header("Controller Settings")]
        public float normalControllerHeight = 2f;
        public float diveControllerHeight = 1f;

        [Header("Camera Settings")]
        public CinemachineCamera freeLookCam;
        public float normalDamping = 1f;     // default smooth follow
        public float dashDamping = 0f;       // no delay (snaps instantly)

        [Header("Camera Dead Zone Settings")]
        public bool useDeadZone = true;
        [Tooltip("Half-dimensions of dead zone (X: Horizontal, Y: Vertical, Z: Depth). Camera will not follow player while inside this area.")]
        public Vector3 deadZoneSize = new Vector3(2f, 1.5f, 2f);
        [Tooltip("If true, snaps camera target immediately to player when dashing.")]
        public bool snapDeadZoneOnDash = true;
        [Tooltip("Show dead zone bounds gizmo in Scene View when selected.")]
        public bool showDeadZoneGizmos = true;

        [Header("References")]
        public CharacterController characterController;
        public Transform cam;
        
        private InputSystemActions inputActions;
        private CinemachineOrbitalFollow orbitalFollow;
        private Transform cameraTargetProxy;

        // movement variables
        private float turnVelocity;
        private Vector3 move, moveDir;

        // jump & gravity variables
        private float groundedGravity = -1f;
        private bool isJumpPressed, isJumping;
        private Vector3 verticalVelocity;

        // dash variables
        private bool isDashing = false, canDash = true;
        private float dashTimer = 0f, dashCooldownTimer = 0f;
        private Vector3 dashDir;

        // dive variables
        private bool isDiving = false, canDive = true, isRecoveringRotation = false;
        private float slipTimer = 0f, currentDiveSpeed = 0f;
        private Vector3 diveDir;

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Setup input and camera ONLY for the local owning player
            if (IsOwner)
            {
                inputActions = new InputSystemActions();
                inputActions.Player.Enable();

                inputActions.Player.Jump.started += OnJump;
                inputActions.Player.Jump.canceled += OnJump;
                inputActions.Player.Dash.started += OnDash;

                // Find main camera if not set
                if (cam == null && Camera.main != null)
                {
                    cam = Camera.main.transform;
                }

                // Find Cinemachine camera in scene if not assigned
                if (freeLookCam == null)
                {
                    freeLookCam = FindFirstObjectByType<CinemachineCamera>();
                }

                if (freeLookCam != null)
                {
                    orbitalFollow = freeLookCam.GetComponent<CinemachineOrbitalFollow>();

                    // Setup local camera target proxy for camera dead zone
                    GameObject targetObj = new GameObject($"CameraFollowTarget_{OwnerId}");
                    cameraTargetProxy = targetObj.transform;
                    cameraTargetProxy.position = transform.position;

                    freeLookCam.Target.TrackingTarget = cameraTargetProxy;
                }

                GameManager.OnControlModeChanged += HandleControlModeChanged;
                if (GameManager.Instance != null)
                {
                    HandleControlModeChanged(GameManager.Instance.UseTouchControls);
                }
            }
        }

        private void OnDestroy()
        {
            if (IsOwner)
            {
                GameManager.OnControlModeChanged -= HandleControlModeChanged;

                if (inputActions != null)
                {
                    inputActions.Player.Disable();
                    inputActions.Dispose();
                }

                if (cameraTargetProxy != null)
                {
                    Destroy(cameraTargetProxy.gameObject);
                }
            }
        }

        private void HandleControlModeChanged(bool isTouchEnabled)
        {
            if (!IsOwner) return;

            if (freeLookCam != null)
            {
                CinemachineInputAxisController axisController = freeLookCam.GetComponent<CinemachineInputAxisController>();
                if (axisController != null)
                {
                    axisController.enabled = !isTouchEnabled;
                }
            }
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            if (!IsOwner || !context.ReadValueAsButton()) return;

            if (characterController.isGrounded)
            {
                if (!isJumping && !isDiving)
                {
                    verticalVelocity.y = jumpHeight;
                    isJumping = true;
                    canDash = false;
                }
            }
            else
            {
                if (!isDiving && canDive)
                {
                    StartDive();
                }
            }
        }

        private void OnDash(InputAction.CallbackContext context)
        {
            if (!IsOwner || !canDash || isDashing) return;

            Vector3 chosenDir = moveDir.magnitude > 0.1f ? moveDir.normalized : transform.forward;
            StartDashLocal(chosenDir);
            ServerDashRpc(chosenDir);
        }

        private void StartDashLocal(Vector3 dir)
        {
            isDashing = true;
            canDash = false;
            dashTimer = dashDuration;
            dashDir = dir;

            if (IsOwner)
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
            Vector3 chosenDir = moveDir.magnitude > 0.1f ? moveDir.normalized : transform.forward;
            StartDiveLocal(chosenDir);
            ServerDiveRpc(chosenDir);
        }

        private void StartDiveLocal(Vector3 dir)
        {
            isDiving = true;
            canDive = false;
            isJumpPressed = false;

            if (characterController != null)
            {
                characterController.height = diveControllerHeight;
            }

            diveDir = dir;
            slipTimer = slipDistance;
            currentDiveSpeed = 0f;
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

        private void Update()
        {
            // Dive logic runs for owner & observers to match visual movement
            if (isDiving)
            {
                HandleDive();
                return;
            }

            if (isDashing)
            {
                DashMovement();
                return;
            }

            // Only local owner reads input & calculates autonomous movement
            if (IsOwner)
            {
                HandleMovement();
                HandleDashCooldown();
            }

            if (isRecoveringRotation)
            {
                RecoverRotation();
            }
        }

        private void HandleMovement()
        {
            if (inputActions == null) return;

            Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();

            move = new Vector3(input.x, 0, input.y);

            float camY = cam != null ? cam.eulerAngles.y : 0f;
            float targetAngle = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg + camY;

            if (move.magnitude >= 0.1f)
            {
                if (!isRecoveringRotation)
                {
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelocity, turningTime);
                    transform.rotation = Quaternion.Euler(0, angle, 0);
                }
                else
                {
                    RecoverRotation();
                }

                moveDir = Quaternion.Euler(0, targetAngle, 0) * Vector3.forward;
            }
            else
            {
                moveDir = Quaternion.Euler(0, targetAngle, 0) * Vector3.zero;
            }

            if (characterController != null)
            {
                characterController.Move((moveDir * moveSpeed + HandleVerticalMovement()) * Time.deltaTime);
            }
        }

        private Vector3 HandleVerticalMovement()
        {
            if (characterController.isGrounded)
            {
                if (verticalVelocity.y < 0)
                    verticalVelocity.y = groundedGravity;

                if (isJumpPressed && !isJumping && !isDiving)
                {
                    verticalVelocity.y = jumpHeight;
                    isJumpPressed = false;
                    isJumping = true;
                }
                else
                {
                    isJumpPressed = false;
                    isJumping = false;
                }
            }
            else
            {
                float gravityToUse = verticalVelocity.y > 0 ? jumpGravity : fallGravity;
                verticalVelocity.y += gravityToUse * Time.deltaTime;
            }

            return verticalVelocity;
        }

        private void DashMovement()
        {
            if (characterController != null)
            {
                characterController.Move(dashDir * dashSpeed * Time.deltaTime);
            }

            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
                dashCooldownTimer = dashCooldown;
                if (IsOwner)
                {
                    SetCameraDamping(normalDamping);
                }
            }
        }

        private void HandleDive()
        {
            if (characterController == null) return;

            if (!characterController.isGrounded)
            {
                currentDiveSpeed = Mathf.Lerp(currentDiveSpeed, diveDistance, 6f * Time.deltaTime);
                verticalVelocity.y += diveGravity * Time.deltaTime;

                Vector3 moveVec = (diveDir * currentDiveSpeed * Time.deltaTime) + (verticalVelocity * Time.deltaTime);
                characterController.Move(moveVec);

                transform.rotation = Quaternion.Lerp(transform.rotation,
                    Quaternion.LookRotation(diveDir + Vector3.down * 0.5f),
                    8f * Time.deltaTime);

                return;
            }

            if (slipTimer > 0f)
            {
                characterController.Move(diveDir * slipDistance * Time.deltaTime);
                slipTimer -= Time.deltaTime;
                return;
            }

            if (slipTimer <= 0f)
            {
                isDiving = false;
                canDive = true;
                isDashing = false;
                verticalVelocity = Vector3.zero;
                characterController.height = normalControllerHeight;
                isRecoveringRotation = true;
            }
        }

        private void RecoverRotation()
        {
            Quaternion targetRot = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 20f * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, targetRot) < 0.5f)
                isRecoveringRotation = false;
        }

        private void HandleDashCooldown()
        {
            if (canDash) return;

            if (dashCooldownTimer > 0)
            {
                dashCooldownTimer -= Time.deltaTime;
            }

            if (dashCooldownTimer <= 0f && characterController != null && characterController.isGrounded)
            {
                canDash = true;
            }
        }

        private void SetCameraDamping(float value)
        {
            if (orbitalFollow != null)
            {
                orbitalFollow.TrackerSettings.PositionDamping = new Vector3(value, value, value);
            }
        }

        private void LateUpdate()
        {
            if (IsOwner)
            {
                UpdateCameraDeadZone();
            }
        }

        private void UpdateCameraDeadZone()
        {
            if (cameraTargetProxy == null) return;

            if (!useDeadZone || (isDashing && snapDeadZoneOnDash))
            {
                cameraTargetProxy.position = transform.position;
                return;
            }

            Vector3 playerPos = transform.position;
            Vector3 targetPos = cameraTargetProxy.position;

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

            cameraTargetProxy.position = targetPos;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDeadZoneGizmos || !useDeadZone) return;

            Vector3 center = Application.isPlaying && cameraTargetProxy != null ? cameraTargetProxy.position : transform.position;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(center, deadZoneSize * 2f);
        }
    }
}