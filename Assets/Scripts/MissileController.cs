using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class MissileController : MonoBehaviour
{
    private static readonly Dictionary<int, Stack<MissileController>> Pools =
        new Dictionary<int, Stack<MissileController>>();
    private static readonly Dictionary<GameObject, Collider2D[]> OwnerColliderCache =
        new Dictionary<GameObject, Collider2D[]>();
    private static AudioSource sharedLaunchAudioSource;

    [Header("Missile")]
    public float speed = 8f;
    public float rotateSpeed = 360f;
    public float lifeTime = 5f;
    public int damage = 1;
    public MissileOwner owner = MissileOwner.Player;

    [Header("Impact Flash")]
    public GameObject impactFlashPrefab;
    public Color impactFlashColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("Trail Smoke")]
    public ParticleSystem smokeParticle;
    public float smokeFadeDuration = 1f;

    private EnemyController target;
    private bool isHoming = true;
    private Vector2 straightDirection;
    private GameObject ownerObject;
    private int poolKey;
    private float remainingLifeTime;
    private float recycleDelayRemaining;
    private bool isRecycling;
    private Collider2D[] missileColliders;
    private bool[] colliderEnabledStates;
    private Renderer[] bodyRenderers;
    private bool[] rendererEnabledStates;
    private readonly List<Collider2D> ignoredOwnerColliders = new List<Collider2D>();
    private AudioSource launchAudioSource;
    private AudioClip launchAudioClip;
    private float launchAudioVolume = 1f;
    private bool launchSoundPending;
    private Transform smokeOriginalParent;
    private Vector3 smokeOriginalLocalPosition;
    private Quaternion smokeOriginalLocalRotation;
    private Vector3 smokeOriginalLocalScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPools()
    {
        Pools.Clear();
        OwnerColliderCache.Clear();
        sharedLaunchAudioSource = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Pools.Clear();
        OwnerColliderCache.Clear();
    }

    public static void CacheOwnerColliders(GameObject ownerObject, Collider2D[] colliders)
    {
        if (ownerObject == null || colliders == null)
            return;

        OwnerColliderCache[ownerObject] = colliders;
    }

    public static void RemoveOwnerColliderCache(GameObject ownerObject)
    {
        if (ownerObject != null)
            OwnerColliderCache.Remove(ownerObject);
    }

    public static MissileController Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        int key = prefab.GetInstanceID();
        MissileController missile = null;

        if (Pools.TryGetValue(key, out Stack<MissileController> pool))
        {
            while (pool.Count > 0 && missile == null)
                missile = pool.Pop();
        }

        if (missile == null)
        {
            GameObject missileObject = Instantiate(prefab, position, rotation);
            missile = missileObject.GetComponent<MissileController>();

            if (missile == null)
            {
                Destroy(missileObject);
                return null;
            }
        }
        else
        {
            missile.transform.SetPositionAndRotation(position, rotation);
            missile.gameObject.SetActive(true);
        }

        missile.poolKey = key;
        missile.transform.SetPositionAndRotation(position, rotation);
        missile.PrepareForSpawn();
        return missile;
    }

    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0)
            return;

        int key = prefab.GetInstanceID();
        if (!Pools.TryGetValue(key, out Stack<MissileController> pool))
        {
            pool = new Stack<MissileController>(count);
            Pools.Add(key, pool);
        }

        while (pool.Count < count)
        {
            GameObject missileObject = Instantiate(prefab);
            MissileController missile = missileObject.GetComponent<MissileController>();
            if (missile == null)
            {
                Destroy(missileObject);
                break;
            }

            missile.poolKey = key;
            missile.WarmForPool();
            missileObject.SetActive(false);
            pool.Push(missile);
        }
    }

    void Awake()
    {
        missileColliders = GetComponentsInChildren<Collider2D>(true);
        colliderEnabledStates = new bool[missileColliders.Length];
        for (int i = 0; i < missileColliders.Length; i++)
            colliderEnabledStates[i] = missileColliders[i] != null && missileColliders[i].enabled;

        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        List<Renderer> visibleBodyRenderers = new List<Renderer>(allRenderers.Length);
        foreach (Renderer renderer in allRenderers)
        {
            if (renderer == null)
                continue;
            if (smokeParticle != null && renderer.transform.IsChildOf(smokeParticle.transform))
                continue;

            visibleBodyRenderers.Add(renderer);
        }

        bodyRenderers = visibleBodyRenderers.ToArray();
        rendererEnabledStates = new bool[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; i++)
            rendererEnabledStates[i] = bodyRenderers[i] != null && bodyRenderers[i].enabled;

        launchAudioSource = GetComponent<AudioSource>();
        if (launchAudioSource != null)
        {
            launchAudioSource.playOnAwake = false;
            launchAudioSource.Stop();
            launchAudioClip = launchAudioSource.clip;
            launchAudioVolume = launchAudioSource.volume;
#if !UNITY_WEBGL
            if (launchAudioClip != null)
                launchAudioClip.LoadAudioData();
#endif
            EnsureSharedLaunchAudioSource(launchAudioSource);
            launchAudioSource.enabled = false;
        }

        if (smokeParticle != null)
        {
            var main = smokeParticle.main;
            main.playOnAwake = false;
            smokeParticle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            smokeOriginalParent = smokeParticle.transform.parent;
            smokeOriginalLocalPosition = smokeParticle.transform.localPosition;
            smokeOriginalLocalRotation = smokeParticle.transform.localRotation;
            smokeOriginalLocalScale = smokeParticle.transform.localScale;
        }
    }

    void PrepareForSpawn()
    {
        RestoreOwnerCollisions();
        ownerObject = null;
        target = null;
        isHoming = true;
        straightDirection = transform.up;
        isRecycling = false;
        recycleDelayRemaining = 0f;
        remainingLifeTime = lifeTime;
        launchSoundPending = launchAudioClip != null;

        for (int i = 0; i < missileColliders.Length; i++)
        {
            if (missileColliders[i] != null)
                missileColliders[i].enabled = colliderEnabledStates[i];
        }

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null)
                bodyRenderers[i].enabled = rendererEnabledStates[i];
        }

        if (smokeParticle != null)
        {
            RestoreSmokeParent();
            smokeParticle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            smokeParticle.Play(false);
        }
    }

    public void SetOwner(MissileOwner newOwner, GameObject newOwnerObject)
    {
        RestoreOwnerCollisions();
        owner = newOwner;
        ownerObject = newOwnerObject;
        IgnoreOwnerCollisions();
        PlayPendingLaunchSound();
    }

    public void SetTarget(EnemyController newTarget)
    {
        target = newTarget;
        isHoming = true;
    }

    public void FireStraight()
    {
        isHoming = false;
        straightDirection = transform.up;
    }

    public void FireStraight(Vector2 direction)
    {
        isHoming = false;

        straightDirection = direction == Vector2.zero
            ? (Vector2)transform.up
            : direction.normalized;

        transform.up = straightDirection;
    }

    void Update()
    {
        if (isRecycling)
        {
            recycleDelayRemaining -= Time.deltaTime;
            if (recycleDelayRemaining <= 0f)
                CompleteRecycle();
            return;
        }

        remainingLifeTime -= Time.deltaTime;
        if (remainingLifeTime <= 0f)
        {
            Recycle();
            return;
        }

        if (isHoming)
        {
            if (target == null || target.IsDestroyed)
            {
                Recycle();
                return;
            }

            MoveToTarget();
        }
        else
        {
            MoveStraight();
        }
    }

    void MoveToTarget()
    {
        Vector2 dir = target.transform.position - transform.position;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );

        transform.position += transform.up * speed * Time.deltaTime;
    }

    void MoveStraight()
    {
        transform.position += (Vector3)straightDirection * speed * Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (isRecycling)
            return;

        if (IsOwnerCollider(collision))
            return;

        DebrisController debris = collision.GetComponentInParent<DebrisController>();
        if (debris != null)
        {
            SpawnImpactFlash(transform.position);
            Recycle();
            return;
        }

        if (owner == MissileOwner.Player)
        {
            EnemyController enemy = collision.GetComponentInParent<EnemyController>();

            if (enemy != null)
            {
                SpawnImpactFlash(transform.position);
                enemy.TakeDamage(damage, transform.up);
                Recycle();
            }

            return;
        }

        if (owner == MissileOwner.Enemy)
        {
            FlightController player = collision.GetComponentInParent<FlightController>();

            if (player != null)
            {
                SpawnImpactFlash(transform.position);
                player.Die(transform.position, transform.up);
                Recycle();
            }
        }
    }

    void OnDestroy()
    {
        RestoreOwnerCollisions();
    }

    void IgnoreOwnerCollisions()
    {
        if (ownerObject == null) return;

        if (!OwnerColliderCache.TryGetValue(ownerObject, out Collider2D[] ownerColliders))
        {
            ownerColliders = ownerObject.GetComponentsInChildren<Collider2D>();
            OwnerColliderCache[ownerObject] = ownerColliders;
        }

        ignoredOwnerColliders.Clear();

        foreach (Collider2D missileCollider in missileColliders)
        {
            foreach (Collider2D ownerCollider in ownerColliders)
            {
                if (missileCollider != null && ownerCollider != null)
                {
                    Physics2D.IgnoreCollision(missileCollider, ownerCollider);
                    if (!ignoredOwnerColliders.Contains(ownerCollider))
                        ignoredOwnerColliders.Add(ownerCollider);
                }
            }
        }
    }

    void RestoreOwnerCollisions()
    {
        if (missileColliders != null)
        {
            foreach (Collider2D missileCollider in missileColliders)
            {
                if (missileCollider == null)
                    continue;

                foreach (Collider2D ownerCollider in ignoredOwnerColliders)
                {
                    if (ownerCollider != null)
                        Physics2D.IgnoreCollision(missileCollider, ownerCollider, false);
                }
            }
        }

        ignoredOwnerColliders.Clear();
    }

    bool IsOwnerCollider(Collider2D collision)
    {
        if (ownerObject == null || collision == null) return false;

        return collision.transform == ownerObject.transform
            || collision.transform.IsChildOf(ownerObject.transform);
    }

    void Recycle()
    {
        if (isRecycling)
            return;

        isRecycling = true;
        RestoreOwnerCollisions();

        for (int i = 0; i < missileColliders.Length; i++)
        {
            if (missileColliders[i] != null)
                missileColliders[i].enabled = false;
        }

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null)
                bodyRenderers[i].enabled = false;
        }

        if (smokeParticle == null || smokeFadeDuration <= 0f)
        {
            CompleteRecycle();
            return;
        }

        smokeParticle.transform.SetParent(null);
        smokeParticle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        recycleDelayRemaining = smokeFadeDuration;
    }

    void CompleteRecycle()
    {
        if (smokeParticle != null)
        {
            smokeParticle.Clear(false);
            RestoreSmokeParent();
        }

        target = null;
        ownerObject = null;
        gameObject.SetActive(false);

        if (poolKey == 0)
        {
            Destroy(gameObject);
            return;
        }

        if (!Pools.TryGetValue(poolKey, out Stack<MissileController> pool))
        {
            pool = new Stack<MissileController>();
            Pools.Add(poolKey, pool);
        }

        pool.Push(this);
    }

    void RestoreSmokeParent()
    {
        if (smokeParticle == null)
            return;

        Transform parent = smokeOriginalParent != null ? smokeOriginalParent : transform;
        smokeParticle.transform.SetParent(parent, false);
        smokeParticle.transform.localPosition = smokeOriginalLocalPosition;
        smokeParticle.transform.localRotation = smokeOriginalLocalRotation;
        smokeParticle.transform.localScale = smokeOriginalLocalScale;
    }

    static void EnsureSharedLaunchAudioSource(AudioSource template)
    {
        if (sharedLaunchAudioSource != null || template == null)
            return;

        GameObject audioObject = new GameObject("Missile Launch SFX");
        sharedLaunchAudioSource = audioObject.AddComponent<AudioSource>();
        sharedLaunchAudioSource.playOnAwake = false;
        sharedLaunchAudioSource.loop = false;
        sharedLaunchAudioSource.spatialBlend = 0f;
        sharedLaunchAudioSource.volume = 1f;
        sharedLaunchAudioSource.pitch = template.pitch;
        sharedLaunchAudioSource.priority = template.priority;
        sharedLaunchAudioSource.outputAudioMixerGroup = template.outputAudioMixerGroup;
    }

    void WarmForPool()
    {
        if (smokeParticle == null)
            return;

        RestoreSmokeParent();
        smokeParticle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        smokeParticle.Simulate(0.02f, false, true, false);
        smokeParticle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void PlayPendingLaunchSound()
    {
        if (!launchSoundPending || launchAudioClip == null)
            return;

        launchSoundPending = false;

#if UNITY_WEBGL
        // Web Audio can create a main-thread stall when an enemy launch SFX starts.
        // Player launch audio is kept; enemy launch audio is skipped only on WebGL.
        if (owner == MissileOwner.Enemy)
            return;
#endif

        if (sharedLaunchAudioSource == null)
            EnsureSharedLaunchAudioSource(launchAudioSource);

        if (sharedLaunchAudioSource != null)
            sharedLaunchAudioSource.PlayOneShot(launchAudioClip, launchAudioVolume);
    }

    void SpawnImpactFlash(Vector3 position)
    {
        if (impactFlashPrefab == null) return;

        PooledParticleEffect flash = ParticleEffectPool.Spawn(
            impactFlashPrefab,
            position,
            Quaternion.identity);
        if (flash == null) return;

        ParticleSystem ps = flash.ParticleSystem;
        if (ps == null)
        {
            flash.Play();
            return;
        }

        var main = ps.main;
        main.startColor = impactFlashColor;

        flash.Play();
    }
}
