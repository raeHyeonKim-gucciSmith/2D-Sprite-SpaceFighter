using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpaceMineSpawner : MonoBehaviour
{
    // 적 종류 하나당 하나씩 등록하는 스폰 설정 (프리팹마다 독립적인 주기를 가짐)
    [System.Serializable]
    public class SpawnEntry
    {
        public string label = "Enemy"; // 인스펙터에서 구분하기 편하도록 붙이는 이름표 (예: "지뢰", "전투기")
        public GameObject prefab;
        public float minSpawnInterval = 3f;
        public float maxSpawnInterval = 7f;

        [HideInInspector] public float timer;
        [HideInInspector] public float nextSpawnTime;
    }

    [Header("Spawn Settings")]
    public SpawnEntry[] spawnEntries; // 여기에 지뢰, 전투기 등 종류별로 하나씩 등록

    [Header("Spawn Area")]
    public Camera targetCamera;                 // 비워두면 Camera.main 자동 사용
    public float spawnMargin = 2f;               // 화면 가장자리에서 얼마나 떨어져서 생성할지

    [Header("Limit")]
    public int maxAliveEnemies = 8;              // 모든 종류를 합친 동시 최대 생존 개수 (0이면 무제한)

    [Header("Missile Pool")]
    [Min(0)] public int enemyMissilePrewarmCount = 12;
    [Min(1)] public int missilesPrewarmedPerFrame = 1;

    public bool skipSpaceMines = true;

    private readonly List<GameObject> aliveEnemies = new List<GameObject>();
    private readonly HashSet<GameObject> skippedPrefabs = new HashSet<GameObject>();

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        HashSet<GameObject> missilePrefabsToPrewarm = new HashSet<GameObject>();
        foreach (SpawnEntry entry in spawnEntries)
        {
            if (entry.prefab != null)
            {
                if (skipSpaceMines && entry.prefab.GetComponent<SpaceMineController>() != null)
                    skippedPrefabs.Add(entry.prefab);

                FighterEnemyController fighter = entry.prefab.GetComponent<FighterEnemyController>();
                if (fighter != null && fighter.missilePrefab != null)
                    missilePrefabsToPrewarm.Add(fighter.missilePrefab);
            }

            SetNextSpawnTime(entry);
        }

        foreach (GameObject missilePrefab in missilePrefabsToPrewarm)
            StartCoroutine(PrewarmMissilesGradually(missilePrefab));
    }

    IEnumerator PrewarmMissilesGradually(GameObject missilePrefab)
    {
        int targetCount = Mathf.Max(0, enemyMissilePrewarmCount);
        int batchSize = Mathf.Max(1, missilesPrewarmedPerFrame);

        for (int warmedCount = batchSize;
             warmedCount <= targetCount;
             warmedCount += batchSize)
        {
            MissileController.Prewarm(
                missilePrefab,
                Mathf.Min(warmedCount, targetCount));
            yield return null;
        }

        if (targetCount > 0 && targetCount % batchSize != 0)
            MissileController.Prewarm(missilePrefab, targetCount);
    }

    void Update()
    {
        aliveEnemies.RemoveAll(e => e == null);

        foreach (SpawnEntry entry in spawnEntries)
        {
            if (entry.prefab == null) continue;
            if (skippedPrefabs.Contains(entry.prefab)) continue;

            entry.timer += Time.deltaTime;

            if (entry.timer >= entry.nextSpawnTime)
            {
                if (maxAliveEnemies <= 0 || aliveEnemies.Count < maxAliveEnemies)
                {
                    SpawnEnemy(entry.prefab);
                }

                entry.timer = 0f;
                SetNextSpawnTime(entry);
            }
        }
    }

    void SetNextSpawnTime(SpawnEntry entry)
    {
        entry.nextSpawnTime = Random.Range(entry.minSpawnInterval, entry.maxSpawnInterval);
    }

    void SpawnEnemy(GameObject prefab)
    {
        if (targetCamera == null) return;

        Vector3 spawnPos = GetRandomEdgePosition();
        GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);

        aliveEnemies.Add(enemy);
    }

    // 카메라 시야 바깥, 상/하/좌/우 중 랜덤한 한 변에서 스폰 위치를 계산
    Vector3 GetRandomEdgePosition()
    {
        float camHeight = targetCamera.orthographicSize;
        float camWidth = camHeight * targetCamera.aspect;
        Vector3 camPos = targetCamera.transform.position;

        float minX = camPos.x - camWidth - spawnMargin;
        float maxX = camPos.x + camWidth + spawnMargin;
        float minY = camPos.y - camHeight - spawnMargin;
        float maxY = camPos.y + camHeight + spawnMargin;

        int edge = Random.Range(0, 4);
        float x, y;

        switch (edge)
        {
            case 0: // 왼쪽
                x = minX;
                y = Random.Range(minY, maxY);
                break;
            case 1: // 오른쪽
                x = maxX;
                y = Random.Range(minY, maxY);
                break;
            case 2: // 위쪽
                x = Random.Range(minX, maxX);
                y = maxY;
                break;
            default: // 아래쪽
                x = Random.Range(minX, maxX);
                y = minY;
                break;
        }

        return new Vector3(x, y, 0f);
    }
}
