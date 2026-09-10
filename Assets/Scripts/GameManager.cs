using System;
using UnityEngine;

/// <summary>
/// Core Game Manager singleton. Holds global state such as control mode (Touch vs Keyboard)
/// and notifies subscribers via C# delegates and events when settings change.
/// </summary>
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Delegate & Event for control mode changes
    public delegate void ControlModeChangedHandler(bool isTouchEnabled);
    public static event ControlModeChangedHandler OnControlModeChanged;

    [Header("Input Control Settings")]
    [Tooltip("If true, Touch UI controls (Joystick, Jump, Dash) are enabled. If false, Keyboard controls are active and Touch UI is hidden.")]
    [SerializeField] private bool useTouchControls = true;

    public bool UseTouchControls
    {
        get => useTouchControls;
        set
        {
            if (useTouchControls != value)
            {
                useTouchControls = value;
                OnControlModeChanged?.Invoke(useTouchControls);
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Notify subscribers of initial state
        OnControlModeChanged?.Invoke(useTouchControls);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            OnControlModeChanged?.Invoke(useTouchControls);
        }
    }
}
