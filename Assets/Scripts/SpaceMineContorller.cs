using UnityEngine;

public class SpaceMineController : EnemyController
{
    [Header("Movement")]
    public float moveSpeed = 1.5f;
    public float speedVariance = 0.5f;          // 방향이 바뀔 때마다 속도도 살짝 랜덤하게

    [Header("Direction Change")]
    public float minDirectionChangeTime = 1f;   // 방향 전환 최소 주기
    public float maxDirectionChangeTime = 3f;   // 방향 전환 최대 주기
    public float turnSmoothness = 2f;           // 클수록 목표 방향으로 빠르게 회전

    [Header("Wobble (유기적인 흔들림)")]
    public float wobbleStrength = 0.3f;         // 흔들림 강도
    public float wobbleFrequency = 0.5f;        // 흔들림 속도

    private Vector2 moveDir;
    private Vector2 targetDir;
    private float timer;
    private float directionChangeTime;
    private float currentSpeed;
    private float noiseOffset;

    protected override void Start()
    {
        base.Start();

        // 지뢰마다 노이즈 패턴이 겹치지 않도록 오프셋을 랜덤 지정
        noiseOffset = Random.Range(0f, 1000f);

        PickRandomDirection();
        moveDir = targetDir;
        PickRandomSpeed();
    }

    void Update()
    {
        if (IsDestroyed) return;

        MoveRandomly();

        timer += Time.deltaTime;

        if (timer >= directionChangeTime)
        {
            PickRandomDirection();
            PickRandomSpeed();
            timer = 0f;
        }
    }

    void MoveRandomly()
    {
        // 목표 방향으로 즉시 꺾이지 않고 부드럽게 커브를 그리며 회전
        moveDir = Vector2.Lerp(moveDir, targetDir, turnSmoothness * Time.deltaTime).normalized;

        // 펄린 노이즈로 진행 방향에 수직인 미세한 흔들림을 더해서 표류하는 느낌을 줌
        float noise = (Mathf.PerlinNoise(Time.time * wobbleFrequency, noiseOffset) - 0.5f) * 2f;
        Vector2 perpendicular = new Vector2(-moveDir.y, moveDir.x);
        Vector2 finalDir = (moveDir + perpendicular * noise * wobbleStrength).normalized;

        transform.position += (Vector3)finalDir * currentSpeed * Time.deltaTime;
    }

    void PickRandomDirection()
    {
        targetDir = Random.insideUnitCircle.normalized;
        directionChangeTime = Random.Range(minDirectionChangeTime, maxDirectionChangeTime);
    }

    void PickRandomSpeed()
    {
        currentSpeed = Mathf.Max(0f, moveSpeed + Random.Range(-speedVariance, speedVariance));
    }
}
