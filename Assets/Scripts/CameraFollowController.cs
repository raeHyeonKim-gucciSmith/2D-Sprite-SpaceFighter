using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollowController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public string targetTag = "Player";

    [Header("Follow")]
    public float smoothTime = 0.2f;
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    [Header("Thrust Zoom")]
    [Min(0f)] public float maxThrustZoomOut = 1f;
    [Min(0.01f)] public float zoomSmoothTime = 0.35f;

    [Header("Bounds")]
    public Transform borderRoot;
    public float boundsPadding = 0f;

    private Camera cam;
    private FlightController flightController;
    private Vector3 velocity;
    private float baseOrthographicSize;
    private float zoomVelocity;
    private Bounds cameraBounds;
    private bool hasBounds = false;

    void Awake()
    {
        cam = GetComponent<Camera>();
        baseOrthographicSize = cam.orthographicSize;
    }

    void Start()
    {
        if (target == null && !string.IsNullOrEmpty(targetTag))
        {
            GameObject targetObject = GameObject.FindGameObjectWithTag(targetTag);
            if (targetObject != null)
                target = targetObject.transform;
        }

        CacheFlightController();
        CalculateBounds();
    }

    void LateUpdate()
    {
        if (target == null) return;

        UpdateThrustZoom();

        Vector3 targetPosition = target.position + offset;
        targetPosition.z = offset.z;

        if (hasBounds)
            targetPosition = ClampCameraPosition(targetPosition);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            smoothTime
        );
    }

    void CacheFlightController()
    {
        flightController = target != null ? target.GetComponent<FlightController>() : null;
    }

    void UpdateThrustZoom()
    {
        if (!cam.orthographic) return;

        if (flightController == null)
            CacheFlightController();

        float thrust01 = flightController != null ? flightController.CurrentThrust01 : 0f;
        float targetSize = baseOrthographicSize + maxThrustZoomOut * Mathf.Clamp01(thrust01);
        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize,
            targetSize,
            ref zoomVelocity,
            zoomSmoothTime
        );
    }

    void CalculateBounds()
    {
        if (borderRoot == null)
        {
            hasBounds = false;
            return;
        }

        Collider2D[] colliders = borderRoot.GetComponentsInChildren<Collider2D>();

        if (colliders.Length == 0)
        {
            hasBounds = false;
            return;
        }

        cameraBounds = colliders[0].bounds;

        for (int i = 1; i < colliders.Length; i++)
        {
            cameraBounds.Encapsulate(colliders[i].bounds);
        }

        cameraBounds.Expand(boundsPadding * -2f);
        hasBounds = true;
    }

    Vector3 ClampCameraPosition(Vector3 targetPosition)
    {
        float cameraHeight = cam.orthographicSize;
        float cameraWidth = cameraHeight * cam.aspect;

        float minX = cameraBounds.min.x + cameraWidth;
        float maxX = cameraBounds.max.x - cameraWidth;
        float minY = cameraBounds.min.y + cameraHeight;
        float maxY = cameraBounds.max.y - cameraHeight;

        if (minX > maxX)
            targetPosition.x = cameraBounds.center.x;
        else
            targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);

        if (minY > maxY)
            targetPosition.y = cameraBounds.center.y;
        else
            targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);

        return targetPosition;
    }

    public void RefreshBounds()
    {
        CalculateBounds();
    }

    void OnDrawGizmosSelected()
    {
        if (!hasBounds) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(cameraBounds.center, cameraBounds.size);
    }
}
