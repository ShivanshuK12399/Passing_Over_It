using UnityEngine;

namespace PassingOverIt.Player
{
    /// <summary>
    /// Drives the PandaChibi Animator parameters from PlayerController state.
    /// The owner uses input/physics state directly; remote copies estimate speed and
    /// vertical motion from their synced position, since they have no input or simulation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimation : MonoBehaviour
    {
        [Tooltip("How fast the Speed parameter ramps up/down. Increase for snappier transitions.")]
        [SerializeField] private float speedSmoothRate = 10f;

        private const float RemoteFallThreshold = 0.1f;
        private const string JumpStartState = "Jump_Start";

        private static readonly int SpeedHash      = Animator.StringToHash("Speed");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsJumpingHash  = Animator.StringToHash("IsJumping");
        private static readonly int IsFallingHash  = Animator.StringToHash("IsFalling");
        private static readonly int IsDivingHash   = Animator.StringToHash("IsDiving");
        private static readonly int IsDashingHash  = Animator.StringToHash("IsDashing");

        private PlayerController _controller;
        private Animator _animator;

        // Last values written, to skip redundant Animator writes.
        private float _lastSpeed = -1f;
        private bool _lastGrounded, _lastJumping, _lastFalling, _lastDiving, _lastDashing;

        private float _smoothedSpeed;

        // Position-derived velocity for remote players.
        private Vector3 _lastPosition;
        private Vector3 _estimatedVelocity;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _animator = GetComponent<Animator>();

            if (_animator == null)
            {
                Debug.LogWarning($"[PlayerAnimation] No Animator found on {name}. Animations will not play.", this);
                return;
            }

            // CharacterController.Move() must be the only thing moving the player.
            _animator.applyRootMotion = false;
        }

        private void OnEnable() => _lastPosition = transform.position;

        /// <summary>Starts the jump animation. Called locally on jump and via RPC on observers.</summary>
        public void PlayJumpAnimation()
        {
            if (_animator == null) return;

            _animator.Play(JumpStartState, 0, 0f);
            SetBool(IsFallingHash, false, ref _lastFalling);
            SetBool(IsGroundedHash, false, ref _lastGrounded);
        }

        private void Update()
        {
            if (_animator == null) return;

            bool isDriver = _controller.IsLocalDriver;
            if (!isDriver)
                EstimateVelocity();

            UpdateSpeed(isDriver);

            SetBool(IsGroundedHash, _controller.IsGrounded, ref _lastGrounded);
            SetBool(IsJumpingHash,  _controller.IsJumping,  ref _lastJumping);
            SetBool(IsFallingHash,  IsFalling(isDriver),    ref _lastFalling);
            SetBool(IsDivingHash,   _controller.IsDiving,   ref _lastDiving);
            SetBool(IsDashingHash,  _controller.IsDashing,  ref _lastDashing);
        }

        private void EstimateVelocity()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _estimatedVelocity = (transform.position - _lastPosition) / dt;
            _lastPosition = transform.position;
        }

        private void UpdateSpeed(bool isDriver)
        {
            // Input magnitude for the owner (landing drift won't pollute Speed);
            // synced horizontal movement for remote players.
            float target = isDriver
                ? _controller.MoveInputMagnitude
                : new Vector2(_estimatedVelocity.x, _estimatedVelocity.z).magnitude / _controller.MoveSpeed;

            _smoothedSpeed = Mathf.MoveTowards(_smoothedSpeed, Mathf.Clamp01(target), speedSmoothRate * Time.deltaTime);

            if (Mathf.Approximately(_smoothedSpeed, _lastSpeed)) return;

            _animator.SetFloat(SpeedHash, _smoothedSpeed);
            _lastSpeed = _smoothedSpeed;
        }

        private bool IsFalling(bool isDriver)
        {
            if (isDriver)
                return _controller.IsFalling;

            return !_controller.IsGrounded
                && !_controller.IsDiving
                && _estimatedVelocity.y < -RemoteFallThreshold;
        }

        private void SetBool(int hash, bool value, ref bool lastValue)
        {
            if (value == lastValue) return;

            _animator.SetBool(hash, value);
            lastValue = value;
        }
    }
}