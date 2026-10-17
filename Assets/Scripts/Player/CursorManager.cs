using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PassingOverIt.Player
{
    /// <summary>
    /// Manages cursor lock state and visibility during gameplay.
    /// Locks cursor at screen center and hides it ONLY while actively playing and when this component is enabled.
    /// Disabling this component in the Inspector completely disables autolocking, restoring free cursor and default controls.
    /// </summary>
    [DisallowMultipleComponent]
    public class CursorManager : MonoBehaviour
    {
        public static CursorManager Instance { get; private set; }

        public static event Action<bool> OnCursorLockStateChanged;
        public static bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>
        /// True if CursorManager is present in the scene and its component is currently enabled.
        /// </summary>
        public static bool IsEnabled => Instance != null && Instance.isActiveAndEnabled;

        /// <summary>
        /// True only when the local player character is actively spawned and controlling in the game.
        /// </summary>
        public static bool IsPlaying => PlayerController.LocalInstance != null && PlayerController.LocalInstance.IsLocalDriver;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameManager.OnControlModeChanged += HandleControlModeChanged;

            if (ShouldLockCursor())
            {
                LockCursor();
            }
            else
            {
                UnlockCursor();
            }
        }

        private void OnDisable()
        {
            GameManager.OnControlModeChanged -= HandleControlModeChanged;
            UnlockCursor();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            UnlockCursor();
        }

        private void Update()
        {
            if (!ShouldLockCursor())
            {
                if (IsCursorLocked)
                    UnlockCursor();
                return;
            }

            // Check Escape key to unlock cursor
            if (WasEscapePressed())
            {
                UnlockCursor();
            }
            // Check Click to re-lock when currently unlocked during gameplay
            else if (!IsCursorLocked && WasLeftClickPressed())
            {
                // Don't re-lock if clicking on UI elements
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                LockCursor();
            }
        }

        public static void LockCursor()
        {
            if (!IsEnabled) return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            OnCursorLockStateChanged?.Invoke(true);
        }

        public static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            OnCursorLockStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Cursor should only be locked when this component is enabled, the player is actively in gameplay, and not using touch controls.
        /// </summary>
        public static bool ShouldLockCursor()
        {
            if (!IsEnabled)
                return false;

            if (!IsPlaying)
                return false;

            if (GameManager.Instance != null && GameManager.Instance.UseTouchControls)
                return false;

            return true;
        }

        private void HandleControlModeChanged(bool isTouchEnabled)
        {
            if (isTouchEnabled || !IsPlaying)
                UnlockCursor();
            else
                LockCursor();
        }

        private bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
                return true;
#endif
            return false;
        }

        private bool WasLeftClickPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
                return true;
#endif
            return false;
        }
    }
}
