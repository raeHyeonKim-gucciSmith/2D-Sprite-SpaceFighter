using UnityEngine;

public class FrontAttackEnemyController : EnemyController
{
    private enum State { Charging, Turning }

    [Header("Target")]
    public Transform player;

    [Header("Charge")]
    public float chargeSpeed = 7f;
    public float chargePastPlayerDistance = 3f;
    public float maxChargeDistance = 18f;

    [Header("Turn")]
    public float turnRadius = 6f;
    public float turnDuration = 3.5f;

    [Header("Smooth Steering")]
    [Min(1f)] public float movementTurnSpeed = 70f;
    [Min(1f)] public float visualTurnSpeed = 110f;

    [Header("Debris Avoidance")]
    [HideInInspector]
    public LayerMask debrisLayer;
    [HideInInspector]
    public float debrisCheckRadius = 0.35f;
    [HideInInspector]
    public float debrisAvoidAngle = 25f;

    [Header("Lifetime")]
    public float safetyLifeTime = 30f;

    private State state = State.Charging;
    private Vector2 chargeDirection;
    private Vector2 attackChargeDirection;
    private Vector2 chargeTarget;
    private Vector2 chargeStartPosition;
    private Vector2 turnStartPosition;
    private Vector2 turnCenter;
    private float turnStartAngle;
    private float turnDirection = 1f;
    private float currentTurnDirection = 1f;
    private float turnTimer;
    private bool wasAvoidingDebris;
    private Vector2 currentMoveDirection;

    protected override void Start()
    {
        base.Start();

        FindPlayerIfNeeded();
        currentMoveDirection = transform.up;
        BeginCharge();

        if (safetyLifeTime > 0f)
            Destroy(gameObject, safetyLifeTime);
    }

    void Update()
    {
        if (IsDestroyed) return;

        FindPlayerIfNeeded();
        if (player == null) return;

        switch (state)
        {
            case State.Charging:
                UpdateCharge();
                break;
            case State.Turning:
                UpdateTurn();
                break;
        }
    }

    void BeginCharge()
    {
        if (player == null) return;

        chargeStartPosition = transform.position;

        Vector2 playerPos = player.position;
        attackChargeDirection = (playerPos - (Vector2)transform.position).normalized;

        if (attackChargeDirection == Vector2.zero)
            attackChargeDirection = transform.up;

        if (currentMoveDirection.sqrMagnitude < 0.0001f)
            currentMoveDirection = transform.up;

        chargeDirection = currentMoveDirection.normalized;
        chargeTarget = playerPos + attackChargeDirection * chargePastPlayerDistance;

        RotateTowards(attackChargeDirection);
        state = State.Charging;
    }

    void UpdateCharge()
    {
        Vector2 safeDirection = ApplyDebrisAvoidance(attackChargeDirection, chargeSpeed);

        if (IsDebrisAvoidanceActive)
        {
            wasAvoidingDebris = true;
            chargeDirection = SmoothMovementDirection(safeDirection);
            transform.position += (Vector3)chargeDirection * chargeSpeed * Time.deltaTime;
            RotateTowards(chargeDirection);
            return;
        }

        if (wasAvoidingDebris)
        {
            wasAvoidingDebris = false;
            BeginCharge();
            return;
        }

        chargeDirection = SmoothMovementDirection(attackChargeDirection);

        transform.position += (Vector3)chargeDirection * chargeSpeed * Time.deltaTime;
        RotateTowards(chargeDirection);

        float traveledDistance = Vector2.Distance(chargeStartPosition, transform.position);
        float targetDistance = Vector2.Distance(chargeStartPosition, chargeTarget);

        if (traveledDistance >= targetDistance || traveledDistance >= maxChargeDistance)
            BeginTurn();
    }

    void BeginTurn()
    {
        state = State.Turning;
        turnTimer = 0f;
        turnStartPosition = transform.position;
        currentTurnDirection = turnDirection;

        Vector2 side = new Vector2(-chargeDirection.y, chargeDirection.x) * currentTurnDirection;
        turnCenter = turnStartPosition + side * turnRadius;
        turnStartAngle = Mathf.Atan2(
            turnStartPosition.y - turnCenter.y,
            turnStartPosition.x - turnCenter.x
        );

        turnDirection *= -1f;
    }

    void UpdateTurn()
    {
        float nextTurnTimer = turnTimer + Time.deltaTime;

        float t = Mathf.Clamp01(nextTurnTimer / Mathf.Max(0.01f, turnDuration));
        float angle = turnStartAngle + Mathf.PI * t * currentTurnDirection;

        Vector2 nextPos = turnCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * turnRadius;
        Vector2 moveDir = (nextPos - (Vector2)transform.position).normalized;
        float turnMoveSpeed = Mathf.PI * turnRadius / Mathf.Max(0.01f, turnDuration);
        Vector2 safeMoveDir = ApplyDebrisAvoidance(moveDir, turnMoveSpeed);

        if (IsDebrisAvoidanceActive)
        {
            wasAvoidingDebris = true;
            Vector2 avoidanceMoveDir = SmoothMovementDirection(safeMoveDir);
            transform.position += (Vector3)avoidanceMoveDir
                * turnMoveSpeed * Time.deltaTime;
            RotateTowards(avoidanceMoveDir);
            return;
        }

        if (wasAvoidingDebris)
        {
            wasAvoidingDebris = false;
            BeginCharge();
            return;
        }

        turnTimer = nextTurnTimer;
        transform.position = new Vector3(nextPos.x, nextPos.y, transform.position.z);
        currentMoveDirection = moveDir;

        if (safeMoveDir != Vector2.zero)
            RotateTowards(safeMoveDir);

        if (t >= 1f)
            BeginCharge();
    }

    Vector2 GetDebrisSafeDirection(Vector2 desiredDirection)
    {
        if (debrisLayer.value == 0) return desiredDirection;

        Vector2 start = transform.position;
        float checkDistance = Mathf.Min(maxChargeDistance, Vector2.Distance(start, (Vector2)player.position) + chargePastPlayerDistance);

        if (!IsDebrisInDirection(start, desiredDirection, checkDistance))
            return desiredDirection;

        Vector2 left = RotateVector(desiredDirection, debrisAvoidAngle);
        Vector2 right = RotateVector(desiredDirection, -debrisAvoidAngle);

        bool leftBlocked = IsDebrisInDirection(start, left, checkDistance);
        bool rightBlocked = IsDebrisInDirection(start, right, checkDistance);

        if (!leftBlocked && rightBlocked) return left;
        if (leftBlocked && !rightBlocked) return right;
        if (!leftBlocked && !rightBlocked)
            return Random.value < 0.5f ? left : right;

        return desiredDirection;
    }

    bool IsDebrisInDirection(Vector2 start, Vector2 direction, float distance)
    {
        RaycastHit2D hit = Physics2D.CircleCast(
            start,
            debrisCheckRadius,
            direction,
            distance,
            debrisLayer
        );

        return hit.collider != null;
    }

    Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        ).normalized;
    }

    void FindPlayerIfNeeded()
    {
        if (player != null) return;

        FlightController fc = FindFirstObjectByType<FlightController>();
        if (fc != null)
            player = fc.transform;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        FlightController playerController = collision.gameObject.GetComponentInParent<FlightController>();
        if (playerController == null) return;

        Vector2 hitDirection = chargeDirection == Vector2.zero
            ? (Vector2)transform.up
            : chargeDirection;

        playerController.Die(collision.GetContact(0).point, hitDirection);
        DestroyEnemy(-hitDirection);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        FlightController playerController = collision.GetComponentInParent<FlightController>();
        if (playerController == null) return;

        Vector2 hitPosition = collision.ClosestPoint(transform.position);
        Vector2 hitDirection = chargeDirection == Vector2.zero
            ? (Vector2)transform.up
            : chargeDirection;

        playerController.Die(hitPosition, hitDirection);
        DestroyEnemy(-hitDirection);
    }

    void RotateTowards(Vector2 dir)
    {
        if (dir == Vector2.zero) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            Mathf.Max(1f, visualTurnSpeed) * Time.deltaTime);
    }

    Vector2 SmoothMovementDirection(Vector2 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude < 0.0001f)
            return currentMoveDirection;

        desiredDirection.Normalize();

        if (currentMoveDirection.sqrMagnitude < 0.0001f)
            currentMoveDirection = transform.up;

        Vector3 rotatedDirection = Vector3.RotateTowards(
            currentMoveDirection,
            desiredDirection,
            Mathf.Max(1f, movementTurnSpeed) * Mathf.Deg2Rad * Time.deltaTime,
            0f);

        currentMoveDirection = ((Vector2)rotatedDirection).normalized;
        return currentMoveDirection;
    }
}
