using UnityEngine;

public class FighterEnemyController : EnemyController
{
    private enum State { Approaching, Orbiting }

    [Header("Target")]
    public Transform player;

    [Header("Approach")]
    public float approachSpeed = 3f;
    public float engageDistance = 5f;

    [Header("Orbit")]
    public float orbitRadius = 6f;
    public float orbitAngularSpeed = 40f;

    [Header("Missile")]
    public GameObject missilePrefab;
    public Transform missileSpawnPoint;
    public float missileSpawnForwardOffset = 0.6f;
    public float minFireInterval = 2f;
    public float maxFireInterval = 4f;

    [Header("First Shot")]
    public float firstShotDelay = 1f;

    [Header("Rotation")]
    public float turnSmoothness = 5f;

    private State state = State.Approaching;
    private float orbitAngle;
    private float fireTimer;
    private float nextFireInterval;

    private bool firstShotFired = false;
    private float screenTimer = 0f;
    private bool wasAvoidingDuringOrbit;

    protected override void Start()
    {
        base.Start();

        if (player == null)
        {
            FlightController fc = FindFirstObjectByType<FlightController>();
            if (fc != null) player = fc.transform;
        }
    }

    void Update()
    {
        if (IsDestroyed || player == null) return;

        bool isOnScreen = IsOnScreen();

        switch (state)
        {
            case State.Approaching:
                Approach();
                break;
            case State.Orbiting:
                Orbit();
                break;
        }

        if (!IsDebrisAvoidanceActive)
        {
            HandleFirstShotTrigger(isOnScreen);

            if (state == State.Orbiting)
                HandleFireTimer(isOnScreen);
        }
    }

    void HandleFirstShotTrigger(bool isOnScreen)
    {
        if (firstShotFired) return;

        if (!isOnScreen)
        {
            screenTimer = 0f;
            return;
        }

        screenTimer += Time.deltaTime;

        if (screenTimer >= firstShotDelay)
        {
            FireMissile();
            firstShotFired = true;
        }
    }

    bool IsOnScreen()
    {
        if (Camera.main == null) return false;

        Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);

        return viewportPos.z > 0f
            && viewportPos.x >= 0f && viewportPos.x <= 1f
            && viewportPos.y >= 0f && viewportPos.y <= 1f;
    }

    void Approach()
    {
        Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
        dir = ApplyDebrisAvoidance(dir, approachSpeed);
        transform.position += (Vector3)dir * approachSpeed * Time.deltaTime;

        RotateTowards(dir);

        if (IsDebrisAvoidanceActive)
            return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist <= engageDistance)
            EnterOrbit();
    }

    void EnterOrbit()
    {
        state = State.Orbiting;

        Vector2 offset = (Vector2)transform.position - (Vector2)player.position;
        orbitAngle = Mathf.Atan2(offset.y, offset.x);

        SetNextFireInterval();
    }

    void Orbit()
    {
        float nextOrbitAngle = orbitAngle
            + orbitAngularSpeed * Mathf.Deg2Rad * Time.deltaTime;

        Vector2 offset = new Vector2(
            Mathf.Cos(nextOrbitAngle),
            Mathf.Sin(nextOrbitAngle)) * orbitRadius;
        Vector2 targetPos = (Vector2)player.position + offset;

        Vector2 moveDir = (targetPos - (Vector2)transform.position).normalized;
        float orbitMoveSpeed = Mathf.Max(
            approachSpeed,
            Mathf.Abs(orbitAngularSpeed) * Mathf.Deg2Rad * orbitRadius);
        Vector2 safeMoveDir = ApplyDebrisAvoidance(moveDir, orbitMoveSpeed);

        if (IsDebrisAvoidanceActive)
        {
            wasAvoidingDuringOrbit = true;
            transform.position += (Vector3)safeMoveDir
                * orbitMoveSpeed * Time.deltaTime;
            RotateTowards(safeMoveDir);
            return;
        }

        if (wasAvoidingDuringOrbit)
        {
            wasAvoidingDuringOrbit = false;
            state = State.Approaching;
            return;
        }

        orbitAngle = nextOrbitAngle;
        transform.position = targetPos;
        RotateTowards(moveDir);
    }

    void HandleFireTimer(bool isOnScreen)
    {
        if (!firstShotFired) return;

        if (!isOnScreen)
        {
            fireTimer = 0f;
            return;
        }

        fireTimer += Time.deltaTime;

        if (fireTimer >= nextFireInterval)
        {
            FireMissile();
            SetNextFireInterval();
        }
    }

    void SetNextFireInterval()
    {
        fireTimer = 0f;
        nextFireInterval = Random.Range(minFireInterval, maxFireInterval);
    }

    void FireMissile()
    {
        if (missilePrefab == null || player == null) return;

        Vector3 spawnPos = missileSpawnPoint != null
            ? missileSpawnPoint.position
            : transform.position + transform.up * missileSpawnForwardOffset;

        Vector2 dir = ((Vector2)player.position - (Vector2)spawnPos).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        MissileController missile = MissileController.Spawn(missilePrefab, spawnPos, rot);

        if (missile != null)
        {
            missile.SetOwner(MissileOwner.Enemy, gameObject);
            missile.FireStraight();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        FlightController playerController =
            collision.gameObject.GetComponentInParent<FlightController>();
        if (playerController == null || IsDestroyed)
            return;

        Vector2 hitPosition = collision.contactCount > 0
            ? collision.GetContact(0).point
            : (Vector2)transform.position;
        DestroyBothFighters(playerController, hitPosition);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        FlightController playerController =
            collision.GetComponentInParent<FlightController>();
        if (playerController == null || IsDestroyed)
            return;

        Vector2 hitPosition = collision.ClosestPoint(transform.position);
        DestroyBothFighters(playerController, hitPosition);
    }

    void DestroyBothFighters(
        FlightController playerController,
        Vector2 hitPosition)
    {
        Vector2 hitDirection =
            (playerController.transform.position - transform.position).normalized;
        if (hitDirection == Vector2.zero)
            hitDirection = transform.up;

        playerController.Die(hitPosition, hitDirection);
        DestroyEnemy(-hitDirection);
    }

    void RotateTowards(Vector2 dir)
    {
        if (dir == Vector2.zero) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            turnSmoothness * Time.deltaTime
        );
    }
}
