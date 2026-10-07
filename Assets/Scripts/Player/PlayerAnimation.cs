using UnityEngine;

namespace PassingOverIt.Player
{
    /// <summary>
    /// Drives the PandaChibi Animator from PlayerController state.
    /// Reads movement state each frame and updates Animator parameters only when values change.
    /// Animator component must be on a child mesh GameObject under the player root.
    /// 
    /// Required Animator parameters:
    ///   Speed     (Float)  — 0 = Idle, 1 = Run
    ///   IsGrounded (Bool)
    ///   IsJumping  (Bool)
    ///   IsDiving   (Bool)
    ///   IsDashing  (Bool)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimation : MonoBehaviour
    {
        [Header("Speed Smoothing")]
        [Tooltip("How fast the Speed animator float ramps up/down. Increase for snappier transitions.")]
        [SerializeField] private float speedSmoothRate = 10f;

        // Cached references
        private PlayerController _controller;
        private CharacterController _characterController;
        private Animator _animator;

        // Cached Animator parameter hashes (zero-GC per guide §6)
        private static readonly int SpeedHash      = Animator.StringToHash("Speed");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsJumpingHash  = Animator.StringToHash("IsJumping");
        private static readonly int IsDivingHash   = Animator.StringToHash("IsDiving");
        private static readonly int IsDashingHash  = Animator.StringToHash("IsDashing");

        // Last-written values to avoid redundant Animator writes (per guide §6)
        private float _lastSpeed = -1f;
        private bool  _lastIsGrounded = false;
        private bool  _lastIsJumping  = false;
        private bool  _lastIsDiving   = false;
        private bool  _lastIsDashing  = false;

        private float _smoothedSpeed;

        private void Awake()
        {
            _controller        = GetComponent<PlayerController>();
            _characterController = GetComponent<CharacterController>();

            // Animator lives on the child mesh, not the root
            _animator = GetComponentInChildren<Animator>();

            if (_animator == null)
                Debug.LogWarning("[PlayerAnimation] No Animator found in children of " + name + ". Animations will not play.");

            if (_characterController == null)
                Debug.LogWarning("[PlayerAnimation] No CharacterController found on " + name + ".");
        }

        private void Update()
        {
            if (_animator == null || _controller == null || _characterController == null) return;

            UpdateSpeed();
            UpdateGrounded();
            UpdateJumping();
            UpdateDiving();
            UpdateDashing();
        }

        private void UpdateSpeed()
        {
            // Use horizontal velocity only (exclude vertical for jump/fall)
            Vector3 horizontalVelocity = _characterController.velocity;
            horizontalVelocity.y = 0f;
            float targetSpeed = horizontalVelocity.magnitude / _controller.moveSpeed; // normalized 0-1

            // Smooth the speed value so blend tree transitions aren't jittery
            _smoothedSpeed = Mathf.MoveTowards(_smoothedSpeed, targetSpeed, speedSmoothRate * Time.deltaTime);

            // Only write to Animator when the value actually changes (per guide §6)
            if (!Mathf.Approximately(_smoothedSpeed, _lastSpeed))
            {
                _animator.SetFloat(SpeedHash, _smoothedSpeed);
                _lastSpeed = _smoothedSpeed;
            }
        }

        private void UpdateGrounded()
        {
            bool grounded = _characterController.isGrounded;
            if (grounded == _lastIsGrounded) return;

            _animator.SetBool(IsGroundedHash, grounded);
            _lastIsGrounded = grounded;
        }

        private void UpdateJumping()
        {
            bool jumping = _controller.IsJumping;
            if (jumping == _lastIsJumping) return;

            _animator.SetBool(IsJumpingHash, jumping);
            _lastIsJumping = jumping;
        }

        private void UpdateDiving()
        {
            bool diving = _controller.IsDiving;
            if (diving == _lastIsDiving) return;

            _animator.SetBool(IsDivingHash, diving);
            _lastIsDiving = diving;
        }

        private void UpdateDashing()
        {
            bool dashing = _controller.IsDashing;
            if (dashing == _lastIsDashing) return;

            _animator.SetBool(IsDashingHash, dashing);
            _lastIsDashing = dashing;
        }
    }
}
