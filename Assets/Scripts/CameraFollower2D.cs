using UnityEngine;

public class CameraFollower2D : MonoBehaviour
{
    [Header("Target")]
    public Camera targetCamera;

    [Header("Parallax")]
    [Range(0f, 1f)] public float parallaxX = 0.25f;
    [Range(0f, 1f)] public float parallaxY = 0.25f;

    [Header("Follow Axis")]
    public bool followX = true;
    public bool followY = true;
    public bool keepInitialZ = true;
    public float zPosition = -10f;
    public float smoothTime = 0f;

    [Header("Star Depth Layers")]
    public bool createDepthLayers = true;
    [Range(0f, 1f)] public float farParallax = 0.05f;
    [Range(0f, 1f)] public float middleParallax = 0.18f;
    [Range(0f, 1f)] public float nearParallax = 0.38f;

    private Vector3 velocity;
    private Vector3 initialPosition;
    private Vector3 initialCameraPosition;
    private float initialZ;
    private Transform farLayer;
    private Transform nearLayer;
    private Vector3 farInitialPosition;
    private Vector3 nearInitialPosition;
    private Vector3 farVelocity;
    private Vector3 nearVelocity;
    private static bool creatingRuntimeLayer;

    void Awake()
    {
        // Runtime copies contain the same component. Keep their particle systems,
        // but prevent them from recursively creating more depth layers.
        if (creatingRuntimeLayer)
        {
            enabled = false;
            return;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;

        initialPosition = transform.position;
        initialCameraPosition = targetCamera != null
            ? targetCamera.transform.position
            : Vector3.zero;

        initialZ = transform.position.z;

        if (createDepthLayers && GetComponent<ParticleSystem>() != null)
            CreateStarDepthLayers();
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 cameraDelta = cameraPosition - initialCameraPosition;
        float middleX = createDepthLayers ? middleParallax : parallaxX;
        float middleY = createDepthLayers ? middleParallax : parallaxY;

        Vector3 targetPosition = GetLayerTarget(initialPosition, cameraDelta, middleX, middleY);
        transform.position = MoveLayer(transform.position, targetPosition, ref velocity);

        if (farLayer != null)
        {
            Vector3 farTarget = GetLayerTarget(
                farInitialPosition, cameraDelta, farParallax, farParallax);
            farLayer.position = MoveLayer(farLayer.position, farTarget, ref farVelocity);
        }

        if (nearLayer != null)
        {
            Vector3 nearTarget = GetLayerTarget(
                nearInitialPosition, cameraDelta, nearParallax, nearParallax);
            nearLayer.position = MoveLayer(nearLayer.position, nearTarget, ref nearVelocity);
        }
    }

    private Vector3 GetLayerTarget(
        Vector3 origin, Vector3 cameraDelta, float xParallax, float yParallax)
    {
        Vector3 target = origin;
        if (followX)
            target.x += cameraDelta.x * xParallax;
        if (followY)
            target.y += cameraDelta.y * yParallax;
        target.z = keepInitialZ ? origin.z : zPosition;
        return target;
    }

    private Vector3 MoveLayer(
        Vector3 current, Vector3 target, ref Vector3 layerVelocity)
    {
        if (smoothTime <= 0f)
            return target;

        return Vector3.SmoothDamp(
            current,
            target,
            ref layerVelocity,
            smoothTime);
    }

    private void CreateStarDepthLayers()
    {
        creatingRuntimeLayer = true;
        GameObject farObject;
        GameObject nearObject;
        try
        {
            farObject = Instantiate(gameObject, transform.parent);
            nearObject = Instantiate(gameObject, transform.parent);
        }
        finally
        {
            creatingRuntimeLayer = false;
        }

        farObject.name = "StarryBackground - Far Stars";
        nearObject.name = "StarryBackground - Near Stars";
        farLayer = farObject.transform;
        nearLayer = nearObject.transform;
        farInitialPosition = farLayer.position;
        nearInitialPosition = nearLayer.position;

        ConfigureStarLayer(farObject, 0.55f, 0.62f, 0.7f, -210);
        ConfigureStarLayer(gameObject, 0.85f, 0.82f, 0.9f, -205);
        ConfigureStarLayer(nearObject, 1.35f, 0.9f, 0.45f, -200);
    }

    private static void ConfigureStarLayer(
        GameObject layerObject,
        float sizeScale,
        float alpha,
        float emissionScale,
        int sortingOrder)
    {
        ParticleSystem particles = layerObject.GetComponent<ParticleSystem>();
        if (particles != null)
        {
            ParticleSystem.MainModule main = particles.main;
            main.startSizeMultiplier *= sizeScale;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, alpha));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTimeMultiplier *= emissionScale;
        }

        ParticleSystemRenderer particleRenderer =
            layerObject.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
            particleRenderer.sortingOrder = sortingOrder;
    }

    void OnDestroy()
    {
        if (farLayer != null)
            Destroy(farLayer.gameObject);
        if (nearLayer != null)
            Destroy(nearLayer.gameObject);
    }
}
