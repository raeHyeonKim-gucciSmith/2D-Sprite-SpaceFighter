using System.Collections.Generic;
using UnityEngine;

public class DebrisSpawner : MonoBehaviour
{
    [System.Serializable]
    public class DebrisSpawnEntry
    {
        public string label = "Debris";
        public GameObject prefab;
        public float minSpawnInterval = 2f;
        public float maxSpawnInterval = 4f;

        [HideInInspector] public float timer;
        [HideInInspector] public float nextSpawnTime;
    }

    [Header("Spawn Settings")]
    public DebrisSpawnEntry[] spawnEntries;

    [Header("Spawn Area")]
    public Camera targetCamera;
    public float spawnMargin = 2f;

    [Header("Movement")]
    public bool aimAcrossScreen = true;
    public float directionSpread = 30f;

    [Header("Limit")]
    public int maxAliveDebris = 20;

    private readonly List<GameObject> aliveDebris = new List<GameObject>();

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        foreach (DebrisSpawnEntry entry in spawnEntries)
            SetNextSpawnTime(entry);
    }

    void Update()
    {
        for (int i = aliveDebris.Count - 1; i >= 0; i--)
        {
            GameObject debrisObject = aliveDebris[i];
            if (debrisObject == null || !debrisObject.activeSelf)
                aliveDebris.RemoveAt(i);
        }

        foreach (DebrisSpawnEntry entry in spawnEntries)
        {
            if (entry.prefab == null) continue;

            entry.timer += Time.deltaTime;

            if (entry.timer < entry.nextSpawnTime)
                continue;

            if (maxAliveDebris <= 0 || aliveDebris.Count < maxAliveDebris)
                SpawnDebris(entry.prefab);

            entry.timer = 0f;
            SetNextSpawnTime(entry);
        }
    }

    void SetNextSpawnTime(DebrisSpawnEntry entry)
    {
        entry.nextSpawnTime = Random.Range(entry.minSpawnInterval, entry.maxSpawnInterval);
    }

    void SpawnDebris(GameObject prefab)
    {
        if (targetCamera == null) return;

        Vector3 spawnPos = GetRandomEdgePosition(out Vector2 inwardDirection);
        Quaternion rotation = Quaternion.identity;

        if (aimAcrossScreen)
        {
            float angleOffset = Random.Range(-directionSpread, directionSpread);
            Vector2 dir = Quaternion.Euler(0f, 0f, angleOffset) * inwardDirection;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            rotation = Quaternion.Euler(0f, 0f, angle);
        }

        DebrisController debris = DebrisController.Spawn(prefab, spawnPos, rotation);
        if (debris != null)
            aliveDebris.Add(debris.gameObject);
    }

    Vector3 GetRandomEdgePosition(out Vector2 inwardDirection)
    {
        float camHeight = targetCamera.orthographicSize;
        float camWidth = camHeight * targetCamera.aspect;
        Vector3 camPos = targetCamera.transform.position;

        float minX = camPos.x - camWidth - spawnMargin;
        float maxX = camPos.x + camWidth + spawnMargin;
        float minY = camPos.y - camHeight - spawnMargin;
        float maxY = camPos.y + camHeight + spawnMargin;

        int edge = Random.Range(0, 4);
        float x;
        float y;

        switch (edge)
        {
            case 0:
                x = minX;
                y = Random.Range(minY, maxY);
                inwardDirection = Vector2.right;
                break;
            case 1:
                x = maxX;
                y = Random.Range(minY, maxY);
                inwardDirection = Vector2.left;
                break;
            case 2:
                x = Random.Range(minX, maxX);
                y = maxY;
                inwardDirection = Vector2.down;
                break;
            default:
                x = Random.Range(minX, maxX);
                y = minY;
                inwardDirection = Vector2.up;
                break;
        }

        return new Vector3(x, y, 0f);
    }
}
