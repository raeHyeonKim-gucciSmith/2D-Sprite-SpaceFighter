using System.Collections; // [추가] 코루틴(IEnumerator) 사용을 위해 필요합니다.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class PlayerController : MonoBehaviour
{
    public UIDocument uiDocument;
    private Label scoreText;
    private Label highScoreText;
    private Button restartButton;
    public InputAction moveForward;
    public InputAction lookPosition;
    public float thrustForce = 1f;
    public float maxSpeed = 5f;
    public float accel = 1f;
    public GameObject borderParent;

    Rigidbody2D rb;
    public GameObject boosterFlame;
    private float elapsedTime = 0f;
    private float score = 0f;
    public float scoreMultiplier = 10f;
    public GameObject explosionEffect;

    private const string HighScoreKey = "HighScore";

    void Start()
    {
        moveForward.Enable();
        lookPosition.Enable();
        rb = GetComponent<Rigidbody2D>();

        scoreText = uiDocument.rootVisualElement.Q<Label>("ScoreLabel");
        highScoreText = uiDocument.rootVisualElement.Q<Label>("HighScoreLabel");
        restartButton = uiDocument.rootVisualElement.Q<Button>("RestartButton");

        restartButton.style.display = DisplayStyle.None;
        restartButton.clicked += ReloadScene;

        int savedHighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        if (highScoreText != null)
        {
            highScoreText.text = "BEST: " + savedHighScore;
        }
    }

    void Update()
    {
        UpdateScore();
        MovePlayer();
        BoosterFlame();
    }

    void UpdateScore()
    {
        elapsedTime += Time.deltaTime;
        score = Mathf.FloorToInt(elapsedTime * scoreMultiplier);
        scoreText.text = "DOGE: " + score;
    }

    void MovePlayer()
    {
        if (moveForward.IsPressed())
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(lookPosition.ReadValue<Vector2>());
            Vector2 direction = (mousePos - transform.position).normalized;
            transform.up = direction;

            rb.AddForce(direction * thrustForce * accel);
            if (rb.linearVelocity.magnitude > maxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }
        }
    }

    void BoosterFlame()
    {
        if(moveForward.WasPressedThisFrame())
        {
            boosterFlame.SetActive(true);
        }
        if (moveForward.WasReleasedThisFrame())
        {
            boosterFlame.SetActive(false);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleGameOverHighScore();

        Vector2 dir;

        if (collision.gameObject.CompareTag("Border"))
            dir = collision.GetContact(0).normal;
        else
            dir = rb.linearVelocity.normalized;

        SpawnExplosion(collision.GetContact(0).point, dir);

        Destroy(gameObject);
        restartButton.style.display = DisplayStyle.Flex;
        borderParent.SetActive(false);
    }

    void SpawnExplosion(Vector2 point, Vector2 dir)
    {
        if (dir == Vector2.zero)
            dir = Vector2.up;

        PooledParticleEffect effect = ParticleEffectPool.Spawn(
            explosionEffect,
            point,
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

    void HandleGameOverHighScore()
    {
        int currentScore = Mathf.FloorToInt(score);
        int savedHighScore = PlayerPrefs.GetInt(HighScoreKey, 0);

        if (currentScore > savedHighScore)
        {
            PlayerPrefs.SetInt(HighScoreKey, currentScore);
            PlayerPrefs.Save();

            if (highScoreText != null)
            {
                highScoreText.text = "NEW BEST: " + currentScore;
                // 처음 시작 색상을 빨간색으로 지정합니다.
                highScoreText.style.color = Color.red;

                if (uiDocument != null)
                {
                    // 빨간색과 흰색이 번갈아 깜빡이는 코루틴 시작
                    uiDocument.StartCoroutine(BlinkHighScoreLabel());
                }
            }
        }
    }

    // [수정] 빨간색과 흰색 교차 깜빡임 코루틴
    IEnumerator BlinkHighScoreLabel()
    {
        // 씬이 바뀌기 전까지 무한 반복
        while (highScoreText != null)
        {
            // 현재 글자 색상이 빨간색이면 흰색으로, 아니면 빨간색으로 변경합니다.
            if (highScoreText.style.color.value == Color.red)
            {
                highScoreText.style.color = Color.white;
            }
            else
            {
                highScoreText.style.color = Color.red;
            }

            // 0.2초 동안 대기 (숫자가 작아질수록 더 빠르게 깜빡입니다)
            yield return new WaitForSecondsRealtime(0.2f);
        }
    }

    void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
