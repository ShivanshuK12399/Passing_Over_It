using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Controls camera follow dead zone where the camera remains still while the player moves inside specified world zone boundaries.
/// </summary>
public class CameraDeadZone : MonoBehaviour
{
    [Header("Target References")]
    [Tooltip("Target to follow (usually the Player). If unassigned, defaults to this GameObject.")]
    public Transform targetToFollow;

    [Tooltip("Cinemachine camera to apply tracking target to.")]
    public CinemachineCamera freeLookCam;

    [Header("Dead Zone Settings")]
    public bool enableDeadZone = true;

    [Tooltip("Half-dimensions of box dead zone (X: Horizontal, Y: Vertical, Z: Depth). Camera will not follow player while inside this area.")]
    public Vector3 deadZoneSize = new Vector3(2f, 1.5f, 2f);

    [Header("Gizmos Settings")]
    public bool showGizmos = true;
    public Color gizmoColor = new Color(1f, 0.8f, 0.2f, 0.9f);

    public Transform CameraTargetProxy { get; private set; }

    private void Start()
    {
        if (targetToFollow == null)
        {
            targetToFollow = transform;
        }

        GameObject proxyObj = new GameObject("CameraFollowTarget_Proxy");
        CameraTargetProxy = proxyObj.transform;
        CameraTargetProxy.position = targetToFollow.position;

        if (freeLookCam != null)
        {
            freeLookCam.Target.TrackingTarget = CameraTargetProxy;
        }
    }

    private void OnDestroy()
    {
        if (CameraTargetProxy != null)
        {
            Destroy(CameraTargetProxy.gameObject);
        }
    }

    private void LateUpdate()
    {
        UpdateDeadZone();
    }

    public void UpdateDeadZone()
    {
        if (CameraTargetProxy == null || targetToFollow == null) return;

        if (!enableDeadZone)
        {
            CameraTargetProxy.position = targetToFollow.position;
            return;
        }

        Vector3 playerPos = targetToFollow.position;
        Vector3 targetPos = CameraTargetProxy.position;

        float deltaX = playerPos.x - targetPos.x;
        if (Mathf.Abs(deltaX) > deadZoneSize.x)
        {
            targetPos.x = playerPos.x - Mathf.Sign(deltaX) * deadZoneSize.x;
        }

        float deltaY = playerPos.y - targetPos.y;
        if (Mathf.Abs(deltaY) > deadZoneSize.y)
        {
            targetPos.y = playerPos.y - Mathf.Sign(deltaY) * deadZoneSize.y;
        }

        float deltaZ = playerPos.z - targetPos.z;
        if (Mathf.Abs(deltaZ) > deadZoneSize.z)
        {
            targetPos.z = playerPos.z - Mathf.Sign(deltaZ) * deadZoneSize.z;
        }

        CameraTargetProxy.position = targetPos;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || !enableDeadZone) return;

        Vector3 center = Application.isPlaying && CameraTargetProxy != null ? CameraTargetProxy.position : (targetToFollow != null ? targetToFollow.position : transform.position);
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(center, deadZoneSize * 2f);
    }
}
