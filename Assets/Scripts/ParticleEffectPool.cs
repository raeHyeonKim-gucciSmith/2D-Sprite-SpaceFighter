using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PooledParticleEffect : MonoBehaviour
{
    public ParticleSystem ParticleSystem { get; private set; }
    internal int PoolKey { get; private set; }

    private float remainingLifetime;
    private bool isPlaying;

    void Awake()
    {
        ParticleSystem = GetComponent<ParticleSystem>();
    }

    internal void Initialize(int poolKey)
    {
        PoolKey = poolKey;
        if (ParticleSystem == null)
            ParticleSystem = GetComponent<ParticleSystem>();
    }

    internal void Prepare(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        isPlaying = false;
        remainingLifetime = 0f;

        if (ParticleSystem != null)
        {
            ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.Clear(true);
            var main = ParticleSystem.main;
            main.stopAction = ParticleSystemStopAction.None;
        }
    }

    public void Play()
    {
        if (ParticleSystem == null)
        {
            remainingLifetime = 0.1f;
            isPlaying = true;
            return;
        }

        ParticleSystem.Play(true);
        var main = ParticleSystem.main;
        remainingLifetime = Mathf.Max(
            0.1f,
            main.duration + main.startLifetime.constantMax + 0.1f);
        isPlaying = true;
    }

    void Update()
    {
        if (!isPlaying)
            return;

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
            ParticleEffectPool.Release(this);
    }
}

public static class ParticleEffectPool
{
    private static readonly Dictionary<int, Stack<PooledParticleEffect>> Pools =
        new Dictionary<int, Stack<PooledParticleEffect>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPools()
    {
        Pools.Clear();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Pools.Clear();
    }

    public static PooledParticleEffect Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (prefab == null)
            return null;

        int key = prefab.GetInstanceID();
        PooledParticleEffect effect = null;

        if (Pools.TryGetValue(key, out Stack<PooledParticleEffect> pool))
        {
            while (pool.Count > 0 && effect == null)
                effect = pool.Pop();
        }

        if (effect == null)
        {
            GameObject effectObject = Object.Instantiate(prefab, position, rotation);
            effect = effectObject.GetComponent<PooledParticleEffect>();
            if (effect == null)
                effect = effectObject.AddComponent<PooledParticleEffect>();
            effect.Initialize(key);
        }
        else
        {
            effect.transform.SetPositionAndRotation(position, rotation);
            effect.gameObject.SetActive(true);
        }

        effect.Prepare(position, rotation);
        return effect;
    }

    internal static void Release(PooledParticleEffect effect)
    {
        if (effect == null)
            return;

        if (effect.ParticleSystem != null)
            effect.ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        int key = effect.PoolKey;
        effect.gameObject.SetActive(false);

        if (!Pools.TryGetValue(key, out Stack<PooledParticleEffect> pool))
        {
            pool = new Stack<PooledParticleEffect>();
            Pools.Add(key, pool);
        }

        pool.Push(effect);
    }
}
