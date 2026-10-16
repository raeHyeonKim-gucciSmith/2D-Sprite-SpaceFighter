using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public sealed class LaunchSequenceController : MonoBehaviour
{
    private enum SequencePhase
    {
        RadioCheck,
        LoudClear,
        RadarDemo,
        WeaponCheck,
        MissileWarning,
        FadeToRunway,
        Clearance,
        AwaitLaunch,
        Launching
    }

    [Header("Sequence Assets")]
    [SerializeField] private Sprite fighterSprite;
    [SerializeField] private Sprite operatorSprite;
    [SerializeField] private Sprite carrierRunwaySprite;
    [SerializeField] private Sprite unlockSprite;
    [SerializeField] private Sprite lockOnSprite;
    [SerializeField] private GameObject fighterPrefab;
    [SerializeField] private VisualTreeAsset fighterUIAsset;
    [SerializeField] private PanelSettings fighterUIPanelSettings;
    [SerializeField] private string battleSceneName = "SpriteFlight";

    [Header("Sequence Timing")]
    [SerializeField] private float radioCheckDuration = 4f;
    [SerializeField] private float radioFadeStartTime = 1.8f;
    [SerializeField] private float radioFadeDuration = 2.2f;
    [SerializeField] private float loudClearDuration = 3.5f;
    [SerializeField] private float radarDemoDuration = 12f;
    [SerializeField] private float weaponCheckDelay = 3f;
    [SerializeField] private float runwayFadeDuration = 2.5f;
    [SerializeField] private float clearanceDuration = 7f;
    [SerializeField] private float typewriterCharactersPerSecond = 14f;

    [Header("Tutorial Panel Layout - normalized screen coordinates")]
    [SerializeField] private Vector2 operatorPanelPosition = new Vector2(0.02f, 0.02f);
    [SerializeField] private Vector2 operatorPanelSize = new Vector2(0.58f, 0.43f);
    [SerializeField] private float operatorPortraitWidthRatio = 0.56f;
    [SerializeField, Range(1f, 4f)] private float operatorPortraitZoom = 1.8f;
    [SerializeField] private Vector2 operatorPortraitFocus = new Vector2(0.5f, 0.58f);
    [SerializeField] private Vector2 radarPanelPosition = new Vector2(0.24f, 0.45f);
    [SerializeField] private Vector2 radarPanelSize = new Vector2(0.52f, 0.52f);

    [Header("Radar Lock-On Tutorial Layout - world coordinates")]
    [Tooltip("Center of the detection/lock-on cones. Other radar tutorial positions are relative to this point.")]
    [SerializeField] private Vector3 radarTutorialOrigin = new Vector3(0f, -3.45f, 0f);
    [Tooltip("Vertical distance from the tutorial origin to the virtual enemy.")]
    [SerializeField, Min(0f)] private float radarEnemyHeight = 3f;
    [Tooltip("Maximum horizontal distance travelled by the virtual enemy during the radar demonstration.")]
    [SerializeField, Min(0f)] private float radarEnemyTravelWidth = 3.35f;
    [Tooltip("Horizontal offset used while the virtual enemy is held inside the lock-on cone.")]
    [SerializeField] private float radarLockedEnemyOffsetX = 0.85f;
    [Tooltip("World-space radius of both radar cones.")]
    [SerializeField, Min(0.1f)] private float radarConeRadius = 3.75f;
    [Tooltip("World-space size of the virtual enemy marker.")]
    [SerializeField] private Vector2 radarEnemySize = new Vector2(0.34f, 0.34f);
    [Tooltip("World-space height of the aim/lock-on icon.")]
    [SerializeField, Min(0.01f)] private float radarAimIconHeight = 0.72f;

    [Header("Scene Layout Handles - move these children in the Hierarchy")]
    [SerializeField] private Transform sceneLayoutRoot;
    [SerializeField] private Transform radarOriginHandle;
    [SerializeField] private Transform radarEnemyPathLeftHandle;
    [SerializeField] private Transform radarEnemyPathRightHandle;
    [SerializeField] private Transform radarLockedEnemyHandle;
    [SerializeField] private Transform carrierPositionHandle;
    [SerializeField] private Transform fighterStartHandle;
    [SerializeField] private LaunchSequenceTextSettings textSettings;
    [SerializeField] private Transform missileWarningTextHandle;

    public Vector2 OperatorPanelPosition { get => operatorPanelPosition; set => operatorPanelPosition = value; }
    public Vector2 OperatorPanelSize { get => operatorPanelSize; set => operatorPanelSize = value; }
    public float OperatorPortraitZoom { get => operatorPortraitZoom; set => operatorPortraitZoom = value; }
    public Vector2 OperatorPortraitFocus { get => operatorPortraitFocus; set => operatorPortraitFocus = value; }
    public Vector2 RadarPanelPosition { get => radarPanelPosition; set => radarPanelPosition = value; }
    public Vector2 RadarPanelSize { get => radarPanelSize; set => radarPanelSize = value; }
    public Sprite OperatorSprite => operatorSprite;

    [Header("Flight - same values as SpriteFlight")]
    [SerializeField] private float thrustForce = 1f;
    [SerializeField] private float maxSpeed = 4f;
    [SerializeField] private float acceleration = 0.9f;
    [SerializeField] private float thrustLinearDamping = 0f;
    [SerializeField] private float coastLinearDamping = 0.1f;
    [SerializeField] private float maxThrustChargeTime = 1.5f;
    [SerializeField] private float thrustReleaseDecayTime = 3f;
    [SerializeField] private float carrierDepartureDistance = 3.5f;
    [SerializeField] private float maxThrustFlightDuration = 5f;
    [SerializeField] private float launchCameraSmoothTime = 0.18f;

    private Camera mainCamera;
    private Transform flightVisualRoot;
    private Transform tutorialRoot;
    private Transform fighter;
    private Transform flame;
    private Transform tutorialEnemy;
    private Transform aimIcon;
    private Rigidbody2D fighterBody;
    private GameObject launchBooster;
    private BoosterFlameTriangle[] boosterTriangles;
    private VisualElement thrustGauge;
    private VisualElement thrustGaugeFill;
    private Label thrustGaugeValueLabel;
    private SpriteRenderer aimIconRenderer;
    private SpriteRenderer tutorialEnemyRenderer;
    private Renderer[] launchFighterRenderers;
    private Material aimIconDefaultMaterial;
    private Material redLockOnMaterial;
    private AudioSource operatorVoiceSource;
    private AudioSource launchEngineSource;
    private AudioSource tutorialLockAudioSource;
    private AudioClip radioCheckVoice;
    private AudioClip loudAndClearVoice;
    private AudioClip radarCheckVoice;
    private AudioClip radarSystemGreenVoice;
    private AudioClip weaponCheckVoice;
    private AudioClip systemsAllGreenVoice;
    private AudioClip clearedForLaunchVoice;
    private AudioClip jetEngineStartupClip;
    private AudioClip tutorialUnlockClip;
    private AudioClip tutorialLockOnClip;
    private Coroutine tutorialSequenceRoutine;
    private Coroutine operatorVoiceRoutine;
    private float aimIconBaseScale;
    private static readonly Vector3 DefaultCarrierPosition = new Vector3(0f, -3.2f, 0f);
    private static readonly Vector3 DefaultFighterStartPosition = new Vector3(0f, -3.45f, 0f);

    private SequencePhase phase;
    private float phaseElapsed;
    private float fade01;
    private float thrustHoldTimer;
    private float currentThrust01;
    private bool hasThrustInput;
    private Vector2 moveDirection = Vector2.up;
    private bool loading;
    private float typedCharacterCount;
    private float displayedThrust01;
    private float thrustGaugeRiseSpeed = 2f;
    private float thrustGaugeFallSpeed = 1f;
    private bool hasLeftCarrier;
    private bool maxLaunchThrustLocked;
    private float maxThrustFlightTimer;
    private Vector3 launchCameraOffset;
    private Vector3 launchCameraVelocity;
    private bool layoutEditMode;
    private int activeLayoutControl;
    private Vector2 layoutDragStartMouse;
    private Vector2 layoutDragStartPosition;
    private Vector2 layoutDragStartSize;
    private float savedTimeScale = 1f;
    private string lastOperatorMessage;
    private bool tutorialSkipped;

    private static Sprite whiteSprite;
    private static Font galmuri11Font;
    private static Texture2D skipArrowTexture;
    private const int FighterTutorialSortingOrder = 40;
    private const float RadarSweepAngularSpeed = 1.35f;
    private const float LaunchCameraFollowDurationAfterCarrier = 4f;
    private const float MissileWarningDelayAfterLock = 1.7f;
    private const float MissileWarningBlinkInterval = 0.7f;
    private const float MissileWarningVisibleDuration = 0.36f;
    private const int MissileWarningBlinkCount = 4;
    private const float MissileWarningSequenceDuration =
        (MissileWarningBlinkCount - 1) * MissileWarningBlinkInterval + MissileWarningVisibleDuration;
    private const float TvTurnOnDuration = 0.55f;
    private const float TvTurnOffDuration = 0.42f;

    private Vector3 RadarOriginPosition => radarOriginHandle != null
        ? radarOriginHandle.position
        : radarTutorialOrigin;
    private Vector3 RadarEnemyPathLeftPosition => radarEnemyPathLeftHandle != null
        ? radarEnemyPathLeftHandle.position
        : radarTutorialOrigin + new Vector3(-radarEnemyTravelWidth, radarEnemyHeight, 0f);
    private Vector3 RadarEnemyPathRightPosition => radarEnemyPathRightHandle != null
        ? radarEnemyPathRightHandle.position
        : radarTutorialOrigin + new Vector3(radarEnemyTravelWidth, radarEnemyHeight, 0f);
    private Vector3 RadarLockedEnemyPosition => radarLockedEnemyHandle != null
        ? radarLockedEnemyHandle.position
        : radarTutorialOrigin + new Vector3(radarLockedEnemyOffsetX, radarEnemyHeight, 0f);
    private Vector3 CarrierPosition => carrierPositionHandle != null
        ? carrierPositionHandle.position
        : DefaultCarrierPosition;
    private Vector3 FighterStartPosition => fighterStartHandle != null
        ? fighterStartHandle.position
        : DefaultFighterStartPosition;

    public bool EnsureLayoutHierarchy()
    {
        bool changed = false;
        sceneLayoutRoot = FindOrCreateLayoutPoint(transform, sceneLayoutRoot,
            "Launch Scene Layout (MOVE THESE)", Vector3.zero, ref changed);
        radarOriginHandle = FindOrCreateLayoutPoint(sceneLayoutRoot, radarOriginHandle,
            "Radar - Cone Origin", radarTutorialOrigin, ref changed);
        radarEnemyPathLeftHandle = FindOrCreateLayoutPoint(sceneLayoutRoot, radarEnemyPathLeftHandle,
            "Radar - Enemy Path LEFT", radarTutorialOrigin + new Vector3(-radarEnemyTravelWidth, radarEnemyHeight, 0f), ref changed);
        radarEnemyPathRightHandle = FindOrCreateLayoutPoint(sceneLayoutRoot, radarEnemyPathRightHandle,
            "Radar - Enemy Path RIGHT", radarTutorialOrigin + new Vector3(radarEnemyTravelWidth, radarEnemyHeight, 0f), ref changed);
        radarLockedEnemyHandle = FindOrCreateLayoutPoint(sceneLayoutRoot, radarLockedEnemyHandle,
            "Radar - Locked Enemy Position", radarTutorialOrigin + new Vector3(radarLockedEnemyOffsetX, radarEnemyHeight, 0f), ref changed);
        carrierPositionHandle = FindOrCreateLayoutPoint(sceneLayoutRoot, carrierPositionHandle,
            "Carrier - Position", DefaultCarrierPosition, ref changed);
        fighterStartHandle = FindOrCreateLayoutPoint(sceneLayoutRoot, fighterStartHandle,
            "Fighter - Start Position", DefaultFighterStartPosition, ref changed);
        Transform textObject = FindOrCreateLayoutPoint(sceneLayoutRoot,
            textSettings != null ? textSettings.transform : null,
            "Launch Text (EDIT HERE)", Vector3.zero, ref changed);
        if (textSettings == null)
        {
            textSettings = textObject.GetComponent<LaunchSequenceTextSettings>();
            if (textSettings == null)
                textSettings = textObject.gameObject.AddComponent<LaunchSequenceTextSettings>();
            changed = true;
        }
        missileWarningTextHandle = FindOrCreateLayoutPoint(textObject, missileWarningTextHandle,
            "MISSILE LAUNCH - Position", new Vector3(0f, 0.7f, 0f), ref changed);
        return changed;
    }

    public LaunchSequenceTextSettings TextSettings => textSettings;
    public Transform MissileWarningTextHandle => missileWarningTextHandle;

    private static Transform FindOrCreateLayoutPoint(Transform parent, Transform current,
        string objectName, Vector3 defaultWorldPosition, ref bool changed)
    {
        if (current != null)
            return current;

        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            changed = true;
            return existing;
        }

        GameObject point = new GameObject(objectName);
        point.transform.SetParent(parent);
        point.transform.position = defaultWorldPosition;
        changed = true;
        return point.transform;
    }

    private void OnDrawGizmos()
    {
        Vector3 origin = RadarOriginPosition;
        float detectionHalfWidth = Mathf.Sin(45f * Mathf.Deg2Rad) * radarConeRadius;
        float detectionHeight = Mathf.Cos(45f * Mathf.Deg2Rad) * radarConeRadius;
        float lockHalfWidth = Mathf.Sin(30f * Mathf.Deg2Rad) * radarConeRadius;
        float lockHeight = Mathf.Cos(30f * Mathf.Deg2Rad) * radarConeRadius;

        Gizmos.color = new Color(1f, 1f, 1f, 0.65f);
        Gizmos.DrawLine(origin, origin + new Vector3(-detectionHalfWidth, detectionHeight, 0f));
        Gizmos.DrawLine(origin, origin + new Vector3(detectionHalfWidth, detectionHeight, 0f));
        Gizmos.color = new Color(0.25f, 1f, 0.45f, 0.75f);
        Gizmos.DrawLine(origin, origin + new Vector3(-lockHalfWidth, lockHeight, 0f));
        Gizmos.DrawLine(origin, origin + new Vector3(lockHalfWidth, lockHeight, 0f));

        Gizmos.color = new Color(0.25f, 1f, 0.45f, 0.9f);
        Gizmos.DrawWireSphere(origin, 0.16f);
        Gizmos.DrawLine(origin, RadarEnemyPathLeftPosition);
        Gizmos.DrawLine(origin, RadarEnemyPathRightPosition);
        Gizmos.DrawLine(RadarEnemyPathLeftPosition, RadarEnemyPathRightPosition);
        Gizmos.DrawWireSphere(RadarEnemyPathLeftPosition, 0.16f);
        Gizmos.DrawWireSphere(RadarEnemyPathRightPosition, 0.16f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(RadarLockedEnemyPosition, 0.18f);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(CarrierPosition, new Vector3(1.2f, 0.2f, 0f));
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(FighterStartPosition, 0.18f);
    }

    private void Awake()
    {
        DangerZoneBgm.EnsurePlaying();
        EnsureLayoutHierarchy();
        mainCamera = Camera.main;
        mainCamera.backgroundColor = Color.black;
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 5f;
        InitializeLaunchAudio();
        BuildScene();
        tutorialSequenceRoutine = StartCoroutine(PlayTutorialSequence());
    }

    private IEnumerator PlayTutorialSequence()
    {
        SetPhase(SequencePhase.RadioCheck);
        flightVisualRoot.gameObject.SetActive(true);
        fighter.gameObject.SetActive(true);
        yield return WaitForSecondsTracked(radioCheckDuration);

        SetPhase(SequencePhase.LoudClear);
        yield return WaitForSecondsTracked(loudClearDuration);

        SetPhase(SequencePhase.RadarDemo);
        tutorialRoot.gameObject.SetActive(true);
        yield return WaitForSecondsTracked(radarDemoDuration);

        SetPhase(SequencePhase.WeaponCheck);
        yield return WaitForSecondsTracked(MissileWarningDelayAfterLock);

        SetPhase(SequencePhase.MissileWarning);
        yield return WaitForSecondsTracked(MissileWarningSequenceDuration);

        StopTutorialLockAudio();
        tutorialRoot.gameObject.SetActive(false);
        SetPhase(SequencePhase.Clearance);
        yield return WaitForSecondsTracked(clearanceDuration);

        SetPhase(SequencePhase.AwaitLaunch);
        tutorialSequenceRoutine = null;
    }

    private IEnumerator ContinueFromSystemsAllGreen()
    {
        yield return WaitForSecondsTracked(clearanceDuration);
        SetPhase(SequencePhase.AwaitLaunch);
        tutorialSequenceRoutine = null;
    }

    private void SkipToSystemsAllGreen()
    {
        if (tutorialSkipped || !ShouldShowSkipButton())
            return;

        tutorialSkipped = true;
        if (tutorialSequenceRoutine != null)
        {
            StopCoroutine(tutorialSequenceRoutine);
            tutorialSequenceRoutine = null;
        }

        if (operatorVoiceRoutine != null)
        {
            StopCoroutine(operatorVoiceRoutine);
            operatorVoiceRoutine = null;
        }
        if (operatorVoiceSource != null)
            operatorVoiceSource.Stop();

        StopTutorialLockAudio();
        if (tutorialRoot != null)
            tutorialRoot.gameObject.SetActive(false);

        lastOperatorMessage = null;
        SetPhase(SequencePhase.Clearance);
        tutorialSequenceRoutine = StartCoroutine(ContinueFromSystemsAllGreen());
    }

    private IEnumerator WaitForSecondsTracked(float duration)
    {
        phaseElapsed = 0f;
        while (phaseElapsed < duration)
        {
            phaseElapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator FadeInRunway()
    {
        phaseElapsed = 0f;
        fade01 = 0f;
        while (phaseElapsed < runwayFadeDuration)
        {
            phaseElapsed += Time.deltaTime;
            fade01 = Mathf.Clamp01(phaseElapsed / Mathf.Max(0.01f, runwayFadeDuration));
            yield return null;
        }
        fade01 = 1f;
    }

    private void SetPhase(SequencePhase nextPhase)
    {
        string previousMessage = GetOperatorMessage(phase);
        string nextMessage = GetOperatorMessage(nextPhase);
        if (previousMessage != nextMessage)
            typedCharacterCount = 0f;

        phase = nextPhase;
        phaseElapsed = 0f;

        if (nextPhase == SequencePhase.AwaitLaunch)
            SetThrustUIVisible(true);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            ToggleLayoutEditMode();
#endif

        if (ShouldShowOperatorPanel())
        {
            string message = GetOperatorMessage(phase);
            if (message != lastOperatorMessage)
            {
                typedCharacterCount = 0f;
                lastOperatorMessage = message;
                PlayOperatorVoiceForCurrentState();
            }
            typedCharacterCount = Mathf.Min(
                message.Length,
                typedCharacterCount + typewriterCharactersPerSecond * Time.deltaTime);
        }

        if (phase == SequencePhase.RadarDemo)
            UpdateRadarDemonstration(false);
        else if (phase == SequencePhase.WeaponCheck || phase == SequencePhase.MissileWarning)
            UpdateRadarDemonstration(true);

        if (phase == SequencePhase.AwaitLaunch && IsPointerHeld())
        {
            SetPhase(SequencePhase.Launching);
            PlayLaunchEngineStartup();
            if (launchBooster != null)
                launchBooster.SetActive(true);
        }

        if (phase != SequencePhase.Launching || loading)
            return;

        if (!hasLeftCarrier)
        {
            UpdateFlightInput();
            if (Vector2.Distance(fighter.position, FighterStartPosition) >= carrierDepartureDistance)
            {
                hasLeftCarrier = true;
                maxLaunchThrustLocked = true;
                currentThrust01 = 1f;
                thrustHoldTimer = maxThrustChargeTime;
                hasThrustInput = true;
                maxThrustFlightTimer = 0f;
            }
        }
        else
        {
            currentThrust01 = 1f;
            hasThrustInput = true;
            maxThrustFlightTimer += Time.deltaTime;
        }

        if (!hasLeftCarrier || maxThrustFlightTimer < LaunchCameraFollowDurationAfterCarrier)
            UpdateLaunchCameraFollow();
        UpdateLaunchEngineAudio();
        UpdateBoosterEffect();
        UpdateThrustGaugeUI();

        if (hasLeftCarrier
            && maxThrustFlightTimer >= LaunchCameraFollowDurationAfterCarrier
            && HasFighterExitedCameraView())
            StartCoroutine(LoadBattleScene());
    }

    private void OnDisable()
    {
        if (layoutEditMode)
            Time.timeScale = savedTimeScale;

        if (operatorVoiceSource != null)
            operatorVoiceSource.Stop();
        if (launchEngineSource != null)
            launchEngineSource.Stop();
        StopTutorialLockAudio();
    }

    private void InitializeLaunchAudio()
    {
        operatorVoiceSource = gameObject.AddComponent<AudioSource>();
        operatorVoiceSource.playOnAwake = false;
        operatorVoiceSource.loop = false;
        operatorVoiceSource.spatialBlend = 0f;

        launchEngineSource = gameObject.AddComponent<AudioSource>();
        launchEngineSource.playOnAwake = false;
        launchEngineSource.loop = false;
        launchEngineSource.spatialBlend = 0f;

        GameObject tutorialLockSoundObject = new GameObject("Radar Tutorial Lock Sound");
        tutorialLockSoundObject.transform.SetParent(transform, false);
        tutorialLockAudioSource = tutorialLockSoundObject.AddComponent<AudioSource>();
        tutorialLockAudioSource.playOnAwake = false;
        tutorialLockAudioSource.loop = true;
        tutorialLockAudioSource.volume = 0.7f;
        tutorialLockAudioSource.spatialBlend = 1f;
        tutorialLockAudioSource.minDistance = 1f;
        tutorialLockAudioSource.maxDistance = 12f;

        radioCheckVoice = Resources.Load<AudioClip>("Audio/Radiocheck");
        loudAndClearVoice = Resources.Load<AudioClip>("Audio/loudandclear");
        radarCheckVoice = Resources.Load<AudioClip>("Audio/Radarcheck");
        radarSystemGreenVoice = Resources.Load<AudioClip>("Audio/radarsystemgreen");
        weaponCheckVoice = Resources.Load<AudioClip>("Audio/weaponcheck");
        systemsAllGreenVoice = Resources.Load<AudioClip>("Audio/systemsallgreen");
        clearedForLaunchVoice = Resources.Load<AudioClip>("Audio/ClearedforlaunchGoodluck");
        jetEngineStartupClip = Resources.Load<AudioClip>("Audio/new-jet-engine-startup");
        tutorialUnlockClip = Resources.Load<AudioClip>("Audio/digital_alarm_beep");
        tutorialLockOnClip = Resources.Load<AudioClip>("Audio/continuous_beep_5s");
    }

    private void PlayOperatorVoiceForCurrentState()
    {
        if (operatorVoiceSource == null)
            return;

        if (operatorVoiceRoutine != null)
        {
            StopCoroutine(operatorVoiceRoutine);
            operatorVoiceRoutine = null;
        }
        operatorVoiceSource.Stop();

        if (phase == SequencePhase.Clearance)
        {
            operatorVoiceRoutine = StartCoroutine(PlayClearanceVoiceSequence());
            return;
        }

        AudioClip clip = null;
        switch (phase)
        {
            case SequencePhase.RadioCheck:
                clip = radioCheckVoice;
                break;
            case SequencePhase.LoudClear:
                clip = loudAndClearVoice;
                break;
            case SequencePhase.RadarDemo:
                if (phaseElapsed >= GetFinalRadarSweepStartTime())
                    clip = weaponCheckVoice;
                else if (phaseElapsed >= GetSecondRadarSweepStartTime())
                    clip = radarSystemGreenVoice;
                else
                    clip = radarCheckVoice;
                break;
            case SequencePhase.WeaponCheck:
            case SequencePhase.MissileWarning:
                clip = weaponCheckVoice;
                break;
        }

        if (clip != null)
            operatorVoiceSource.PlayOneShot(clip);
    }

    private IEnumerator PlayClearanceVoiceSequence()
    {
        if (systemsAllGreenVoice != null)
        {
            operatorVoiceSource.clip = systemsAllGreenVoice;
            operatorVoiceSource.Play();
            yield return new WaitForSeconds(systemsAllGreenVoice.length + 0.08f);
        }

        if (clearedForLaunchVoice != null)
        {
            operatorVoiceSource.clip = clearedForLaunchVoice;
            operatorVoiceSource.Play();
        }
        operatorVoiceRoutine = null;
    }

    private void PlayLaunchEngineStartup()
    {
        if (launchEngineSource == null || jetEngineStartupClip == null)
            return;

        launchEngineSource.clip = jetEngineStartupClip;
        launchEngineSource.volume = 0.12f;
        launchEngineSource.pitch = 0.85f;
        launchEngineSource.Play();
    }

    private void UpdateLaunchEngineAudio()
    {
        if (launchEngineSource == null || !launchEngineSource.isPlaying)
            return;

        float volumeCurve = currentThrust01 * currentThrust01;
        launchEngineSource.volume = Mathf.Lerp(0.12f, 0.4f, volumeCurve);
        launchEngineSource.pitch = Mathf.Lerp(0.85f, 1.1f, currentThrust01);
    }

    private void OnDestroy()
    {
        if (redLockOnMaterial != null)
            Destroy(redLockOnMaterial);
    }

    private void ToggleLayoutEditMode()
    {
        layoutEditMode = !layoutEditMode;
        activeLayoutControl = 0;
        if (layoutEditMode)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = savedTimeScale;
        }
    }

    private void FixedUpdate()
    {
        if (phase != SequencePhase.Launching || fighterBody == null)
            return;

        fighterBody.linearDamping = hasThrustInput ? thrustLinearDamping : coastLinearDamping;
        if (currentThrust01 <= 0.001f)
            return;

        fighterBody.MoveRotation(0f);
        fighterBody.AddForce(Vector2.up * thrustForce * acceleration * currentThrust01);

        if (fighterBody.linearVelocity.magnitude > maxSpeed)
            fighterBody.linearVelocity = fighterBody.linearVelocity.normalized * maxSpeed;
    }

    private void UpdateRadarDemonstration(bool holdInLockCone)
    {
        Vector3 enemyPosition;
        if (holdInLockCone)
        {
            enemyPosition = RadarLockedEnemyPosition;
        }
        else
        {
            float finalSweepStart = GetFinalRadarSweepStartTime();
            if (phaseElapsed < finalSweepStart)
            {
                float path01 = (Mathf.Sin(phaseElapsed * RadarSweepAngularSpeed) + 1f) * 0.5f;
                enemyPosition = Vector3.Lerp(RadarEnemyPathLeftPosition, RadarEnemyPathRightPosition, path01);
            }
            else
            {
                float approachDuration = Mathf.Max(0.01f, radarDemoDuration - finalSweepStart);
                float approach01 = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((phaseElapsed - finalSweepStart) / approachDuration));
                Vector3 finalSweepStartPosition = Vector3.Lerp(
                    RadarEnemyPathLeftPosition, RadarEnemyPathRightPosition, 0.5f);
                enemyPosition = Vector3.Lerp(
                    finalSweepStartPosition, RadarLockedEnemyPosition, approach01);
            }
        }

        tutorialEnemy.position = enemyPosition;
        aimIcon.position = enemyPosition;

        Vector2 toEnemy = enemyPosition - RadarOriginPosition;
        float angle = Vector2.Angle(Vector2.up, toEnemy);
        bool detected = angle <= 45f;
        bool locked = angle <= 30f;
        if (phase == SequencePhase.MissileWarning)
            StopTutorialLockAudio();
        else
            UpdateTutorialLockAudio(detected, locked, enemyPosition);

        bool redMissileLock = locked && phase == SequencePhase.MissileWarning;
        aimIconRenderer.enabled = detected;
        if (detected)
        {
            aimIconRenderer.sprite = locked ? lockOnSprite : unlockSprite;
            aimIconRenderer.sharedMaterial = redMissileLock && redLockOnMaterial != null
                ? redLockOnMaterial
                : aimIconDefaultMaterial;
            aimIconRenderer.color = redMissileLock
                ? Color.white
                : locked ? new Color(0.35f, 1f, 0.35f) : Color.white;
            float pulse = 1f + Mathf.Sin(Time.time * 7f) * 0.08f;
            aimIcon.localScale = Vector3.one * aimIconBaseScale * pulse;
        }

        tutorialEnemyRenderer.color = new Color(1f, 0.55f, 0.18f);
    }

    private void UpdateTutorialLockAudio(bool detected, bool locked, Vector3 enemyPosition)
    {
        if (tutorialLockAudioSource == null)
            return;

        tutorialLockAudioSource.transform.position = enemyPosition;
        AudioClip targetClip = detected
            ? (locked ? tutorialLockOnClip : tutorialUnlockClip)
            : null;

        if (targetClip == null)
        {
            StopTutorialLockAudio();
            return;
        }

        if (tutorialLockAudioSource.clip != targetClip)
        {
            tutorialLockAudioSource.Stop();
            tutorialLockAudioSource.clip = targetClip;
            tutorialLockAudioSource.Play();
        }
        else if (!tutorialLockAudioSource.isPlaying)
        {
            tutorialLockAudioSource.Play();
        }
    }

    private void StopTutorialLockAudio()
    {
        if (tutorialLockAudioSource == null)
            return;

        tutorialLockAudioSource.Stop();
        tutorialLockAudioSource.clip = null;
    }

    private void UpdateFlightInput()
    {
        if (maxLaunchThrustLocked)
        {
            currentThrust01 = 1f;
            thrustHoldTimer = maxThrustChargeTime;
            hasThrustInput = true;
            return;
        }

        hasThrustInput = IsPointerHeld();
        if (hasThrustInput)
        {
            thrustHoldTimer += Time.deltaTime;
            currentThrust01 = Mathf.Clamp01(thrustHoldTimer / Mathf.Max(0.01f, maxThrustChargeTime));
            if (currentThrust01 >= 1f)
            {
                maxLaunchThrustLocked = true;
                currentThrust01 = 1f;
                thrustHoldTimer = maxThrustChargeTime;
            }
        }
        else
        {
            currentThrust01 = Mathf.MoveTowards(
                currentThrust01,
                0f,
                Time.deltaTime / Mathf.Max(0.01f, thrustReleaseDecayTime));
            thrustHoldTimer = currentThrust01 * maxThrustChargeTime;
        }
    }

    private void UpdateBoosterEffect()
    {
        float flicker = 1f + Mathf.Sin(Time.time * 32f) * 0.08f * currentThrust01;
        if (boosterTriangles != null && boosterTriangles.Length > 0)
        {
            foreach (BoosterFlameTriangle triangle in boosterTriangles)
            {
                if (triangle != null)
                    triangle.SetThrust(currentThrust01, flicker);
            }
            return;
        }

        if (flame == null)
            return;

        flame.position = fighter.position - Vector3.up * 0.72f;
        flame.rotation = Quaternion.identity;
        flame.localScale = new Vector3(0.12f, Mathf.Lerp(0.08f, 0.9f, currentThrust01) * flicker, 1f);
    }

    private void UpdateLaunchCameraFollow()
    {
        if (mainCamera == null)
            return;

        Vector3 targetPosition = fighter.position + launchCameraOffset;
        targetPosition.z = launchCameraOffset.z;
        mainCamera.transform.position = Vector3.SmoothDamp(
            mainCamera.transform.position,
            targetPosition,
            ref launchCameraVelocity,
            Mathf.Max(0.01f, launchCameraSmoothTime));
    }

    private bool HasFighterExitedCameraView()
    {
        if (mainCamera == null || fighter == null)
            return false;

        float cameraTop = mainCamera.ViewportToWorldPoint(new Vector3(0.5f, 1f, 0f)).y;
        bool foundVisibleRenderer = false;
        if (launchFighterRenderers != null)
        {
            foreach (Renderer renderer in launchFighterRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                foundVisibleRenderer = true;
                if (renderer.bounds.min.y <= cameraTop)
                    return false;
            }
        }

        return foundVisibleRenderer
            || mainCamera.WorldToViewportPoint(fighter.position).y > 1.05f;
    }

    private IEnumerator LoadBattleScene()
    {
        loading = true;
        yield return new WaitForSeconds(0.35f);
        FlightController.PlayEntryAnimationOnNextLoad = true;
        SceneManager.LoadScene(battleSceneName);
    }

    private void BuildScene()
    {
        tutorialRoot = new GameObject("Radar Tutorial").transform;
        tutorialRoot.SetParent(transform);
        BuildRadarTutorial();
        tutorialRoot.gameObject.SetActive(false);

        flightVisualRoot = new GameObject("Carrier Launch").transform;
        flightVisualRoot.SetParent(transform);
        BuildCarrierRunway();
        BuildLaunchFighter();
        BuildThrustUI();
        flightVisualRoot.gameObject.SetActive(false);
    }

    private void BuildRadarTutorial()
    {
        CreateSector("Detection Cone 90", RadarOriginPosition, radarConeRadius, 45f,
            new Color(1f, 1f, 1f, 0.16f), 20, tutorialRoot);
        CreateSector("Lock-on Cone 60", RadarOriginPosition, radarConeRadius, 30f,
            new Color(0.45f, 1f, 0.35f, 0.25f), 21, tutorialRoot);

        tutorialEnemy = CreatePanel("Virtual Enemy", RadarEnemyPathLeftPosition,
            radarEnemySize, new Color(1f, 0.55f, 0.18f), 27, tutorialRoot).transform;
        tutorialEnemy.rotation = Quaternion.Euler(0f, 0f, 45f);
        tutorialEnemyRenderer = tutorialEnemy.GetComponent<SpriteRenderer>();

        aimIcon = new GameObject("Tutorial Aim Icon").transform;
        aimIcon.SetParent(tutorialRoot);
        aimIconRenderer = aimIcon.gameObject.AddComponent<SpriteRenderer>();
        aimIconRenderer.sprite = unlockSprite != null ? unlockSprite : GetWhiteSprite();
        aimIconRenderer.sortingOrder = 30;
        aimIconDefaultMaterial = aimIconRenderer.sharedMaterial;
        Shader redLockShader = Resources.Load<Shader>("LockOnSolidColor");
        if (redLockShader != null)
        {
            redLockOnMaterial = new Material(redLockShader) { name = "Runtime Red Lock-On" };
            redLockOnMaterial.SetColor("_SolidColor", new Color(1f, 0.015f, 0.005f, 1f));
        }
        float iconHeight = aimIconRenderer.sprite.bounds.size.y;
        aimIconBaseScale = radarAimIconHeight / Mathf.Max(0.01f, iconHeight);
        aimIcon.localScale = Vector3.one * aimIconBaseScale;
        aimIconRenderer.enabled = false;

    }

    private void BuildCarrierRunway()
    {
        GameObject carrier = new GameObject("Carrier Runway");
        carrier.transform.SetParent(flightVisualRoot);
        carrier.transform.position = CarrierPosition;
        SpriteRenderer renderer = carrier.AddComponent<SpriteRenderer>();
        renderer.sprite = carrierRunwaySprite != null ? carrierRunwaySprite : GetWhiteSprite();
        renderer.sortingOrder = 1;
        float scale = 10.8f / Mathf.Max(0.01f, renderer.sprite.bounds.size.y);
        carrier.transform.localScale = Vector3.one * scale;

        CreateStars(flightVisualRoot);
    }

    private void BuildLaunchFighter()
    {
        GameObject fighterObject = fighterPrefab != null
            ? Instantiate(fighterPrefab)
            : new GameObject("UF-35 Launch Fighter");
        fighterObject.name = "UF-35 Launch Fighter";
        fighter = fighterObject.transform;
        fighter.SetParent(flightVisualRoot);
        fighter.position = FighterStartPosition;
        fighter.rotation = Quaternion.identity;

        SpriteRenderer fighterRenderer = fighter.GetComponent<SpriteRenderer>();
        if (fighterRenderer == null)
            fighterRenderer = fighter.gameObject.AddComponent<SpriteRenderer>();
        fighterRenderer.sprite = fighterSprite != null ? fighterSprite : GetWhiteSprite();
        foreach (SpriteRenderer renderer in fighter.GetComponentsInChildren<SpriteRenderer>(true))
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, FighterTutorialSortingOrder);
        launchFighterRenderers = fighter.GetComponentsInChildren<Renderer>(true);

        FlightController sourceController = fighter.GetComponent<FlightController>();
        if (sourceController != null)
        {
            thrustForce = sourceController.thrustForce;
            maxSpeed = sourceController.maxSpeed;
            acceleration = sourceController.accel;
            thrustLinearDamping = sourceController.thrustLinearDamping;
            coastLinearDamping = sourceController.coastLinearDamping;
            maxThrustChargeTime = sourceController.maxThrustChargeTime;
            thrustReleaseDecayTime = sourceController.thrustReleaseDecayTime;
            thrustGaugeRiseSpeed = sourceController.thrustGaugeRiseSpeed;
            thrustGaugeFallSpeed = sourceController.thrustGaugeFallSpeed;
            launchBooster = sourceController.boosterFlame;
            sourceController.enabled = false;
        }

        fighterBody = fighter.GetComponent<Rigidbody2D>();
        if (fighterBody == null)
            fighterBody = fighter.gameObject.AddComponent<Rigidbody2D>();
        fighterBody.gravityScale = 0f;
        fighterBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        fighterBody.linearVelocity = Vector2.zero;
        fighterBody.angularVelocity = 0f;
        launchCameraOffset = mainCamera.transform.position - fighter.position;

        if (launchBooster != null)
        {
            boosterTriangles = launchBooster.GetComponentsInChildren<BoosterFlameTriangle>(true);
            launchBooster.SetActive(false);
        }
        else
        {
            flame = CreatePanel("Booster Flame", FighterStartPosition + Vector3.down * 0.7f,
                new Vector2(0.06f, 0.08f), new Color(1f, 0.2f, 0.05f, 0.9f), 11, flightVisualRoot).transform;
            launchBooster = flame.gameObject;
            launchBooster.SetActive(false);
        }
    }

    private void BuildThrustUI()
    {
        if (fighterUIAsset == null || fighterUIPanelSettings == null)
            return;

        GameObject uiObject = new GameObject("SpriteFlight Thrust UI");
        uiObject.transform.SetParent(transform);
        UIDocument document = uiObject.AddComponent<UIDocument>();
        document.panelSettings = fighterUIPanelSettings;
        document.visualTreeAsset = fighterUIAsset;

        VisualElement root = document.rootVisualElement;
        thrustGauge = root.Q<VisualElement>("ThrustGauge");
        thrustGaugeFill = root.Q<VisualElement>("ThrustGaugeFill");
        thrustGaugeValueLabel = root.Q<Label>("ThrustGaugeValue");

        foreach (VisualElement child in root.Children())
        {
            if (child != thrustGauge)
                child.style.display = DisplayStyle.None;
        }

        SetThrustUIVisible(false);
        UpdateThrustGaugeUI(true);
    }

    private void SetThrustUIVisible(bool visible)
    {
        if (thrustGauge != null)
            thrustGauge.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void UpdateThrustGaugeUI(bool instant = false)
    {
        float speed = currentThrust01 > displayedThrust01 ? thrustGaugeRiseSpeed : thrustGaugeFallSpeed;
        displayedThrust01 = instant
            ? currentThrust01
            : Mathf.MoveTowards(displayedThrust01, currentThrust01, speed * Time.deltaTime);

        int percent = Mathf.RoundToInt(displayedThrust01 * 100f);
        if (thrustGaugeFill != null)
            thrustGaugeFill.style.height = Length.Percent(percent);
        if (thrustGaugeValueLabel != null)
            thrustGaugeValueLabel.text = $"{percent}%";
    }

    private void OnGUI()
    {
        GUIStyle header = CreateStyle(Mathf.RoundToInt(Screen.height * 0.022f), FontStyle.Bold, new Color(0.4f, 0.95f, 1f));
        GUIStyle body = CreateStyle(Mathf.RoundToInt(Screen.height * 0.019f), FontStyle.Bold, Color.white);
        GUIStyle small = CreateStyle(Mathf.RoundToInt(Screen.height * 0.015f), FontStyle.Bold, new Color(0.7f, 0.92f, 1f));

        if (phase == SequencePhase.RadioCheck)
            DrawRadioCheckFade();

        if (ShouldShowOperatorPanel() || layoutEditMode)
            DrawOperatorPanel(header, body);

        if (phase == SequencePhase.RadarDemo || phase == SequencePhase.WeaponCheck || phase == SequencePhase.MissileWarning || layoutEditMode)
            DrawRadarTutorialLabels(header, small);

        if (layoutEditMode)
        {
            GUIStyle editStyle = CreateStyle(Mathf.RoundToInt(Screen.height * 0.018f), FontStyle.Bold, new Color(1f, 0.85f, 0.15f));
            GUI.Label(new Rect(0f, Screen.height - 42f, Screen.width, 34f),
                "LAYOUT EDIT  |  DRAG PANEL TO MOVE  |  DRAG YELLOW CORNER TO RESIZE  |  F2 TO FINISH", editStyle);
        }

        if (phase == SequencePhase.MissileWarning && IsMissileWarningVisible())
        {
            GUIStyle warning = CreateStyle(Mathf.RoundToInt(Screen.height * 0.065f), FontStyle.Bold, new Color(1f, 0.08f, 0.04f));
            Vector3 screenPoint = mainCamera != null && missileWarningTextHandle != null
                ? mainCamera.WorldToScreenPoint(missileWarningTextHandle.position)
                : new Vector3(Screen.width * 0.5f, Screen.height * 0.57f);
            Rect warningRect = new Rect(screenPoint.x - Screen.width * 0.5f,
                Screen.height - screenPoint.y - 45f, Screen.width, 90f);
            string warningText = textSettings != null && !string.IsNullOrWhiteSpace(textSettings.missileWarning)
                ? NormalizeDisplayText(textSettings.missileWarning)
                : "MISSILE LAUNCH";

            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = -100;
            GUI.color = Color.white;
            GUIStyle warningShadow = new GUIStyle(warning);
            warningShadow.normal.textColor = Color.black;
            GUI.Label(new Rect(warningRect.x + 3f, warningRect.y + 3f,
                warningRect.width, warningRect.height), warningText, warningShadow);
            GUI.Label(warningRect, warningText, warning);
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        if (phase == SequencePhase.FadeToRunway)
        {
            GUI.color = new Color(0f, 0f, 0f, 1f - fade01);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        if (phase == SequencePhase.AwaitLaunch || phase == SequencePhase.Launching)
            DrawLaunchPrompt(header, body);

        // Keep the skip control above every tutorial panel, warning and full-screen overlay.
        if (ShouldShowSkipButton())
            DrawSkipButton();
    }

    private void DrawRadioCheckFade()
    {
        float progress = Mathf.Clamp01(
            (phaseElapsed - radioFadeStartTime) / Mathf.Max(0.01f, radioFadeDuration));
        GUI.color = new Color(0f, 0f, 0f, 1f - progress);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private bool ShouldShowOperatorPanel()
    {
        return phase == SequencePhase.RadioCheck
            || phase == SequencePhase.LoudClear
            || phase == SequencePhase.RadarDemo
            || phase == SequencePhase.WeaponCheck
            || phase == SequencePhase.MissileWarning
            || phase == SequencePhase.Clearance;
    }

    private bool ShouldShowSkipButton()
    {
        return !layoutEditMode
            && !tutorialSkipped
            && (phase == SequencePhase.RadioCheck
                || phase == SequencePhase.LoudClear
                || phase == SequencePhase.RadarDemo
                || phase == SequencePhase.WeaponCheck
                || phase == SequencePhase.MissileWarning);
    }

    private void DrawSkipButton()
    {
        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        GUI.depth = -1000;
        GUI.color = Color.white;

        float height = Mathf.Clamp(Screen.height * 0.052f, 34f, 58f);
        float width = Mathf.Clamp(Screen.width * 0.115f, 118f, 190f);
        float margin = Mathf.Clamp(Screen.height * 0.026f, 16f, 30f);
        Rect buttonRect = new Rect(Screen.width - width - margin, margin, width, height);

        GUIStyle hitArea = new GUIStyle(GUI.skin.button);
        hitArea.normal.background = null;
        hitArea.hover.background = null;
        hitArea.active.background = null;
        hitArea.border = new RectOffset();

        // Draw the click target first so Unity's default button skin cannot cover
        // the custom neon label and arrow.
        bool clicked = GUI.Button(buttonRect, GUIContent.none, hitArea);
        bool hovered = buttonRect.Contains(Event.current.mousePosition);
        Color accent = hovered
            ? new Color(0.72f, 1f, 0.25f)
            : new Color(0.35f, 1f, 0.08f);

        DrawHudPanel(
            buttonRect,
            hovered
                ? new Color(0.12f, 0.28f, 0.03f, 0.96f)
                : new Color(0.035f, 0.12f, 0.015f, 0.94f),
            accent);

        GUIStyle labelStyle = CreateStyle(
            Mathf.RoundToInt(Screen.height * 0.025f),
            FontStyle.Bold,
            accent);
        labelStyle.alignment = TextAnchor.MiddleRight;

        float arrowSize = height * 0.46f;
        float arrowRight = buttonRect.xMax - 4f;
        Rect arrowRect = new Rect(
            arrowRight - arrowSize,
            buttonRect.center.y - arrowSize * 0.5f,
            arrowSize,
            arrowSize);
        Rect labelRect = new Rect(
            buttonRect.x,
            buttonRect.y,
            buttonRect.width - arrowSize - 13f,
            buttonRect.height);

        GUI.Label(labelRect, "SKIP", labelStyle);
        GUI.color = accent;
        GUI.DrawTexture(arrowRect, GetSkipArrowTexture(), ScaleMode.StretchToFill, true);
        GUI.color = previousColor;

        if (clicked)
            SkipToSystemsAllGreen();

        GUI.depth = previousDepth;
    }

    private static Texture2D GetSkipArrowTexture()
    {
        if (skipArrowTexture != null)
            return skipArrowTexture;

        const int size = 32;
        skipArrowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        skipArrowTexture.name = "Runtime Skip Arrow";
        skipArrowTexture.hideFlags = HideFlags.HideAndDontSave;
        skipArrowTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float halfHeight = center * (1f - x / (size - 1f));
                pixels[y * size + x] = Mathf.Abs(y - center) <= halfHeight
                    ? Color.white
                    : Color.clear;
            }
        }

        skipArrowTexture.SetPixels(pixels);
        skipArrowTexture.Apply(false, true);
        return skipArrowTexture;
    }

    private void DrawOperatorPanel(GUIStyle header, GUIStyle body)
    {
        Rect panel = NormalizedScreenRect(operatorPanelPosition, operatorPanelSize);
        float tvVisibility = GetOperatorTvVisibility();
        Matrix4x4 previousMatrix = GUI.matrix;
        ApplyTvPanelTransform(panel, tvVisibility);

        DrawHudPanel(panel, new Color(0.015f, 0.09f, 0.14f, 0.93f), new Color(0.08f, 0.9f, 1f));

        float portraitRatio = Mathf.Clamp(operatorPortraitWidthRatio, 0.25f, 0.75f);
        Rect imageRect = new Rect(panel.x + 10f, panel.y + 34f, panel.width * portraitRatio, panel.height - 44f);
        if (operatorSprite != null)
        {
            Rect uvRect = CalculatePortraitUV(imageRect, operatorSprite.texture);
            GUI.DrawTextureWithTexCoords(imageRect, operatorSprite.texture, uvRect, true);
        }

        header.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(panel.x + 12f, panel.y + 2f, panel.width - 24f, 28f), "CARRIER COMM // IRIS", header);

        body.alignment = TextAnchor.MiddleLeft;
        body.wordWrap = true;
        float textStartRatio = portraitRatio + 0.03f;
        Rect textRect = new Rect(panel.x + panel.width * textStartRatio, panel.y + 38f,
            panel.width * (0.98f - textStartRatio), panel.height - 48f);
        GUI.Label(textRect, layoutEditMode ? "VIPER 01, IRIS.\n\nOPERATOR PANEL PREVIEW." : GetTypedOperatorMessage(), body);

        GUI.matrix = previousMatrix;
        DrawTvTransitionOverlay(panel, tvVisibility);
        HandleLayoutPanel(1, panel, ref operatorPanelPosition, ref operatorPanelSize, "OPERATOR PANEL");
    }

    private string GetTypedOperatorMessage()
    {
        string message = GetOperatorMessage(phase);
        int visibleCharacters = Mathf.Clamp(Mathf.FloorToInt(typedCharacterCount), 0, message.Length);
        string visibleText = message.Substring(0, visibleCharacters);
        if (visibleCharacters < message.Length && Mathf.Repeat(Time.unscaledTime, 0.7f) < 0.45f)
            visibleText += "_";
        return visibleText;
    }

    private string GetOperatorMessage(SequencePhase targetPhase)
    {
        if (textSettings != null)
        {
            switch (targetPhase)
            {
                case SequencePhase.RadioCheck: return NormalizeDisplayText(textSettings.radioCheck);
                case SequencePhase.LoudClear: return NormalizeDisplayText(textSettings.loudAndClear);
                case SequencePhase.RadarDemo:
                    if (phaseElapsed >= GetFinalRadarSweepStartTime())
                        return NormalizeDisplayText(textSettings.finalWeaponCheck);
                    if (phaseElapsed >= GetSecondRadarSweepStartTime())
                        return NormalizeDisplayText(textSettings.radarSystemGreen);
                    return NormalizeDisplayText(textSettings.radarCheck);
                case SequencePhase.WeaponCheck:
                case SequencePhase.MissileWarning:
                    return NormalizeDisplayText(textSettings.finalWeaponCheck);
                case SequencePhase.Clearance: return NormalizeDisplayText(textSettings.clearance);
                default: return string.Empty;
            }
        }

        switch (targetPhase)
        {
            case SequencePhase.RadioCheck:
                return "VIPER 01, IRIS.\n\nRADIO CHECK.";
            case SequencePhase.LoudClear:
                return "VIPER 01, LOUD & CLEAR.";
            case SequencePhase.RadarDemo:
                if (phaseElapsed >= GetFinalRadarSweepStartTime())
                    return "VIPER 01,\nWEAPON CHECK.";
                if (phaseElapsed >= GetSecondRadarSweepStartTime())
                    return "VIPER 01,\nRADAR SYSTEM GREEN.";
                return "VIPER 01, RADAR CHECK.";
            case SequencePhase.WeaponCheck:
            case SequencePhase.MissileWarning:
                return "VIPER 01,\nWEAPON CHECK.";
            case SequencePhase.Clearance:
                return "VIPER 01,\nSYSTEMS ALL GREEN.\n\nCLEARED FOR LAUNCH.\n\nGOOD LUCK.";
            default:
                return string.Empty;
        }
    }

    public static string NormalizeDisplayText(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\n", "\n");
    }

    private static float GetSecondRadarSweepStartTime()
    {
        return Mathf.PI * 2f / RadarSweepAngularSpeed;
    }

    private static float GetFinalRadarSweepStartTime()
    {
        return Mathf.PI * 4f / RadarSweepAngularSpeed;
    }

    private void DrawRadarTutorialLabels(GUIStyle header, GUIStyle small)
    {
        Rect panel = NormalizedScreenRect(radarPanelPosition, radarPanelSize);
        float tvVisibility = GetRadarTvVisibility();
        Matrix4x4 previousMatrix = GUI.matrix;
        ApplyTvPanelTransform(panel, tvVisibility);

        DrawHudPanel(panel, new Color(0.01f, 0.035f, 0.055f, 0.04f), new Color(0.2f, 0.75f, 0.85f, 0.9f));

        header.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(panel.x, panel.y + 8f, panel.width, 34f),
            textSettings != null ? textSettings.radarTitle : "UF-35 RADAR / LOCK-ON TUTORIAL", header);

        small.alignment = TextAnchor.MiddleLeft;
        small.normal.textColor = Color.white;
        GUI.Label(new Rect(panel.x + 18f, panel.y + 48f, panel.width * 0.36f, 28f),
            textSettings != null ? textSettings.detectionLegend : "WHITE  90°  DETECTION", small);
        small.normal.textColor = new Color(0.45f, 1f, 0.4f);
        GUI.Label(new Rect(panel.x + 18f, panel.y + 76f, panel.width * 0.36f, 28f),
            textSettings != null ? textSettings.lockOnLegend : "GREEN  60°  LOCK-ON", small);

        small.alignment = TextAnchor.MiddleCenter;
        small.normal.textColor = Color.white;
        GUI.Label(new Rect(panel.x, panel.yMax - 42f, panel.width, 30f),
            textSettings != null ? textSettings.radarDescription : "TARGET ENTERS 90°: DETECT  /  TARGET ENTERS 60°: LOCK ON", small);

        GUI.matrix = previousMatrix;
        DrawTvTransitionOverlay(panel, tvVisibility);
        HandleLayoutPanel(2, panel, ref radarPanelPosition, ref radarPanelSize, "RADAR PANEL");
    }

    private float GetOperatorTvVisibility()
    {
        if (layoutEditMode)
            return 1f;

        if (phase == SequencePhase.RadioCheck)
            return SmoothTvProgress(phaseElapsed / TvTurnOnDuration);

        if (phase == SequencePhase.Clearance)
        {
            float turnOffStart = Mathf.Max(0f, clearanceDuration - TvTurnOffDuration);
            if (phaseElapsed >= turnOffStart)
                return 1f - SmoothTvProgress((phaseElapsed - turnOffStart) / TvTurnOffDuration);
        }

        return 1f;
    }

    private float GetRadarTvVisibility()
    {
        if (layoutEditMode)
            return 1f;

        if (phase == SequencePhase.RadarDemo)
            return SmoothTvProgress(phaseElapsed / TvTurnOnDuration);

        if (phase == SequencePhase.MissileWarning)
        {
            float turnOffStart = Mathf.Max(0f, MissileWarningSequenceDuration - TvTurnOffDuration);
            if (phaseElapsed >= turnOffStart)
                return 1f - SmoothTvProgress((phaseElapsed - turnOffStart) / TvTurnOffDuration);
        }

        return 1f;
    }

    private static float SmoothTvProgress(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static void ApplyTvPanelTransform(Rect panel, float visibility)
    {
        visibility = Mathf.Clamp01(visibility);
        float horizontalScale = Mathf.Lerp(0.04f, 1f, SmoothTvProgress(visibility * 3.2f));
        float verticalScale = Mathf.Lerp(0.012f, 1f, SmoothTvProgress(visibility));

        if (visibility > 0.001f && visibility < 0.999f)
            horizontalScale *= 1f + Mathf.Sin(Time.unscaledTime * 95f) * 0.012f;

        GUIUtility.ScaleAroundPivot(
            new Vector2(Mathf.Max(0.01f, horizontalScale), Mathf.Max(0.01f, verticalScale)),
            panel.center);
    }

    private static void DrawTvTransitionOverlay(Rect panel, float visibility)
    {
        visibility = Mathf.Clamp01(visibility);
        if (visibility >= 0.999f)
            return;

        float horizontalScale = Mathf.Lerp(0.04f, 1f, SmoothTvProgress(visibility * 3.2f));
        float verticalScale = Mathf.Lerp(0.012f, 1f, SmoothTvProgress(visibility));
        Rect visibleRect = new Rect(
            panel.center.x - panel.width * horizontalScale * 0.5f,
            panel.center.y - panel.height * verticalScale * 0.5f,
            panel.width * horizontalScale,
            panel.height * verticalScale);

        Color previousColor = GUI.color;
        float flicker = 0.72f + Mathf.Sin(Time.unscaledTime * 83f) * 0.16f;

        GUI.color = new Color(0.35f, 1f, 1f, 0.14f * flicker);
        GUI.DrawTexture(new Rect(visibleRect.x, panel.center.y - 5f, visibleRect.width, 10f), Texture2D.whiteTexture);
        GUI.color = new Color(0.72f, 1f, 1f, 0.72f * flicker);
        GUI.DrawTexture(new Rect(visibleRect.x, panel.center.y - 1f, visibleRect.width, 2f), Texture2D.whiteTexture);

        GUI.color = new Color(0.45f, 0.95f, 1f, 0.055f);
        for (float y = visibleRect.y + 3f; y < visibleRect.yMax; y += 8f)
            GUI.DrawTexture(new Rect(visibleRect.x, y, visibleRect.width, 1f), Texture2D.whiteTexture);

        GUI.color = previousColor;
    }

    private void HandleLayoutPanel(int panelId, Rect panel, ref Vector2 normalizedPosition,
        ref Vector2 normalizedSize, string panelName)
    {
        if (!layoutEditMode)
            return;

        const float handleSize = 26f;
        Rect resizeHandle = new Rect(panel.xMax - handleSize, panel.yMax - handleSize, handleSize, handleSize);
        GUI.color = new Color(1f, 0.8f, 0.05f, 0.95f);
        GUI.DrawTexture(resizeHandle, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle labelStyle = CreateStyle(Mathf.RoundToInt(Screen.height * 0.013f), FontStyle.Bold, new Color(1f, 0.85f, 0.15f));
        labelStyle.alignment = TextAnchor.UpperLeft;
        GUI.Label(new Rect(panel.x + 6f, panel.y + 4f, panel.width - 40f, 24f), panelName, labelStyle);

        Event current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 0)
        {
            if (resizeHandle.Contains(current.mousePosition))
            {
                activeLayoutControl = panelId + 10;
            }
            else if (panel.Contains(current.mousePosition))
            {
                activeLayoutControl = panelId;
            }
            else
            {
                return;
            }

            layoutDragStartMouse = current.mousePosition;
            layoutDragStartPosition = normalizedPosition;
            layoutDragStartSize = normalizedSize;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && current.button == 0)
        {
            Vector2 delta = current.mousePosition - layoutDragStartMouse;
            Vector2 normalizedDelta = new Vector2(delta.x / Screen.width, delta.y / Screen.height);

            if (activeLayoutControl == panelId)
            {
                normalizedPosition = layoutDragStartPosition + normalizedDelta;
                normalizedPosition.x = Mathf.Clamp(normalizedPosition.x, 0f, 1f - normalizedSize.x);
                normalizedPosition.y = Mathf.Clamp(normalizedPosition.y, 0f, 1f - normalizedSize.y);
                current.Use();
            }
            else if (activeLayoutControl == panelId + 10)
            {
                normalizedSize = layoutDragStartSize + normalizedDelta;
                normalizedSize.x = Mathf.Clamp(normalizedSize.x, 0.15f, 1f - normalizedPosition.x);
                normalizedSize.y = Mathf.Clamp(normalizedSize.y, 0.15f, 1f - normalizedPosition.y);
                current.Use();
            }
        }
        else if (current.type == EventType.MouseUp && current.button == 0
            && (activeLayoutControl == panelId || activeLayoutControl == panelId + 10))
        {
            activeLayoutControl = 0;
            current.Use();
        }
    }

    private bool IsMissileWarningVisible()
    {
        int blinkIndex = Mathf.FloorToInt(phaseElapsed / MissileWarningBlinkInterval);
        return blinkIndex < MissileWarningBlinkCount
            && phaseElapsed - blinkIndex * MissileWarningBlinkInterval < MissileWarningVisibleDuration;
    }

    private void DrawLaunchPrompt(GUIStyle header, GUIStyle body)
    {
        header.alignment = TextAnchor.MiddleCenter;
        string prompt = phase == SequencePhase.AwaitLaunch
            ? (textSettings != null ? textSettings.pressToLaunch : "PRESS LEFT CLICK")
            : maxLaunchThrustLocked || hasLeftCarrier
                ? (textSettings != null ? textSettings.maximumThrust : "MAXIMUM THRUST")
                : (textSettings != null ? textSettings.launching : "LAUNCHING");
        GUI.Label(new Rect(0f, Screen.height * 0.08f, Screen.width, 46f), prompt, header);

        body.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0f, Screen.height * 0.125f, Screen.width, 34f),
            maxLaunchThrustLocked || hasLeftCarrier
                ? (textSettings != null ? textSettings.maxThrustCruise : "MAX THRUST CRUISE")
                : (textSettings != null ? textSettings.thrustInstruction : "HOLD TO BUILD THRUST  •  HEADING LOCKED NORTH"), body);
    }

    private void CreateSector(string objectName, Vector3 origin, float radius, float halfAngle,
        Color color, int sortingOrder, Transform parent)
    {
        const int segments = 32;
        Mesh mesh = new Mesh { name = objectName + " Mesh" };
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * radius;
            if (i < segments)
            {
                int triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = i + 2;
            }
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        GameObject sector = new GameObject(objectName);
        sector.transform.SetParent(parent);
        sector.transform.position = origin;
        sector.AddComponent<MeshFilter>().mesh = mesh;
        MeshRenderer renderer = sector.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Sprites/Default");
        renderer.material = new Material(shader) { color = color };
        renderer.sortingOrder = sortingOrder;
    }

    private void CreateStars(Transform parent)
    {
        Random.State previous = Random.state;
        Random.InitState(20260714);
        for (int i = 0; i < 70; i++)
        {
            float size = Random.Range(0.015f, 0.055f);
            CreatePanel("Star", new Vector3(Random.Range(-5.5f, 5.5f), Random.Range(-4.8f, 4.8f)),
                Vector2.one * size, new Color(0.65f, 0.8f, 1f, Random.Range(0.4f, 0.9f)), -5, parent);
        }
        Random.state = previous;
    }

    private GameObject CreatePanel(string objectName, Vector3 position, Vector2 size, Color color,
        int order, Transform parent)
    {
        GameObject panel = new GameObject(objectName);
        panel.transform.SetParent(parent);
        panel.transform.position = position;
        panel.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = panel.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        renderer.sortingOrder = order;
        return panel;
    }

    private static GUIStyle CreateStyle(int fontSize, FontStyle fontStyle, Color color)
    {
        if (galmuri11Font == null)
            galmuri11Font = Resources.Load<Font>("Galmuri11-Condensed");

        return new GUIStyle(GUI.skin.label)
        {
            font = galmuri11Font,
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Max(12, fontSize) + 1,
            fontStyle = fontStyle,
            wordWrap = true,
            normal = { textColor = color }
        };
    }

    private static void DrawHudPanel(Rect rect, Color fill, Color border)
    {
        GUI.color = fill;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = border;
        const float thickness = 2f;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private static Rect NormalizedScreenRect(Vector2 position, Vector2 size)
    {
        float width = Mathf.Clamp(size.x, 0.1f, 1f - Mathf.Clamp01(position.x)) * Screen.width;
        float height = Mathf.Clamp(size.y, 0.1f, 1f - Mathf.Clamp01(position.y)) * Screen.height;
        return new Rect(
            Mathf.Clamp01(position.x) * Screen.width,
            Mathf.Clamp01(position.y) * Screen.height,
            width,
            height);
    }

    private Rect CalculatePortraitUV(Rect destination, Texture texture)
    {
        float zoom = Mathf.Clamp(operatorPortraitZoom, 1f, 4f);
        float textureAspect = texture.width / (float)Mathf.Max(1, texture.height);
        float destinationAspect = destination.width / Mathf.Max(1f, destination.height);

        float uvHeight = 1f / zoom;
        float uvWidth = uvHeight * destinationAspect / Mathf.Max(0.01f, textureAspect);
        if (uvWidth > 1f)
        {
            uvHeight /= uvWidth;
            uvWidth = 1f;
        }

        Vector2 focus = new Vector2(
            Mathf.Clamp01(operatorPortraitFocus.x),
            Mathf.Clamp01(operatorPortraitFocus.y));
        float x = Mathf.Clamp(focus.x - uvWidth * 0.5f, 0f, 1f - uvWidth);
        float y = Mathf.Clamp(focus.y - uvHeight * 0.5f, 0f, 1f - uvHeight);
        return new Rect(x, y, uvWidth, uvHeight);
    }

    private bool IsPointerHeld()
    {
        return (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            || (Mouse.current != null && Mouse.current.leftButton.isPressed);
    }

    private bool TryGetHeldPointerPosition(out Vector2 position)
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }

        position = default;
        return false;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D texture = new Texture2D(1, 1) { name = "RuntimeWhitePixel" };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        whiteSprite.name = "RuntimeWhiteSprite";
        return whiteSprite;
    }
}
