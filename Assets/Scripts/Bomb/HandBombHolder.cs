using UnityEngine;
using PassingOverIt.Player;

namespace PassingOverIt.Bomb
{
    /// <summary>
    /// Component attached to Player GameObject. Manages spawning and attaching
    /// the bomb visual prefab to the player's hands/socket when they hold the bomb.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public class HandBombHolder : MonoBehaviour
    {
        [Header("Hand Socket Configuration")]
        [Tooltip("Transform bone or object representing player's hands. If null, an anchor point in front of player is automatically created.")]
        [SerializeField] private Transform handSocket;

        [Tooltip("Local position offset relative to hand socket.")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0f, 0.5f);

        private PlayerController _playerController;
        private GameObject _spawnedBombInstance;
        private Transform _targetSocket;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();

            if (handSocket != null)
            {
                _targetSocket = handSocket;
            }
            else
            {
                // Create a front anchor socket under this transform
                GameObject anchor = new GameObject("HandSocket_Anchor");
                anchor.transform.SetParent(transform, false);
                anchor.transform.localPosition = new Vector3(0f, 1.2f, 0.6f);
                _targetSocket = anchor.transform;
            }
        }

        private void OnEnable()
        {
            BombManager.OnBombHolderChanged += HandleBombHolderChanged;
            
            // Check initial state
            if (BombManager.Instance != null)
            {
                bool isHolder = BombManager.Instance.CurrentHolderObjectId.Value == _playerController.ObjectId;
                SetBombVisualActive(isHolder);
            }
        }

        private void OnDisable()
        {
            BombManager.OnBombHolderChanged -= HandleBombHolderChanged;
            SetBombVisualActive(false);
        }

        private void HandleBombHolderChanged(PlayerController newHolder)
        {
            bool isHolder = newHolder != null && newHolder.ObjectId == _playerController.ObjectId;
            SetBombVisualActive(isHolder);
        }

        private void SetBombVisualActive(bool active)
        {
            if (active)
            {
                EnsureBombInstanceCreated();
                if (_spawnedBombInstance != null)
                {
                    _spawnedBombInstance.SetActive(true);
                }
            }
            else
            {
                if (_spawnedBombInstance != null)
                {
                    _spawnedBombInstance.SetActive(false);
                }
            }
        }

        private void EnsureBombInstanceCreated()
        {
            if (_spawnedBombInstance != null) return;

            GameObject prefab = BombManager.Instance != null ? BombManager.Instance.BombPrefab : null;

            if (prefab != null)
            {
                _spawnedBombInstance = Instantiate(prefab, _targetSocket);
                _spawnedBombInstance.transform.localPosition = localOffset;
                _spawnedBombInstance.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Primitive fallback sphere bomb if no prefab assigned in BombManager inspector
                _spawnedBombInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _spawnedBombInstance.name = "Fallback_Bomb_Visual";
                Destroy(_spawnedBombInstance.GetComponent<Collider>()); // Visual only
                _spawnedBombInstance.transform.SetParent(_targetSocket, false);
                _spawnedBombInstance.transform.localPosition = localOffset;
                _spawnedBombInstance.transform.localScale = Vector3.one * 0.5f;

                Renderer rend = _spawnedBombInstance.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material.color = Color.black;
                }
            }
        }
    }
}
