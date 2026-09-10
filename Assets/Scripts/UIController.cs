using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private void Start()
    {
        InitializeSensitivitySliders();
    }

    private void OnEnable()
    {
        GameManager.OnControlModeChanged += HandleControlModeChanged;

        // Apply state if GameManager is already running
        if (GameManager.Instance != null)
        {
            HandleControlModeChanged(GameManager.Instance.UseTouchControls);
        }
    }

    private void OnDisable()
    {
        GameManager.OnControlModeChanged -= HandleControlModeChanged;
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
}
