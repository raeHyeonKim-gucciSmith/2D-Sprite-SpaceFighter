using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
public class DebrisController : MonoBehaviour
{
    private static readonly HashSet<DebrisController> ActiveDebris =
        new HashSet<DebrisController>();
    private static readonly Dictionary<int, Stack<DebrisController>> Pools =
        new Dictionary<int, Stack<DebrisController>>();

    public static event System.Action<DebrisController> DebrisSpawned;
    public static event System.Action<DebrisController> DebrisRemoved;
    public static IEnumerable<DebrisController> ActiveInstances => ActiveDebris;

    [Header("Movement")]
    public float moveSpeed = 0.8f;
    public float speedVariance = 0.25f;

    [Header("Rotation")]
    public float rotationSpeed = 35f;
    public float rotationVariance = 15f;

    [Header("Screen Lifetime")]
    public float offscreenDestroyDelay = 0.5f;
    public float safetyLifeTime = 30f;
    [Min(0f)] public float safetyLifeTimeJitter = 3f;

    [Header("Sprite Variants")]
    public Sprite[] spriteVariants;

    private Vector2 moveDirection;
    private float currentMoveSpeed;
    private float currentRotationSpeed;
    private bool hasEnteredCameraView;
    private float offscreenTimer;
    private Rigidbody2D rb;
    private Camera targetCamera;
    private SpriteRenderer cachedSpriteRenderer;
    private int poolKey;
    private bool isRecycling;
    private float remainingSafetyLife;
    private float avoidanceRadius = 0.5f;

    public Vector2 CurrentVelocity => rb != null
        ? rb.linearVelocity
        : moveDirection * currentMoveSpeed;

    public static DebrisController Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (prefab == null)
            return null;

        int key = prefab.GetInstanceID();
        DebrisController debris = null;

        if (Pools.TryGetValue(key, out Stack<DebrisController> pool))
        {
            while (pool.Count > 0 && debris == null)
                debris = pool.Pop();
        }

        if (debris == null)
        {
            GameObject debrisObject = Instantiate(prefab, position, rotation);
            debris = debrisObject.GetComponent<DebrisController>();
            if (debris == null)
            {
                Destroy(debrisObject);
                return null;
            }

            debris.poolKey = key;
        }
        else
        {
            debris.transform.SetPositionAndRotation(position, rotation);
            debris.gameObject.SetActive(true);
        }

        debris.poolKey = key;
        debris.transform.SetPositionAndRotation(position, rotation);
        return debris;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry()
    {
        ActiveDebris.Clear();
        Pools.Clear();
        DebrisSpawned = null;
        DebrisRemoved = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Pools.Clear();
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        targetCamera = Camera.main;
        cachedSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        SetupRigidbody();

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null)
                continue;

            Vector2 extents = collider.bounds.extents;
            avoidanceRadius = Mathf.Max(avoidanceRadius, extents.x, extents.y);
        }
    }

    void OnEnable()
    {
        isRecycling = false;
        hasEnteredCameraView = false;
        offscreenTimer = 0f;
        remainingSafetyLife = safetyLifeTime > 0f
            ? Mathf.Max(
                0.1f,
                safetyLifeTime + Random.Range(
                    -safetyLifeTimeJitter,
                    safetyLifeTimeJitter))
            : 0f;

        if (targetCamera == null)
            targetCamera = Camera.main;
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        SetupRigidbody();
        moveDirection = transform.up;
        currentMoveSpeed = Mathf.Max(0f, moveSpeed + Random.Range(-speedVariance, speedVariance));
        currentRotationSpeed = rotationSpeed + Random.Range(-rotationVariance, rotationVariance);
        ApplyVelocity();
        ApplyRandomSprite();

        if (ActiveDebris.Add(this))
            DebrisSpawned?.Invoke(this);
    }

    void OnDisable()
    {
        if (ActiveDebris.Remove(this))
            DebrisRemoved?.Invoke(this);
    }

    void Update()
    {
        if (safetyLifeTime > 0f)
        {
            remainingSafetyLife -= Time.deltaTime;
            if (remainingSafetyLife <= 0f)
            {
                Recycle();
                return;
            }
        }

        UpdateCameraLifetime();
    }

    void SetupRigidbody()
    {
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void ApplyVelocity()
    {
        if (rb == null) return;

        rb.linearVelocity = moveDirection * currentMoveSpeed;
        rb.angularVelocity = currentRotationSpeed;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        DebrisController otherDebris = collision.gameObject.GetComponentInParent<DebrisController>();
        if (otherDebris != null)
        {
            BounceOffDebris(collision);
            return;
        }

        EnemyController enemy = collision.gameObject.GetComponentInParent<EnemyController>();
        if (enemy != null)
        {
            Vector2 contactPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : (Vector2)transform.position;
            Vector2 hazardHitDirection = ((Vector2)enemy.transform.position - contactPoint).normalized;
            enemy.DestroyByEnvironment(hazardHitDirection);
            return;
        }

        FlightController player = collision.gameObject.GetComponentInParent<FlightController>();
        if (player == null) return;

        Vector2 hitDirection = ((Vector2)player.transform.position - collision.GetContact(0).point).normalized;
        player.Die(collision.GetContact(0).point, hitDirection);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        DebrisController otherDebris = collision.GetComponentInParent<DebrisController>();
        if (otherDebris != null)
        {
            BounceAwayFrom(otherDebris.transform.position);
            return;
        }

        EnemyController enemy = collision.GetComponentInParent<EnemyController>();
        if (enemy != null)
        {
            Vector2 hazardHitPosition = collision.ClosestPoint(transform.position);
            Vector2 hazardHitDirection = ((Vector2)enemy.transform.position - hazardHitPosition).normalized;
            enemy.DestroyByEnvironment(hazardHitDirection);
            return;
        }

        FlightController player = collision.GetComponentInParent<FlightController>();
        if (player == null) return;

        Vector2 hitPosition = collision.ClosestPoint(transform.position);
        Vector2 hitDirection = ((Vector2)player.transform.position - hitPosition).normalized;
        player.Die(hitPosition, hitDirection);
    }

    void ApplyRandomSprite()
    {
        if (spriteVariants == null || spriteVariants.Length == 0) return;

        if (cachedSpriteRenderer == null) return;

        cachedSpriteRenderer.sprite = spriteVariants[Random.Range(0, spriteVariants.Length)];
    }

    void BounceOffDebris(Collision2D collision)
    {
        if (collision.contactCount == 0)
        {
            moveDirection = -moveDirection;
            ApplyVelocity();
            return;
        }

        Vector2 normal = collision.GetContact(0).normal;
        moveDirection = Vector2.Reflect(moveDirection, normal).normalized;

        if (moveDirection == Vector2.zero)
            moveDirection = Random.insideUnitCircle.normalized;

        ApplyVelocity();
    }

    void BounceAwayFrom(Vector2 otherPosition)
    {
        Vector2 awayDirection = ((Vector2)transform.position - otherPosition).normalized;

        if (awayDirection == Vector2.zero)
            awayDirection = Random.insideUnitCircle.normalized;

        moveDirection = awayDirection;
        ApplyVelocity();
    }

    void UpdateCameraLifetime()
    {
        if (targetCamera == null) return;

        bool isVisible = IsInsideCameraView();

        if (isVisible)
        {
            hasEnteredCameraView = true;
            offscreenTimer = 0f;
            return;
        }

        if (!hasEnteredCameraView) return;

        offscreenTimer += Time.deltaTime;

        if (offscreenTimer >= offscreenDestroyDelay)
            Recycle();
    }

    bool IsInsideCameraView()
    {
        Vector3 viewportPos = targetCamera.WorldToViewportPoint(transform.position);

        return viewportPos.z > 0f
            && viewportPos.x >= 0f && viewportPos.x <= 1f
            && viewportPos.y >= 0f && viewportPos.y <= 1f;
    }

    void Recycle()
    {
        if (isRecycling)
            return;

        isRecycling = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (poolKey == 0)
        {
            Destroy(gameObject);
            return;
        }

        gameObject.SetActive(false);

        if (!Pools.TryGetValue(poolKey, out Stack<DebrisController> pool))
        {
            pool = new Stack<DebrisController>();
            Pools.Add(poolKey, pool);
        }

        pool.Push(this);
    }

    public static bool TryGetAvoidanceDirection(
        Vector2 position,
        Vector2 desiredVelocity,
        float lookAheadTime,
        float clearance,
        out Vector2 avoidanceDirection)
    {
        avoidanceDirection = Vector2.zero;

        float desiredSpeedSqr = desiredVelocity.sqrMagnitude;
        if (desiredSpeedSqr < 0.0001f || ActiveDebris.Count == 0)
            return false;

        Vector2 forward = desiredVelocity / Mathf.Sqrt(desiredSpeedSqr);
        Vector2 combinedAvoidance = Vector2.zero;
        float totalWeight = 0f;
        float safeLookAhead = Mathf.Max(0.1f, lookAheadTime);

        foreach (DebrisController debris in ActiveDebris)
        {
            if (debris == null || debris.isRecycling || !debris.gameObject.activeInHierarchy)
                continue;

            Vector2 relativePosition = (Vector2)debris.transform.position - position;
            float combinedRadius = Mathf.Max(0.1f, clearance) + debris.avoidanceRadius;
            float combinedRadiusSqr = combinedRadius * combinedRadius;

            float forwardDistance = Vector2.Dot(relativePosition, forward);
            if (forwardDistance < -combinedRadius && relativePosition.sqrMagnitude > combinedRadiusSqr)
                continue;

            Vector2 relativeVelocity = debris.CurrentVelocity - desiredVelocity;
            float relativeSpeedSqr = relativeVelocity.sqrMagnitude;
            float closestTime = relativeSpeedSqr > 0.0001f
                ? Mathf.Clamp(-Vector2.Dot(relativePosition, relativeVelocity) / relativeSpeedSqr, 0f, safeLookAhead)
                : 0f;

            Vector2 closestOffset = relativePosition + relativeVelocity * closestTime;
            float closestDistanceSqr = closestOffset.sqrMagnitude;
            if (closestDistanceSqr > combinedRadiusSqr)
                continue;

            Vector2 away;
            if (closestDistanceSqr > 0.0001f)
            {
                away = -closestOffset.normalized;
            }
            else
            {
                float cross = forward.x * relativePosition.y - forward.y * relativePosition.x;
                if (Mathf.Abs(cross) < 0.001f)
                    cross = (debris.GetInstanceID() & 1) == 0 ? 1f : -1f;

                Vector2 right = new Vector2(forward.y, -forward.x);
                away = right * Mathf.Sign(cross);
            }

            float proximityWeight = 1f - Mathf.Clamp01(closestDistanceSqr / combinedRadiusSqr);
            float timeWeight = 1f - Mathf.Clamp01(closestTime / safeLookAhead);
            float weight = 0.35f + proximityWeight + timeWeight;
            combinedAvoidance += away * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0f || combinedAvoidance.sqrMagnitude < 0.0001f)
            return false;

        avoidanceDirection = combinedAvoidance.normalized;
        return true;
    }
}
