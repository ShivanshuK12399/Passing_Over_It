using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;

/// <summary>
/// Attached to the joystick_base GameObject.
/// Intercepts pointer touches anywhere on the joystick base (including the area outside joystick_handle)
/// and dynamically snaps/drags the handle using the OnScreenStick component.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class JoystickBaseTouchForwarder : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Tooltip("Reference to the OnScreenStick component on the joystick handle. Automatically found in children if left unassigned.")]
    [SerializeField] private OnScreenStick targetOnScreenStick;

    private RectTransform baseRectTransform;

    private void Awake()
    {
        baseRectTransform = GetComponent<RectTransform>();

        if (targetOnScreenStick == null)
        {
            targetOnScreenStick = GetComponentInChildren<OnScreenStick>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetOnScreenStick == null || baseRectTransform == null) return;

        // Save original touch position
        Vector2 originalTouchPos = eventData.position;

        // Calculate the screen position corresponding to the center of joystick_base (local (0,0))
        Vector2 baseCenterScreenPoint = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, baseRectTransform.position);

        // Set position to base center so OnScreenStick anchors its drag origin to (0,0)
        eventData.position = baseCenterScreenPoint;
        targetOnScreenStick.OnPointerDown(eventData);

        // Restore actual touch position and perform drag to immediately snap handle to finger
        eventData.position = originalTouchPos;
        targetOnScreenStick.OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetOnScreenStick == null) return;
        targetOnScreenStick.OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (targetOnScreenStick == null) return;
        targetOnScreenStick.OnPointerUp(eventData);
    }
}
