using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PassingOverIt.Player;

/// <summary>
/// Attached to the Canvas or UI root. Listens to GameManager's OnControlModeChanged event,
/// toggles the visibility of the Touch Control UI, and manages X/Y Camera Sensitivity Sliders & TMP_Text displays.
/// </summary>
[DisallowMultipleComponent]
public class UIController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Parent GameObject holding the virtual Joystick, Jump/Dash buttons, and Sensitivity sliders.")]
    [SerializeField] private GameObject touchControlsPanel;

    [Header("Sensitivity Controls")]
    [SerializeField] private Slider sensitivityXSlider;
    [SerializeField] private TMP_Text sensitivityXValueText;
    [SerializeField] private Slider sensitivityYSlider;
    [SerializeField] private TMP_Text sensitivityYValueText;
    [SerializeField] private TouchCameraField touchCameraField;

    [Header("Bomb HUD & Pass Button")]
    [SerializeField] private TMP_Text bombTimerText;
    [SerializeField] private Button passBombButton;
    [SerializeField] private GameObject bombHolderIndicator;

    private void Start()
    {
        InitializeSensitivitySliders();
        InitializePassButton();
    }

    private void OnEnable()
    {
        GameManager.OnControlModeChanged += HandleControlModeChanged;
        PassingOverIt.Bomb.BombManager.OnTimerSecondChanged += HandleTimerSecondChanged;
        PassingOverIt.Bomb.BombManager.OnBombHolderChanged += HandleBombHolderChanged;

        // Apply state if GameManager is already running
        if (GameManager.Instance != null)
        {
            HandleControlModeChanged(GameManager.Instance.UseTouchControls);
        }
    }

    private void OnDisable()
    {
        GameManager.OnControlModeChanged -= HandleControlModeChanged;
        PassingOverIt.Bomb.BombManager.OnTimerSecondChanged -= HandleTimerSecondChanged;
        PassingOverIt.Bomb.BombManager.OnBombHolderChanged -= HandleBombHolderChanged;
    }

    private void InitializeSensitivitySliders()
    {
        if (touchCameraField == null)
        {
            touchCameraField = FindFirstObjectByType<TouchCameraField>();
        }

        if (sensitivityXSlider != null)
        {
            UpdateXText(touchCameraField.sensitivityX);
            sensitivityXSlider.onValueChanged.AddListener(OnSensXChanged);
        }

        if (sensitivityYSlider != null)
        {
            UpdateYText(touchCameraField.sensitivityY);
            sensitivityYSlider.onValueChanged.AddListener(OnSensYChanged);
        }
    }

    private void OnSensXChanged(float value)
    {
        if (touchCameraField != null)
        {
            touchCameraField.sensitivityX = value;
        }
        UpdateXText(value);
    }

    private void OnSensYChanged(float value)
    {
        if (touchCameraField != null)
        {
            touchCameraField.sensitivityY = value;
        }
        UpdateYText(value);
    }

    private void UpdateXText(float value)
    {
        if (sensitivityXValueText != null)
        {
            sensitivityXValueText.text = value.ToString("F2");
        }
    }

    private void UpdateYText(float value)
    {
        if (sensitivityYValueText != null)
        {
            sensitivityYValueText.text = value.ToString("F2");
        }
    }

    private void HandleControlModeChanged(bool isTouchEnabled)
    {
        GameObject panelToToggle = touchControlsPanel != null ? touchControlsPanel : gameObject;
        if (panelToToggle != null)
        {
            panelToToggle.SetActive(isTouchEnabled);
        }
    }

    private void InitializePassButton()
    {
        if (passBombButton != null)
        {
            passBombButton.onClick.AddListener(OnPassButtonClicked);
        }
    }

    private void OnPassButtonClicked()
    {
        if (PlayerController.LocalInstance != null)
        {
            PlayerController.LocalInstance.TryPassBomb();
            return;
        }

        for (int i = 0; i < PlayerController.AllPlayers.Count; i++)
        {
            PlayerController p = PlayerController.AllPlayers[i];
            if (p != null && p.IsLocalDriver)
            {
                p.TryPassBomb();
                break;
            }
        }
    }

    private void HandleTimerSecondChanged(int remainingSeconds)
    {
        if (bombTimerText != null)
        {
            bombTimerText.SetText("Bomb: {0}s", remainingSeconds);
        }
    }

    private void HandleBombHolderChanged(PlayerController newHolder)
    {
        bool localHasBomb = false;
        if (newHolder != null && newHolder.IsOwner)
        {
            localHasBomb = true;
        }

        if (bombHolderIndicator != null)
        {
            bombHolderIndicator.SetActive(localHasBomb);
        }

        if (passBombButton != null)
        {
            passBombButton.interactable = localHasBomb;
        }
    }
}
