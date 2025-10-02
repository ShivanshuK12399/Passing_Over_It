using UnityEngine;
using UnityEngine.InputSystem; // New Input System

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;
    public float dashDistance = 5f;
    public float diveForce = 7f;

    [Header("References")]
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    // Input values
    private Vector2 moveInput;
    private bool jumpPressed;
    private bool dashPressed;
    private bool divePressed;

    // Android UI button flags (set from UI OnClick)
    private bool uiJump;
    private bool uiDash;
    private bool uiDive;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        // Ground check
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f; // Stick to ground

        // Move
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);
        controller.Move(move * moveSpeed * Time.deltaTime);

        // Jump
        if ((jumpPressed || uiJump) && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpPressed = uiJump = false; // reset
        }

        // Dash
        if (dashPressed || uiDash)
        {
            Vector3 dashDir = transform.forward * dashDistance;
            controller.Move(dashDir);
            dashPressed = uiDash = false;
        }

        // Dive (forward + downward force)
        if (divePressed || uiDive)
        {
            Vector3 diveDir = transform.forward * diveForce;
            controller.Move(diveDir * Time.deltaTime);
            velocity.y = -5f; // push down
            divePressed = uiDive = false;
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // ------- INPUT SYSTEM METHODS ------- //
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed) jumpPressed = true;
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.performed) dashPressed = true;
    }

    public void OnDive(InputAction.CallbackContext context)
    {
        if (context.performed) divePressed = true;
    }

    // ------- UI BUTTON HOOKS (Android) ------- //
    public void UIButton_Jump() { uiJump = true; }
    public void UIButton_Dash() { uiDash = true; }
    public void UIButton_Dive() { uiDive = true; }
}
