using NUnit.Framework.Constraints;
using System.Collections;
using System.Threading;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Variables")]
    public float moveSpeed = 5f;
    public float turningTime = 0.1f;
    public float jumpHeight = 20f;


    [Header("Dash Variables")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.3f;
    public float dashCooldown = 1.5f;


    [Header("Dive Variables")]
    public float diveHeight = 8f;       // how high the dive arc goes
    public float diveDistance = 12f;    // forward velocity
    public float slipDistance = 0.35f;     // sliding distance after landing
    public float diveGravity = -20; 


    [Header("Controller Settings")]
    public float normalControllerHeight = 2f;
    public float diveControllerHeight = 1f;


    [Header("Camera Settings")]
    public CinemachineCamera freeLookCam;
    public float normalDamping = 1f;     // default smooth follow
    public float dashDamping = 0f;       // no delay (snaps instantly)


    [Header("Refrences")]
    public CharacterController characterController;
    public Transform cam;
    InputSystemActions inputActions;
    CinemachineOrbitalFollow orbitalFollow;

    // movement variables
    float turnVelocity;
     Vector3 move, moveDir;

    // jump & gravity variables
     float jumpGravity = -80f, fallGravity = -100f, groundedGravity=-1f; //fixedUpdate var jumpGravity = -1f, fallGravity = -8f, groundedGravity=-0.05f;
     bool isJumpPressed, isJumping;
     Vector3 verticalVelocity;

    // dash variables
    bool isDashing = false, canDash = true;
    float dashTimer = 0f, dashCooldownTimer = 0f;
    Vector3 dashDir;

    // dive variables
    bool isDiving = false, canDive = true, isRecoveringRotation = false;
    float slipTimer = 0f, currentDiveSpeed = 0f; // gradually increases for smooth push

    Vector3 diveDir;


    void Start()
    {
        inputActions = new InputSystemActions();
        inputActions.Player.Enable();

        inputActions.Player.Jump.started += OnJump;
        inputActions.Player.Jump.canceled += OnJump;
        inputActions.Player.Dash.started += OnDash;

        orbitalFollow = freeLookCam.GetComponent<CinemachineOrbitalFollow>();

        GameManager.OnControlModeChanged += HandleControlModeChanged;
        if (GameManager.Instance != null)
        {
            HandleControlModeChanged(GameManager.Instance.UseTouchControls);
        }
    }

    private void OnDestroy()
    {
        GameManager.OnControlModeChanged -= HandleControlModeChanged;
    }

    private void HandleControlModeChanged(bool isTouchEnabled)
    {
        if (freeLookCam != null)
        {
            CinemachineInputAxisController axisController = freeLookCam.GetComponent<CinemachineInputAxisController>();
            if (axisController != null)
            {
                axisController.enabled = !isTouchEnabled;
            }
        }
    }

    void OnJump(InputAction.CallbackContext context)
    {
        if (!context.ReadValueAsButton()) return;

        if (GameManager.Instance != null)
        {
            bool isKeyboardDevice = context.control?.device is Keyboard;
            if (GameManager.Instance.UseTouchControls && isKeyboardDevice) return;
            if (!GameManager.Instance.UseTouchControls && !isKeyboardDevice) return;
        }

        if (characterController.isGrounded)
        {
            if (!isJumping && !isDiving)
            {
                // jump instantly
                verticalVelocity.y = jumpHeight;
                isJumping = true;
                canDash = false;
            }
        }
        else
        {
            // in mid-air: trigger dive
            if (!isDiving && canDive)
            {
                StartDive();
            }
        }
    }

    void OnDash(InputAction.CallbackContext context)
    {
        if (!canDash || isDashing) return; // can only dash if available and not already dashing

        if (GameManager.Instance != null)
        {
            bool isKeyboardOrMouseDevice = context.control?.device is Keyboard || context.control?.device is Mouse;
            if (GameManager.Instance.UseTouchControls && isKeyboardOrMouseDevice) return;
            if (!GameManager.Instance.UseTouchControls && !isKeyboardOrMouseDevice) return;
        }

        isDashing = true;
        canDash = false;
        dashTimer = dashDuration;

        // dash in move direction or facing direction if idle
        dashDir = moveDir.magnitude > 0.1f ? moveDir.normalized : transform.forward;
        SetCameraDamping(dashDamping); // set camera damping on dashing
    }

    
    void Update()
    {
        // priority order: dive > dash > normal
        if (isDiving)
        {
            HandleDive();
            return;
        }

        if (isDashing) // dont take input while dashing
        {
            DashMovement();
            return;
        }

        HandleMovement();
        HandleDashCooldown();

        if (isRecoveringRotation)
        {
            RecoverRoation();
        }
    }

    void HandleMovement()
    {
        // taking input
        Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();

        if (GameManager.Instance != null)
        {
            var activeControl = inputActions.Player.Move.activeControl;
            if (activeControl != null)
            {
                bool isKeyboardDevice = activeControl.device is Keyboard;
                if (GameManager.Instance.UseTouchControls && isKeyboardDevice)
                {
                    input = Vector2.zero;
                }
                else if (!GameManager.Instance.UseTouchControls && !isKeyboardDevice)
                {
                    input = Vector2.zero;
                }
            }
        }

        move = new Vector3(input.x, 0, input.y);

        float targetAngle = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
        if (move.magnitude >= 0.1f)
        {
            if (!isRecoveringRotation) // after a dive, recover rotation first
            {
                // calculating rotation
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelocity, turningTime);
                transform.rotation = Quaternion.Euler(0, angle, 0);
            }
            else
            {
                RecoverRoation();
            }

            // movement direction if input given
            moveDir = Quaternion.Euler(0, targetAngle, 0) * Vector3.forward;
        }
        else moveDir = Quaternion.Euler(0, targetAngle, 0) * Vector3.zero;

        characterController.Move((moveDir * moveSpeed + HandleVerticalMovement()) * Time.deltaTime);
    }

    Vector3 HandleVerticalMovement()
    {
        if (characterController.isGrounded)
        {
            // Reset vertical velocity when grounded
            if (verticalVelocity.y < 0)
                verticalVelocity.y = groundedGravity;

            // Jump logic
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
            // Apply gravity
            float gravityToUse = verticalVelocity.y > 0 ? jumpGravity : fallGravity;
            verticalVelocity.y += gravityToUse * Time.deltaTime;
        }

        return verticalVelocity;
    }

    void DashMovement()
    {
        characterController.Move(dashDir * dashSpeed * Time.deltaTime);

        dashTimer -= Time.deltaTime;
        if (dashTimer <= 0f)
        {
            isDashing = false;
            dashCooldownTimer = dashCooldown; // start cooldown timer
            SetCameraDamping(normalDamping); // reset camera damping
        }
    }

    void StartDive()
    {
        isDiving = true;
        canDive = false;
        isJumpPressed = false;

        // setting controller height during dive
        characterController.height = diveControllerHeight;

        diveDir = moveDir.magnitude > 0.1f ? moveDir.normalized : transform.forward;
        slipTimer = slipDistance;

        // Smooth blend forward force
        currentDiveSpeed = 0f;
    }

    void HandleDive()
    {
        // Phase 1: airborne dive arc
        if (!characterController.isGrounded)
        {
            // Smoothly ramp up forward speed (no sudden jerk)
            currentDiveSpeed = Mathf.Lerp(currentDiveSpeed, diveDistance, 6f * Time.deltaTime);

            // Apply gravity for parabola
            verticalVelocity.y += diveGravity * Time.deltaTime;

            // Combine forward + vertical motion
            Vector3 moveVec = (diveDir * currentDiveSpeed * Time.deltaTime) + (verticalVelocity * Time.deltaTime);
            characterController.Move(moveVec);

            // add subtle downward tilt during dive
            transform.rotation = Quaternion.Lerp(transform.rotation, 
                Quaternion.LookRotation(diveDir + Vector3.down * 0.5f),
                8f * Time.deltaTime);

            return;
        }

        // Phase 2: slipping after landing
        if (slipTimer > 0f)
        {
            characterController.Move(diveDir * slipDistance * Time.deltaTime);
            slipTimer -= Time.deltaTime;
            return;
        }

        // Dive complete
        if (slipTimer <= 0f)
        {
            isDiving = false;
            canDive = true;
            isDashing = false;
            verticalVelocity = Vector3.zero; // reset vertical 
            characterController.height = normalControllerHeight; // reset controller height
            isRecoveringRotation = true; // trigger recovery phase
        }
    }

    void RecoverRoation()
    {
        // Smoothly bring player upright
        Quaternion targetRot = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 20f * Time.deltaTime);

        // Stop when nearly upright
        if (Quaternion.Angle(transform.rotation, targetRot) < 0.5f)
            isRecoveringRotation = false;
    }

    void HandleDashCooldown()
    {
        if (canDash) return;

        // reduce cooldown
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        // allow dash again only if cooldown is over AND grounded
        if (dashCooldownTimer <= 0f && characterController.isGrounded)
        {
            canDash = true;
        }
    }

    void SetCameraDamping(float value)
    {
        orbitalFollow.TrackerSettings.PositionDamping = new Vector3(value, value, value);
    }
}