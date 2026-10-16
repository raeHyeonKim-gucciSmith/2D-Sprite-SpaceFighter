using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ScoreUIController : MonoBehaviour
{
    private Label scoreLabel;
    private Label highScoreLabel;

    private bool isHighScoreBlinking = false;
    private float blinkTimer = 0f;
    private float blinkDuration = 1.2f;
    private float blinkInterval = 0.12f;

    void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        scoreLabel = root.Q<Label>("Score");
        highScoreLabel = root.Q<Label>("HighScore");

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnKillCountChanged += UpdateScoreText;
            ScoreManager.Instance.OnHighScoreChanged += UpdateHighScoreText;

            UpdateScoreText(ScoreManager.Instance.KillCount);
            UpdateHighScoreText(ScoreManager.Instance.HighScore, false);
        }
        else
        {
            Debug.LogWarning("ScoreUIController: ScoreManager.Instance not found.");
        }
    }

    void Update()
    {
        UpdateHighScoreBlink();
    }

    void OnDestroy()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnKillCountChanged -= UpdateScoreText;
            ScoreManager.Instance.OnHighScoreChanged -= UpdateHighScoreText;
        }
    }

    void UpdateScoreText(int killCount)
    {
        if (scoreLabel == null) return;

        scoreLabel.text = $"KILL : {killCount}";
    }

    void UpdateHighScoreText(int highScore)
    {
        UpdateHighScoreText(highScore, true);
    }

    void UpdateHighScoreText(int highScore, bool blink)
    {
        if (highScoreLabel == null) return;

        highScoreLabel.text = $"ACE : {highScore}";

        if (blink)
        {
            isHighScoreBlinking = true;
            blinkTimer = 0f;
        }
    }

    void UpdateHighScoreBlink()
    {
        if (!isHighScoreBlinking || highScoreLabel == null) return;

        blinkTimer += Time.deltaTime;

        bool visible = Mathf.FloorToInt(blinkTimer / blinkInterval) % 2 == 0;
        highScoreLabel.style.opacity = visible ? 1f : 0.2f;

        if (blinkTimer >= blinkDuration)
        {
            isHighScoreBlinking = false;
            highScoreLabel.style.opacity = 1f;
        }
    }
}