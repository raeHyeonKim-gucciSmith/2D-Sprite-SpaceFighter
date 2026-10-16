using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private static readonly HashSet<EnemyController> ActiveEnemies =
        new HashSet<EnemyController>();

    public static event System.Action<EnemyController> EnemySpawned;
    public static event System.Action<EnemyController> EnemyRemoved;
    public static IEnumerable<EnemyController> ActiveInstances => ActiveEnemies;

    [Header("Status")]
    public int maxHp = 1;

    [Header("Effects")]
    public GameObject explosionEffect; // 파편 파티클 프리팹

    [Header("Predictive Debris Avoidance")]
    public bool avoidDebris = true;
    [Min(0.1f)] public float debrisLookAheadTime = 1.25f;
    [Min(0.1f)] public float debrisClearance = 1.1f;
    [Min(0f)] public float debrisAvoidanceStrength = 1.6f;
    [Min(0.02f)] public float debrisCheckInterval = 0.1f;
    [Min(0.1f)] public float debrisTurnResponsiveness = 7f;
    [Min(0.1f)] public float debrisAvoidanceHoldTime = 0.55f;

    public bool IsDestroyed { get; private set; }

    protected int currentHp;
    private float debrisCheckTimer;
    private Vector2 targetAvoidanceDirection;
    private Vector2 smoothAvoidanceDirection;
    private float debrisAvoidanceHoldTimer;
    private Collider2D[] cachedOwnerColliders;

    protected bool IsDebrisAvoidanceActive =>
        debrisAvoidanceHoldTimer > 0f
        || targetAvoidanceDirection.sqrMagnitude > 0.001f
        || smoothAvoidanceDirection.sqrMagnitude > 0.01f;

    protected virtual void Awake()
    {
        cachedOwnerColliders = GetComponentsInChildren<Collider2D>(true);
        MissileController.CacheOwnerColliders(gameObject, cachedOwnerColliders);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry()
    {
        ActiveEnemies.Clear();
        EnemySpawned = null;
        EnemyRemoved = null;
    }

    protected virtual void OnEnable()
    {
        IsDestroyed = false;
        currentHp = maxHp;
        targetAvoidanceDirection = Vector2.zero;
        smoothAvoidanceDirection = Vector2.zero;
        debrisAvoidanceHoldTimer = 0f;
        int staggerIndex = Mathf.Abs(GetInstanceID()) % 10;
        debrisCheckTimer = debrisCheckInterval * staggerIndex * 0.1f;

        if (ActiveEnemies.Add(this))
            EnemySpawned?.Invoke(this);
    }

    protected virtual void OnDisable()
    {
        if (ActiveEnemies.Remove(this))
            EnemyRemoved?.Invoke(this);
    }

    protected virtual void OnDestroy()
    {
        MissileController.RemoveOwnerColliderCache(gameObject);
    }

    protected virtual void Start()
    {
        currentHp = maxHp;
    }

    public virtual void TakeDamage(int damage, Vector2 hitDirection = default)
    {
        if (IsDestroyed) return;

        currentHp -= damage;

        if (currentHp <= 0)
        {
            DestroyEnemy(hitDirection);
        }
    }

    public void DestroyByEnvironment(Vector2 hitDirection)
    {
        if (IsDestroyed) return;

        DestroyEnemy(hitDirection, false);
    }

    protected virtual void DestroyEnemy(Vector2 hitDirection, bool awardKill = true)
    {
        IsDestroyed = true;

        // 파괴될 때마다 스코어(kill) 1 증가
        if (awardKill && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddKill();
        }

        SpawnExplosion(hitDirection);

        Destroy(gameObject);
    }

    protected void SpawnExplosion(Vector2 dir)
    {
        if (explosionEffect == null) return;

        if (dir == Vector2.zero)
            dir = Random.insideUnitCircle.normalized;

        PooledParticleEffect effect = ParticleEffectPool.Spawn(
            explosionEffect,
            transform.position,
            Quaternion.identity);
        if (effect == null) return;

        ParticleSystem ps = effect.ParticleSystem;
        if (ps == null)
        {
            effect.Play();
            return;
        }

        var main = ps.main;
        main.startSpeed = 0;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;

        float power = 8f;
        float spread = 3f;

        velocity.x = new ParticleSystem.MinMaxCurve(dir.x * power - spread, dir.x * power + spread);
        velocity.y = new ParticleSystem.MinMaxCurve(dir.y * power - spread, dir.y * power + spread);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        effect.Play();
    }

    protected Vector2 ApplyDebrisAvoidance(Vector2 desiredDirection, float movementSpeed)
    {
        if (desiredDirection.sqrMagnitude < 0.0001f)
            return desiredDirection;

        desiredDirection.Normalize();
        debrisAvoidanceHoldTimer = Mathf.Max(
            0f,
            debrisAvoidanceHoldTimer - Time.deltaTime);

        if (!avoidDebris || debrisAvoidanceStrength <= 0f)
        {
            targetAvoidanceDirection = Vector2.zero;
            smoothAvoidanceDirection = Vector2.zero;
            debrisAvoidanceHoldTimer = 0f;
            return desiredDirection;
        }

        debrisCheckTimer -= Time.deltaTime;
        if (debrisCheckTimer <= 0f)
        {
            debrisCheckTimer = Mathf.Max(0.02f, debrisCheckInterval);
            bool hasThreat = DebrisController.TryGetAvoidanceDirection(
                transform.position,
                desiredDirection * Mathf.Max(0.1f, movementSpeed),
                debrisLookAheadTime,
                debrisClearance,
                out Vector2 avoidanceDirection);

            if (hasThreat)
            {
                bool isNewAvoidance = targetAvoidanceDirection.sqrMagnitude < 0.001f
                    && smoothAvoidanceDirection.sqrMagnitude < 0.001f;

                if (isNewAvoidance)
                {
                    targetAvoidanceDirection = avoidanceDirection;
                    smoothAvoidanceDirection = avoidanceDirection;
                }
                else if (Vector2.Dot(targetAvoidanceDirection, avoidanceDirection) > -0.2f)
                {
                    Vector2 combinedDirection = targetAvoidanceDirection + avoidanceDirection;
                    if (combinedDirection.sqrMagnitude > 0.001f)
                        targetAvoidanceDirection = combinedDirection.normalized;
                }

                debrisAvoidanceHoldTimer = Mathf.Max(0.1f, debrisAvoidanceHoldTime);
            }
            else if (debrisAvoidanceHoldTimer <= 0f)
            {
                targetAvoidanceDirection = Vector2.zero;
            }
        }

        smoothAvoidanceDirection = Vector2.MoveTowards(
            smoothAvoidanceDirection,
            targetAvoidanceDirection,
            debrisTurnResponsiveness * Time.deltaTime);

        Vector2 steeredDirection = IsDebrisAvoidanceActive
            ? desiredDirection * 0.15f
                + smoothAvoidanceDirection * Mathf.Max(1f, debrisAvoidanceStrength)
            : desiredDirection;

        return steeredDirection.sqrMagnitude > 0.0001f
            ? steeredDirection.normalized
            : desiredDirection;
    }
}
