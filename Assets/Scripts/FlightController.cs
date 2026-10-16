using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class FlightController : MonoBehaviour
{
    public static bool PlayEntryAnimationOnNextLoad { get; set; }
    public float CurrentThrust01 => currentThrust01;

    [Header("Movement")]
    public InputAction moveForward;
    public InputAction lookPosition;
    public float thrustForce = 1f;
    public float maxSpeed = 5f;
    public float accel = 1f;
    public float thrustLinearDamping = 0f;
    public float coastLinearDamping = 0.12f;
    public GameObject boosterFlame;

    [Header("Scene Entry")]
    public float entrySlideDuration = 1.5f;
    public float entryStartOffset = 4f;
    public float maxThrustChargeTime = 1.5f;
    public float thrustReleaseDecayTime = 1.8f;
    public float thrustGaugeRiseSpeed = 8f;
    public float thrustGaugeFallSpeed = 4f;
    public float minBoosterFlameLength = 0.35f;
    public float maxBoosterFlameLength = 1.6f;
    public float boosterFlameLengthPositionOffset = 0.18f;
    public Vector2 boosterFlamePositionOffset = new Vector2(-0.08f, 0f);
    public Color lowThrustFlameColor = new Color(0.1f, 0.65f, 1f, 0.55f);
    public Color highThrustFlameColor = new Color(0.75f, 1f, 1f, 1f);
    public float boosterFlameFlickerStrength = 0.12f;
    public float boosterFlameFlickerSpeed = 35f;
    public bool autoCenterBoosterFlames = true;

    private Rigidbody2D rb;
    private Vector3 boosterFlameBaseScale = Vector3.one;
    private Vector3 boosterFlameBaseLocalPosition = Vector3.zero;
    private SpriteRenderer[] boosterFlameRenderers;
    private Transform[] boosterFlameRendererTransforms;
    private Vector3[] boosterFlameRendererBaseScales;
    private Vector3[] boosterFlameRendererBasePositions;
    private BoosterFlameTriangle[] boosterFlameTriangles;

    [Header("Lock On")]
    public float detectRange = 8f;
    public float unlockAngle = 45f;
    public float lockOnAngle = 30f;
    public float lockOnTime = 3f;
    public float lockOnDetectInterval = 0.1f;
    public int maxDetectedTargets = 32;
    public LayerMask targetLayer;
    public LayerMask lockBlockerLayer;
    public float lockBlockAngle = 60f;

    [Header("Missile")]
    public GameObject missilePrefab;
    public Transform missileSpawnPoint;
    public float missileSpawnForwardOffset = 0.6f;

    [Header("Fire FX")]
    public float fireDisplayDuration = 0.4f;

    [Header("Collision / Restart")]
    public GameObject explosionEffect;
    public GameObject borderParent;
    public UIDocument uiDocument;

    private Button restartButton;
    private Label missionFailedLabel;
    private VisualElement gameOverEntryPanel;
    private VisualElement rankingPanel;
    private Label rankingListLabel;
    private Label currentRankLabel;
    private Label currentScoreLabel;
    private TextField playerNameInput;
    private Button registerRankingButton;
    private VisualElement combatOperatorPanel;
    private Label combatOperatorMessage;
    private VisualElement thrustGaugeFill;
    private Label thrustGaugeValueLabel;
    private bool isDead = false;
    private float thrustHoldTimer = 0f;
    private float currentThrust01 = 0f;
    private float displayedThrust01 = 0f;
    private bool hasThrustInput = false;
    private Vector2 moveDirection = Vector2.up;
    private bool isSceneEntryPlaying = false;
    private AudioSource jetEngineAudioSource;
    private AudioClip jetEngineClip;
    private AudioSource combatOperatorAudioSource;
    private AudioClip fiveKillOperatorVoice;
    private AudioClip tenKillOperatorVoice;
    private AudioClip fifteenKillOperatorVoice;
    private Coroutine combatOperatorRoutine;

    private Collider2D[] lockOnHitBuffer;
    private float lockOnDetectTimer = 0f;

    private readonly Dictionary<EnemyController, float> lockTimers = new Dictionary<EnemyController, float>();
    private readonly Dictionary<EnemyController, float> fireDisplayTimers = new Dictionary<EnemyController, float>();
    private readonly List<EnemyController> currentLockTargets = new List<EnemyController>();
    private readonly List<EnemyController> removeList = new List<EnemyController>();
    private readonly List<EnemyController> tempKeys = new List<EnemyController>();
    private int lastDisplayedThrustPercent = -1;

    void Start()
    {
        DangerZoneBgm.EnsurePlaying();
        bool playSceneEntry = PlayEntryAnimationOnNextLoad;
        PlayEntryAnimationOnNextLoad = false;

        moveForward.Enable();
        lookPosition.Enable();
        rb = GetComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        InitializeJetEngineAudio();

        if (boosterFlame != null)
        {
            boosterFlameBaseScale = boosterFlame.transform.localScale;
            boosterFlameBaseLocalPosition = boosterFlame.transform.localPosition;
            boosterFlameRenderers = boosterFlame.GetComponentsInChildren<SpriteRenderer>();
            boosterFlameTriangles = boosterFlame.GetComponentsInChildren<BoosterFlameTriangle>();
            CacheBoosterFlameParts();
        }

        lockOnHitBuffer = new Collider2D[Mathf.Max(1, maxDetectedTargets)];
        MissileController.Prewarm(missilePrefab, 3);

        var root = uiDocument.rootVisualElement;

        gameOverEntryPanel = root.Q<VisualElement>("GameOverEntryPanel");
        if (gameOverEntryPanel != null)
            gameOverEntryPanel.style.display = DisplayStyle.None;

        restartButton = root.Q<Button>("RestartButton");
        if (restartButton != null)
        {
            restartButton.style.display = DisplayStyle.None;
            restartButton.clicked += ReloadScene;
        }

        missionFailedLabel = root.Q<Label>("Missonfailed");
        if (missionFailedLabel != null)
            missionFailedLabel.style.display = DisplayStyle.None;

        rankingPanel = root.Q<VisualElement>("RankingPanel");
        rankingListLabel = root.Q<Label>("RankingList");
        currentRankLabel = root.Q<Label>("CurrentRank");
        currentScoreLabel = root.Q<Label>("CurrentScore");
        playerNameInput = root.Q<TextField>("PlayerNameInput");
        registerRankingButton = root.Q<Button>("RegisterRankingButton");
        if (playerNameInput != null)
            playerNameInput.maxLength = 16;
        if (registerRankingButton != null)
            registerRankingButton.clicked += SubmitRanking;
        if (rankingPanel != null)
            rankingPanel.style.display = DisplayStyle.None;

        combatOperatorPanel = root.Q<VisualElement>("CombatOperatorPanel");
        combatOperatorMessage = root.Q<Label>("CombatOperatorMessage");
        InitializeCombatOperatorPanel();

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnKillCountChanged += HandleKillMilestone;

        thrustGaugeFill = root.Q<VisualElement>("ThrustGaugeFill");
        thrustGaugeValueLabel = root.Q<Label>("ThrustGaugeValue");
        UpdateThrustGauge(true);

        if (playSceneEntry)
            StartCoroutine(PlaySceneEntryAnimation());
    }

    void Update()
    {
        UpdateJetEngineAudio();
        if (isDead || isSceneEntryPlaying) return;

        UpdateThrustCharge();
        UpdateMoveInput();
        UpdateThrustGauge();

        UpdateFireDisplayTimers();
        UpdateLockOnDetection();
    }

    void OnDisable()
    {
        if (jetEngineAudioSource != null)
            jetEngineAudioSource.Stop();
    }

    void OnDestroy()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnKillCountChanged -= HandleKillMilestone;

        if (restartButton != null)
            restartButton.clicked -= ReloadScene;
        if (registerRankingButton != null)
            registerRankingButton.clicked -= SubmitRanking;
    }

    void InitializeCombatOperatorPanel()
    {
        if (combatOperatorPanel != null)
        {
            combatOperatorPanel.style.display = DisplayStyle.None;
            combatOperatorPanel.style.opacity = 0f;
        }

        fiveKillOperatorVoice = Resources.Load<AudioClip>("Audio/twocontacthostilehostile");
        tenKillOperatorVoice = Resources.Load<AudioClip>("Audio/TrafficTwoSevenZerothirtymile");
        fifteenKillOperatorVoice = Resources.Load<AudioClip>("Audio/OneTwoZerotwentymilehostile");

#if !UNITY_WEBGL
        if (fiveKillOperatorVoice != null)
            fiveKillOperatorVoice.LoadAudioData();
        if (tenKillOperatorVoice != null)
            tenKillOperatorVoice.LoadAudioData();
        if (fifteenKillOperatorVoice != null)
            fifteenKillOperatorVoice.LoadAudioData();
#endif

        combatOperatorAudioSource = gameObject.AddComponent<AudioSource>();
        combatOperatorAudioSource.playOnAwake = false;
        combatOperatorAudioSource.loop = false;
        combatOperatorAudioSource.spatialBlend = 0f;
        combatOperatorAudioSource.volume = 0.85f;
    }

    void HandleKillMilestone(int killCount)
    {
        switch (killCount)
        {
            case 5:
                PlayCombatOperatorMessage(
                    "VIPER 01, TWO CONTACT,\nHOSTILE, HOSTILE.",
                    fiveKillOperatorVoice);
                break;
            case 10:
                PlayCombatOperatorMessage(
                    "VIPER 01, TRAFFIC,\nTWO SEVEN ZERO, THIRTY MILE,\nDEBRIS.",
                    tenKillOperatorVoice);
                break;
            case 15:
                PlayCombatOperatorMessage(
                    "VIPER 01, ONE TWO ZERO,\nTWENTY MILE, HOSTILE.",
                    fifteenKillOperatorVoice);
                break;
        }
    }

    void PlayCombatOperatorMessage(string message, AudioClip voiceClip)
    {
        if (combatOperatorPanel == null || combatOperatorMessage == null)
            return;

        if (combatOperatorRoutine != null)
            StopCoroutine(combatOperatorRoutine);

        if (combatOperatorAudioSource != null)
            combatOperatorAudioSource.Stop();

        combatOperatorRoutine = StartCoroutine(
            ShowCombatOperatorMessage(message, voiceClip));
    }

    IEnumerator ShowCombatOperatorMessage(string message, AudioClip voiceClip)
    {
        combatOperatorMessage.text = message;
        combatOperatorPanel.style.display = DisplayStyle.Flex;

        const float fadeDuration = 0.15f;
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            combatOperatorPanel.style.opacity = Mathf.Clamp01(timer / fadeDuration);
            yield return null;
        }

        combatOperatorPanel.style.opacity = 1f;

        if (combatOperatorAudioSource != null && voiceClip != null)
        {
            combatOperatorAudioSource.clip = voiceClip;
            combatOperatorAudioSource.Play();
        }

        float visibleDuration = voiceClip != null
            ? Mathf.Max(3f, voiceClip.length + 0.4f)
            : 4f;
        yield return new WaitForSecondsRealtime(visibleDuration);

        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            combatOperatorPanel.style.opacity = 1f - Mathf.Clamp01(timer / fadeDuration);
            yield return null;
        }

        combatOperatorPanel.style.opacity = 0f;
        combatOperatorPanel.style.display = DisplayStyle.None;
        combatOperatorRoutine = null;
    }

    void InitializeJetEngineAudio()
    {
        jetEngineClip = Resources.Load<AudioClip>("Audio/jet-engine");
        if (jetEngineClip == null)
            return;

#if !UNITY_WEBGL
        jetEngineClip.LoadAudioData();
#endif

        jetEngineAudioSource = gameObject.AddComponent<AudioSource>();
        jetEngineAudioSource.clip = jetEngineClip;
        jetEngineAudioSource.playOnAwake = false;
        jetEngineAudioSource.loop = true;
        jetEngineAudioSource.spatialBlend = 0f;
        jetEngineAudioSource.volume = 0f;
        jetEngineAudioSource.pitch = 0.78f;
        jetEngineAudioSource.Play();
    }

    void UpdateJetEngineAudio()
    {
        if (jetEngineAudioSource == null)
            return;

        float thrust = isDead ? 0f : Mathf.Clamp01(currentThrust01);
        float volumeCurve = thrust * thrust;
        float targetVolume = Mathf.Lerp(0f, 0.35f, volumeCurve);
        float targetPitch = Mathf.Lerp(0.78f, 1.18f, thrust);
        jetEngineAudioSource.volume = Mathf.MoveTowards(
            jetEngineAudioSource.volume, targetVolume, Time.deltaTime * 0.8f);
        jetEngineAudioSource.pitch = Mathf.MoveTowards(
            jetEngineAudioSource.pitch, targetPitch, Time.deltaTime * 0.9f);

        if (!jetEngineAudioSource.isPlaying && thrust > 0.001f)
            jetEngineAudioSource.Play();
    }

    void LateUpdate()
    {
        if (isDead) return;

        BoosterFlame();
    }

    void FixedUpdate()
    {
        if (isDead || isSceneEntryPlaying) return;

        MovePlayer();
    }

    IEnumerator PlaySceneEntryAnimation()
    {
        isSceneEntryPlaying = true;

        CameraFollowController cameraFollow = Camera.main != null
            ? Camera.main.GetComponent<CameraFollowController>()
            : null;
        if (cameraFollow != null)
            cameraFollow.enabled = false;

        Vector3 destination = transform.position;
        Vector3 start = destination - Vector3.up * entryStartOffset;
        transform.position = start;
        transform.rotation = Quaternion.identity;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;
        currentThrust01 = 1f;
        thrustHoldTimer = maxThrustChargeTime;

        float elapsed = 0f;
        while (elapsed < entrySlideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, entrySlideDuration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(start, destination, eased);
            yield return null;
        }

        transform.position = destination;
        currentThrust01 = 0f;
        thrustHoldTimer = 0f;
        displayedThrust01 = 0f;
        rb.simulated = true;
        rb.linearVelocity = Vector2.zero;
        UpdateThrustGauge(true);

        isSceneEntryPlaying = false;
        if (cameraFollow != null)
            cameraFollow.enabled = true;
    }

    void UpdateMoveInput()
    {
        hasThrustInput = moveForward.IsPressed();
        if (!hasThrustInput) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(lookPosition.ReadValue<Vector2>());
        mousePos.z = transform.position.z;

        Vector2 direction = (mousePos - transform.position).normalized;
        if (direction != Vector2.zero)
            moveDirection = direction;
    }

    void MovePlayer()
    {
        rb.linearDamping = hasThrustInput ? thrustLinearDamping : coastLinearDamping;

        if (currentThrust01 <= 0.001f) return;

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg - 90f;
        rb.MoveRotation(angle);

        rb.AddForce(moveDirection * thrustForce * accel * currentThrust01);

        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    void UpdateThrustCharge()
    {
        if (moveForward.IsPressed())
        {
            thrustHoldTimer += Time.deltaTime;
            currentThrust01 = Mathf.Clamp01(thrustHoldTimer / Mathf.Max(0.01f, maxThrustChargeTime));
        }
        else
        {
            currentThrust01 = Mathf.MoveTowards(
                currentThrust01,
                0f,
                Time.deltaTime / Mathf.Max(0.01f, thrustReleaseDecayTime)
            );

            thrustHoldTimer = currentThrust01 * maxThrustChargeTime;
        }
    }

    void BoosterFlame()
    {
        if (boosterFlame == null) return;

        if (!boosterFlame.activeSelf)
            boosterFlame.SetActive(true);

        float flameLength = Mathf.Lerp(
            minBoosterFlameLength,
            maxBoosterFlameLength,
            currentThrust01
        );

        float flicker = 1f + Mathf.Sin(Time.time * boosterFlameFlickerSpeed) * boosterFlameFlickerStrength * currentThrust01;
        float flameWidth = Mathf.Lerp(0.85f, 1.15f, currentThrust01) * flicker;
        flameLength *= flicker;

        boosterFlame.transform.localScale = boosterFlameBaseScale;
        boosterFlame.transform.localPosition = boosterFlameBaseLocalPosition;

        if (boosterFlameTriangles != null && boosterFlameTriangles.Length > 0)
            UpdateBoosterFlameTriangles(flicker);
        else
            UpdateBoosterFlameShape(flameWidth, flameLength);

        UpdateBoosterFlameColor();
    }

    void UpdateBoosterFlameTriangles(float flicker)
    {
        foreach (BoosterFlameTriangle triangle in boosterFlameTriangles)
        {
            if (triangle != null)
            {
                triangle.lowThrustColor = lowThrustFlameColor;
                triangle.highThrustColor = highThrustFlameColor;
                triangle.SetThrust(currentThrust01, flicker);
            }
        }
    }

    void CacheBoosterFlameParts()
    {
        if (boosterFlameRenderers == null) return;

        boosterFlameRendererTransforms = new Transform[boosterFlameRenderers.Length];
        boosterFlameRendererBaseScales = new Vector3[boosterFlameRenderers.Length];
        boosterFlameRendererBasePositions = new Vector3[boosterFlameRenderers.Length];

        for (int i = 0; i < boosterFlameRenderers.Length; i++)
        {
            if (boosterFlameRenderers[i] == null) continue;

            Transform flameTransform = boosterFlameRenderers[i].transform;
            boosterFlameRendererTransforms[i] = flameTransform;
            boosterFlameRendererBaseScales[i] = flameTransform.localScale;
            boosterFlameRendererBasePositions[i] = flameTransform.localPosition;
        }
    }

    void UpdateBoosterFlameShape(float flameWidth, float flameLength)
    {
        if (boosterFlameRendererTransforms == null) return;

        for (int i = 0; i < boosterFlameRendererTransforms.Length; i++)
        {
            Transform flameTransform = boosterFlameRendererTransforms[i];
            if (flameTransform == null) continue;

            Vector3 baseScale = boosterFlameRendererBaseScales[i];
            flameTransform.localScale = new Vector3(
                baseScale.x * flameWidth,
                baseScale.y * flameLength,
                baseScale.z
            );

            flameTransform.localPosition = boosterFlameRendererBasePositions[i];
        }
    }

    void UpdateBoosterFlameColor()
    {
        if (boosterFlameRenderers == null) return;

        Color flameColor = Color.Lerp(
            lowThrustFlameColor,
            highThrustFlameColor,
            currentThrust01
        );

        float alphaFlicker = 1f + Mathf.Sin(Time.time * boosterFlameFlickerSpeed * 1.37f) * 0.08f * currentThrust01;
        flameColor.a = Mathf.Clamp01(flameColor.a * alphaFlicker);

        foreach (SpriteRenderer renderer in boosterFlameRenderers)
        {
            if (renderer != null)
                renderer.color = flameColor;
        }
    }

    void CenterBoosterFlames()
    {
        if (!autoCenterBoosterFlames) return;
        if (boosterFlameRenderers == null || boosterFlameRenderers.Length <= 1) return;

        float centerX = 0f;
        int count = 0;

        foreach (SpriteRenderer renderer in boosterFlameRenderers)
        {
            if (renderer == null) continue;
            if (renderer.transform.parent != boosterFlame.transform) continue;

            centerX += renderer.transform.localPosition.x;
            count++;
        }

        if (count <= 1) return;

        centerX /= count;
        if (Mathf.Abs(centerX) < 0.001f) return;

        foreach (SpriteRenderer renderer in boosterFlameRenderers)
        {
            if (renderer == null) continue;
            if (renderer.transform.parent != boosterFlame.transform) continue;

            Vector3 localPos = renderer.transform.localPosition;
            localPos.x -= centerX;
            renderer.transform.localPosition = localPos;
        }
    }

    void UpdateThrustGauge(bool instant = false)
    {
        if (thrustGaugeFill == null && thrustGaugeValueLabel == null) return;

        float targetThrust01 = currentThrust01;
        float speed = targetThrust01 > displayedThrust01 ? thrustGaugeRiseSpeed : thrustGaugeFallSpeed;

        displayedThrust01 = instant
            ? targetThrust01
            : Mathf.MoveTowards(displayedThrust01, targetThrust01, speed * Time.deltaTime);

        int thrustPercent = Mathf.RoundToInt(displayedThrust01 * 100f);

        if (!instant && thrustPercent == lastDisplayedThrustPercent)
            return;

        lastDisplayedThrustPercent = thrustPercent;

        if (thrustGaugeFill != null)
            thrustGaugeFill.style.height = Length.Percent(thrustPercent);

        if (thrustGaugeValueLabel != null)
            thrustGaugeValueLabel.text = $"{thrustPercent}%";
    }

    void UpdateLockOnDetection()
    {
        if (LockOnUIManager.Instance == null) return;

        lockOnDetectTimer += Time.deltaTime;

        if (lockOnDetectTimer < lockOnDetectInterval)
            return;

        float elapsed = lockOnDetectTimer;
        lockOnDetectTimer = 0f;

        HandleLockOn(elapsed);
    }

    void HandleLockOn(float elapsed)
    {
        LockOnUIManager.Instance.HideAll();

        currentLockTargets.Clear();

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            detectRange,
            lockOnHitBuffer,
            targetLayer
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = lockOnHitBuffer[i];
            if (hit == null) continue;

            EnemyController enemy = hit.GetComponentInParent<EnemyController>();

            if (enemy == null || enemy.IsDestroyed)
                continue;

            if (fireDisplayTimers.ContainsKey(enemy))
                continue;

            Vector2 dirToEnemy = enemy.transform.position - transform.position;
            float angle = Vector2.Angle(transform.up, dirToEnemy);

            if (IsLockBlocked(enemy, dirToEnemy, angle))
            {
                CancelLockOn(enemy);
                continue;
            }

            if (angle <= unlockAngle)
                LockOnUIManager.Instance.ShowIcon(enemy, LockState.Unlock);

            if (angle <= lockOnAngle)
            {
                LockOnUIManager.Instance.ShowIcon(enemy, LockState.LockOn);
                currentLockTargets.Add(enemy);

                if (!lockTimers.ContainsKey(enemy))
                    lockTimers.Add(enemy, 0f);

                lockTimers[enemy] += elapsed;

                if (lockTimers[enemy] >= lockOnTime)
                {
                    FireMissile(enemy);

                    fireDisplayTimers[enemy] = fireDisplayDuration;
                    LockOnUIManager.Instance.ShowIcon(enemy, LockState.Fire);

                    lockTimers[enemy] = 0f;
                }
            }
        }

        CleanUpLockTimers();
    }

    bool IsLockBlocked(EnemyController enemy, Vector2 dirToEnemy, float angle)
    {
        if (enemy == null) return false;
        if (lockBlockerLayer.value == 0) return false;
        if (angle > lockBlockAngle) return false;

        float distance = dirToEnemy.magnitude;
        if (distance <= 0f) return false;

        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            dirToEnemy.normalized,
            distance,
            lockBlockerLayer
        );

        return hit.collider != null;
    }

    void CancelLockOn(EnemyController enemy)
    {
        if (enemy == null) return;

        lockTimers.Remove(enemy);
        fireDisplayTimers.Remove(enemy);

        if (LockOnUIManager.Instance != null)
            LockOnUIManager.Instance.HideIcon(enemy);
    }

    void UpdateFireDisplayTimers()
    {
        if (fireDisplayTimers.Count == 0) return;
        if (LockOnUIManager.Instance == null) return;

        tempKeys.Clear();
        tempKeys.AddRange(fireDisplayTimers.Keys);

        removeList.Clear();

        foreach (EnemyController enemy in tempKeys)
        {
            if (enemy == null || enemy.IsDestroyed)
            {
                removeList.Add(enemy);
                continue;
            }

            float remaining = fireDisplayTimers[enemy] - Time.deltaTime;

            if (remaining <= 0f)
            {
                removeList.Add(enemy);
            }
            else
            {
                fireDisplayTimers[enemy] = remaining;
                LockOnUIManager.Instance.ShowIcon(enemy, LockState.Fire);
            }
        }

        foreach (EnemyController enemy in removeList)
            fireDisplayTimers.Remove(enemy);
    }

    void CleanUpLockTimers()
    {
        removeList.Clear();

        foreach (EnemyController enemy in lockTimers.Keys)
        {
            if (enemy == null || enemy.IsDestroyed || !currentLockTargets.Contains(enemy))
                removeList.Add(enemy);
        }

        foreach (EnemyController enemy in removeList)
            lockTimers.Remove(enemy);
    }

    void FireMissile(EnemyController target)
    {
        if (missilePrefab == null || target == null)
            return;

        Vector3 spawnPos = missileSpawnPoint != null
            ? missileSpawnPoint.position
            : transform.position + transform.up * missileSpawnForwardOffset;

        MissileController missile = MissileController.Spawn(
            missilePrefab,
            spawnPos,
            transform.rotation
        );

        if (missile != null)
        {
            missile.SetOwner(MissileOwner.Player, gameObject);
            missile.SetTarget(target);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        Vector2 dir;

        if (collision.gameObject.CompareTag("Border"))
            dir = collision.GetContact(0).normal;
        else
            dir = rb.linearVelocity.normalized;

        Die(collision.GetContact(0).point, dir);
    }

    public void Die(Vector2 hitPosition, Vector2 hitDirection)
    {
        if (isDead) return;
        isDead = true;

        if (LockOnUIManager.Instance != null)
            LockOnUIManager.Instance.StopAllLockSounds();

        SpawnExplosion(hitPosition, hitDirection);
        HidePlayer();

        if (gameOverEntryPanel != null)
            gameOverEntryPanel.style.display = DisplayStyle.Flex;

        if (missionFailedLabel != null)
            missionFailedLabel.style.display = DisplayStyle.Flex;

        if (restartButton != null)
            restartButton.style.display = DisplayStyle.Flex;

        ShowGameOverRanking();

        if (borderParent != null)
            borderParent.SetActive(false);
    }

    void ShowGameOverRanking()
    {
        if (ScoreManager.Instance == null)
            return;

        if (playerNameInput != null)
        {
            playerNameInput.SetValueWithoutNotify(ScoreManager.Instance.LastPlayerName);
            playerNameInput.SetEnabled(true);
        }

        if (registerRankingButton != null)
            registerRankingButton.SetEnabled(true);

        if (currentScoreLabel != null)
            currentScoreLabel.text = $"CURRENT SCORE : {ScoreManager.Instance.KillCount} KILLS";

        RefreshRankingDisplay();

        if (currentRankLabel != null)
            currentRankLabel.text = "ENTER PILOT NAME";

        if (rankingPanel != null)
            rankingPanel.style.display = DisplayStyle.Flex;
    }

    void SubmitRanking()
    {
        if (ScoreManager.Instance == null)
            return;

        string playerName = playerNameInput != null ? playerNameInput.value : "PILOT";
        int currentRank = ScoreManager.Instance.SubmitCurrentRun(playerName);
        RefreshRankingDisplay();

        if (currentRankLabel != null)
        {
            currentRankLabel.text = currentRank > 0
                ? $"YOUR RANK : {GetPilotRankTitle(currentRank)}"
                : "YOUR RANK : OUTSIDE TOP 10";
        }

        if (playerNameInput != null)
            playerNameInput.SetEnabled(false);
        if (registerRankingButton != null)
            registerRankingButton.SetEnabled(false);
    }

    void RefreshRankingDisplay()
    {
        if (ScoreManager.Instance == null)
            return;

        IReadOnlyList<ScoreManager.RankingEntry> rankings = ScoreManager.Instance.Rankings;

        if (rankingListLabel != null)
        {
            StringBuilder rankingText = new StringBuilder();
            for (int i = 0; i < rankings.Count; i++)
            {
                if (i > 0)
                    rankingText.AppendLine();

                rankingText.Append(GetPilotRankTitle(i + 1).PadRight(10)).Append(" | ")
                    .Append(rankings[i].playerName.PadRight(16)).Append(" | ")
                    .Append(rankings[i].score).Append(" KILLS");
            }

            rankingListLabel.text = rankingText.Length > 0 ? rankingText.ToString() : "NO RECORDS";
        }

    }

    string GetPilotRankTitle(int rank)
    {
        if (rank == 1) return "TOPGUN";
        if (rank == 2) return "2nd Pilot";
        if (rank == 3) return "3rd Pilot";
        return $"{rank}th Pilot";
    }

    void HidePlayer()
    {
        if (boosterFlame != null)
        {
            boosterFlame.transform.localScale = boosterFlameBaseScale;
            boosterFlame.transform.localPosition = boosterFlameBaseLocalPosition;
            boosterFlame.SetActive(false);
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in colliders)
            col.enabled = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
            renderer.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }
    }

    void SpawnExplosion(Vector2 point, Vector2 dir)
    {
        if (explosionEffect == null) return;

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

    void ReloadScene()
    {
        if (isDead && ScoreManager.Instance != null && !ScoreManager.Instance.HasSubmittedRun)
            SubmitRanking();

        SceneManager.LoadScene("SpriteFlight");
    }
}
