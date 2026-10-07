using UnityEngine;

namespace PassingOverIt.Player
{
    /// <summary>
    /// Drives the PandaChibi Animator parameters from PlayerController state.
    /// Manages zero-GC cached parameter updates and network animation synchronization.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimation : MonoBehaviour
    {
        [Header("Speed Smoothing")]
        [Tooltip("How fast the Speed animator float ramps up/down. Increase for snappier transitions.")]
        [SerializeField] private float speedSmoothRate = 10f;

        // Cached component references
        private PlayerController _controller;
        private CharacterController _characterController;
        private Animator _animator;

        // Cached Animator parameter hashes (zero-GC per guide §6)
        private static readonly int SpeedHash      = Animator.StringToHash("Speed");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsJumpingHash  = Animator.StringToHash("IsJumping");
        private static readonly int IsFallingHash  = Animator.StringToHash("IsFalling");
        private static readonly int IsDivingHash   = Animator.StringToHash("IsDiving");
        private static readonly int IsDashingHash  = Animator.StringToHash("IsDashing");

        // Last-written values to avoid redundant Animator writes (per guide §6)
        private float _lastSpeed = -1f;
        private bool  _lastIsGrounded = false;
        private bool  _lastIsJumping  = false;
        private bool  _lastIsFalling  = false;
        private bool  _lastIsDiving   = false;
        private bool  _lastIsDashing  = false;

        private float _smoothedSpeed;

        private void Awake()
        {
            _controller          = GetComponent<PlayerController>();
            _characterController = GetComponent<CharacterController>();
            _animator            = GetComponent<Animator>();

            if (_animator == null)
            {
                Debug.LogWarning($"[PlayerAnimation] No Animator component found on {name}. Animations will not play.");
            }
            else
            {
                // Disable Root Motion so CharacterController.Move() exclusively controls movement physics
                _animator.applyRootMotion = false;
            }

            if (_characterController == null)
            {
                Debug.LogWarning($"[PlayerAnimation] No CharacterController component found on {name}.");
            }
        }

        /// <summary>
        /// Explicitly triggers the jump animation state.
        /// Called locally upon jump launch and via RPC on remote network observers.
        /// </summary>
        public void PlayJumpAnimation()
        {
            if (_animator == null) return;

            _animator.Play("Jump_Start", 0, 0f);
            _animator.SetBool(IsFallingHash, false);
            _animator.SetBool(IsGroundedHash, false);

            _lastIsJumping  = false;
            _lastIsFalling  = false;
            _lastIsGrounded = false;
        }

        private void Update()
        {
            if (_animator == null || _controller == null || _characterController == null) return;

            // In network mode, local owner drives full input animation parameters.
            // Remote clients receive explicit action RPCs (Jump/Dash/Dive) and update physics states (Grounded/Falling).
            if (_controller.NetworkObject != null && _controller.NetworkObject.IsSpawned && !_controller.IsOwner)
            {
                UpdateGrounded();
                UpdateFalling();
                return;
            }

            UpdateSpeed();
            UpdateGrounded();
            UpdateJumping();
            UpdateFalling();
            UpdateDiving();
            UpdateDashing();
        }

        private void UpdateSpeed()
        {
            // Use movement input magnitude so landing physics drift doesn't corrupt Animator Speed
            float targetSpeed = Mathf.Clamp01(_controller.MoveInputMagnitude);

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
            bool grounded = _controller.IsGrounded;
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

        private void UpdateFalling()
        {
            bool falling = _controller.IsFalling;
            if (falling == _lastIsFalling) return;

            _animator.SetBool(IsFallingHash, falling);
            _lastIsFalling = falling;
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
