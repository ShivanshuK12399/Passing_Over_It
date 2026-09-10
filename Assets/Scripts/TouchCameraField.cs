using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

/// <summary>
/// Touch drag zone covering the right half of the screen.
/// Directly rotates the CinemachineOrbitalFollow camera when dragged.
/// Action buttons (Jump, Dash) sit in front of this field in UI order and block touch drag.
/// </summary>
[DisallowMultipleComponent]
public class TouchCameraField : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Camera Reference")]
    public CinemachineCamera freeLookCam;

    [Header("Sensitivity Settings")]
    public float sensitivityX = 0.15f;
    public float sensitivityY = 0.15f;
    public bool invertY = false;

    private CinemachineOrbitalFollow orbitalFollow;

    private void Start()
    {
        FindCameraReferences();
    }

    private void OnEnable()
    {
        FindCameraReferences();
    }

    private void FindCameraReferences()
    {
        if (freeLookCam == null)
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                freeLookCam = pc.freeLookCam;
            }
        }

        if (freeLookCam != null && orbitalFollow == null)
        {
            orbitalFollow = freeLookCam.GetComponent<CinemachineOrbitalFollow>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Touch started on right-side drag area
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (orbitalFollow == null)
        {
            FindCameraReferences();
            if (orbitalFollow == null) return;
        }

        Vector2 delta = eventData.delta;

        // Apply horizontal orbit
        orbitalFollow.HorizontalAxis.Value += delta.x * sensitivityX;

        // Apply vertical orbit
        float yDelta = invertY ? delta.y : -delta.y;
        orbitalFollow.VerticalAxis.Value += yDelta * sensitivityY;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Touch released
    }
}
