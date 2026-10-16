using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FighterWarningUIManager : MonoBehaviour
{
    [Header("References")]
    public Camera targetCamera;
    public Canvas targetCanvas;

    [Header("Enemy Warning")]
    public Sprite warningSprite;
    public float edgePadding = 60f;
    public float iconSize = 56f;
    public float blinkSpeed = 5f;
    public Color iconColor = Color.white;

    [Header("Debris Warning")]
    public bool showDebrisWarnings = true;
    public Sprite debrisWarningSprite;
    public float debrisWarningDistance = 10f;
    public int maxDebrisWarnings = 6;
    public float debrisIconSize = 42f;
    public Color debrisIconColor = Color.white;

    [Header("Performance")]
    [Min(0.05f)] public float objectRescanInterval = 0.25f;

    private readonly Dictionary<EnemyController, RectTransform> fighterWarningIcons =
        new Dictionary<EnemyController, RectTransform>();
    private readonly Dictionary<DebrisController, RectTransform> debrisWarningIcons =
        new Dictionary<DebrisController, RectTransform>();
    private readonly Dictionary<RectTransform, CanvasGroup> iconCanvasGroups =
        new Dictionary<RectTransform, CanvasGroup>();
    private readonly List<EnemyController> trackedFighters = new List<EnemyController>();
    private readonly List<DebrisController> trackedDebris = new List<DebrisController>();
    private readonly HashSet<EnemyController> activeFighters = new HashSet<EnemyController>();
    private readonly HashSet<DebrisController> trackedDebrisSet = new HashSet<DebrisController>();
    private readonly HashSet<DebrisController> activeDebris = new HashSet<DebrisController>();
    private readonly List<DebrisController> nearbyOffscreenDebris = new List<DebrisController>();
    private readonly List<EnemyController> fighterRemoveList = new List<EnemyController>();
    private readonly List<DebrisController> debrisRemoveList = new List<DebrisController>();
    private readonly Stack<RectTransform> fighterIconPool = new Stack<RectTransform>();
    private readonly Stack<RectTransform> debrisIconPool = new Stack<RectTransform>();

    private FlightController targetPlayer;
    private Vector3 debrisSortOrigin;

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        targetPlayer = FindFirstObjectByType<FlightController>();

        EnemyController.EnemySpawned += TrackFighter;
        EnemyController.EnemyRemoved += UntrackFighter;
        DebrisController.DebrisSpawned += TrackDebris;
        DebrisController.DebrisRemoved += UntrackDebris;

        foreach (EnemyController fighter in EnemyController.ActiveInstances)
            TrackFighter(fighter);
        foreach (DebrisController debris in DebrisController.ActiveInstances)
            TrackDebris(debris);
    }

    void LateUpdate()
    {
        if (targetCamera == null || targetCanvas == null)
            return;

        UpdateEnemyWarnings();
        UpdateDebrisWarnings();
    }

    void OnDestroy()
    {
        EnemyController.EnemySpawned -= TrackFighter;
        EnemyController.EnemyRemoved -= UntrackFighter;
        DebrisController.DebrisSpawned -= TrackDebris;
        DebrisController.DebrisRemoved -= UntrackDebris;
    }

    void TrackFighter(EnemyController fighter)
    {
        if (fighter != null && !trackedFighters.Contains(fighter))
            trackedFighters.Add(fighter);
    }

    void UntrackFighter(EnemyController fighter)
    {
        trackedFighters.Remove(fighter);
        activeFighters.Remove(fighter);

        if (fighterWarningIcons.TryGetValue(fighter, out RectTransform icon))
        {
            if (icon != null)
                ReleaseIcon(icon, fighterIconPool);
            fighterWarningIcons.Remove(fighter);
        }
    }

    void TrackDebris(DebrisController debris)
    {
        if (debris != null && trackedDebrisSet.Add(debris))
            trackedDebris.Add(debris);
    }

    void UntrackDebris(DebrisController debris)
    {
        trackedDebris.Remove(debris);
        trackedDebrisSet.Remove(debris);
        activeDebris.Remove(debris);

        if (debrisWarningIcons.TryGetValue(debris, out RectTransform icon))
        {
            if (icon != null)
                ReleaseIcon(icon, debrisIconPool);
            debrisWarningIcons.Remove(debris);
        }
    }

    void UpdateEnemyWarnings()
    {
        if (warningSprite == null)
            return;

        activeFighters.Clear();

        foreach (EnemyController fighter in trackedFighters)
        {
            if (fighter == null || fighter.IsDestroyed)
                continue;

            activeFighters.Add(fighter);

            Vector3 viewportPos = targetCamera.WorldToViewportPoint(fighter.transform.position);
            bool isOnScreen = IsInsideViewport(viewportPos);

            RectTransform icon = GetOrCreateFighterIcon(fighter);

            if (isOnScreen)
            {
                icon.gameObject.SetActive(false);
                continue;
            }

            icon.gameObject.SetActive(true);
            UpdateIcon(icon, fighter.transform.position, true, blinkSpeed);
        }

        RemoveMissingFighterIcons(activeFighters);
    }

    void UpdateDebrisWarnings()
    {
        Sprite iconSprite = debrisWarningSprite != null ? debrisWarningSprite : warningSprite;
        if (!showDebrisWarnings || iconSprite == null)
        {
            HideAllDebrisIcons();
            return;
        }

        if (targetPlayer == null)
            targetPlayer = FindFirstObjectByType<FlightController>();

        if (targetPlayer == null)
        {
            HideAllDebrisIcons();
            return;
        }

        nearbyOffscreenDebris.Clear();
        Vector3 playerPosition = targetPlayer.transform.position;
        float maxDistanceSqr = debrisWarningDistance * debrisWarningDistance;

        foreach (DebrisController debris in trackedDebris)
        {
            if (debris == null)
                continue;

            float distanceSqr = (debris.transform.position - playerPosition).sqrMagnitude;
            if (distanceSqr > maxDistanceSqr)
                continue;

            Vector3 viewportPos = targetCamera.WorldToViewportPoint(debris.transform.position);
            if (IsInsideViewport(viewportPos))
                continue;

            nearbyOffscreenDebris.Add(debris);
        }

        debrisSortOrigin = playerPosition;
        nearbyOffscreenDebris.Sort(CompareDebrisDistance);

        activeDebris.Clear();
        int visibleCount = Mathf.Min(maxDebrisWarnings, nearbyOffscreenDebris.Count);

        for (int i = 0; i < visibleCount; i++)
        {
            DebrisController debris = nearbyOffscreenDebris[i];
            activeDebris.Add(debris);

            RectTransform icon = GetOrCreateDebrisIcon(debris, iconSprite);
            icon.gameObject.SetActive(true);
            UpdateIcon(icon, debris.transform.position, false, 0f);
        }

        foreach (var pair in debrisWarningIcons)
        {
            if (pair.Value != null && !activeDebris.Contains(pair.Key))
                pair.Value.gameObject.SetActive(false);
        }

        RemoveMissingDebrisIcons();
    }

    int CompareDebrisDistance(DebrisController a, DebrisController b)
    {
        if (a == null) return b == null ? 0 : 1;
        if (b == null) return -1;

        float aDistance = (a.transform.position - debrisSortOrigin).sqrMagnitude;
        float bDistance = (b.transform.position - debrisSortOrigin).sqrMagnitude;
        return aDistance.CompareTo(bDistance);
    }

    bool IsInsideViewport(Vector3 viewportPos)
    {
        return viewportPos.z > 0f
            && viewportPos.x >= 0f && viewportPos.x <= 1f
            && viewportPos.y >= 0f && viewportPos.y <= 1f;
    }

    RectTransform GetOrCreateFighterIcon(EnemyController fighter)
    {
        if (fighterWarningIcons.TryGetValue(fighter, out RectTransform icon))
            return icon;

        RectTransform rect = AcquireIcon(
            fighterIconPool,
            "FighterWarningIcon",
            warningSprite,
            iconColor,
            iconSize);
        fighterWarningIcons.Add(fighter, rect);
        return rect;
    }

    RectTransform GetOrCreateDebrisIcon(DebrisController debris, Sprite iconSprite)
    {
        if (debrisWarningIcons.TryGetValue(debris, out RectTransform icon))
            return icon;

        RectTransform rect = AcquireIcon(
            debrisIconPool,
            "DebrisWarningIcon",
            iconSprite,
            debrisIconColor,
            debrisIconSize);
        debrisWarningIcons.Add(debris, rect);
        return rect;
    }

    RectTransform AcquireIcon(
        Stack<RectTransform> pool,
        string objectName,
        Sprite sprite,
        Color color,
        float size)
    {
        RectTransform rect;
        Image warningImage;
        CanvasGroup canvasGroup;

        if (pool.Count > 0)
        {
            rect = pool.Pop();
            rect.SetParent(transform, false);
            rect.gameObject.SetActive(true);
            rect.gameObject.name = objectName;
            warningImage = rect.GetComponent<Image>();
            canvasGroup = rect.GetComponent<CanvasGroup>();
        }
        else
        {
            GameObject iconObject = new GameObject(objectName, typeof(RectTransform));
            iconObject.transform.SetParent(transform, false);
            rect = iconObject.GetComponent<RectTransform>();
            warningImage = iconObject.AddComponent<Image>();
            canvasGroup = iconObject.AddComponent<CanvasGroup>();
        }

        rect.sizeDelta = Vector2.one * size;

        warningImage.sprite = sprite;
        warningImage.color = color;
        warningImage.raycastTarget = false;
        canvasGroup.alpha = 1f;
        iconCanvasGroups[rect] = canvasGroup;

        return rect;
    }

    void ReleaseIcon(RectTransform icon, Stack<RectTransform> pool)
    {
        if (icon == null)
            return;

        icon.gameObject.SetActive(false);
        pool.Push(icon);
    }

    void UpdateIcon(RectTransform icon, Vector3 worldPosition, bool shouldBlink, float currentBlinkSpeed)
    {
        Vector3 screenPos = targetCamera.WorldToScreenPoint(worldPosition);

        if (screenPos.z < 0f)
        {
            screenPos.x = Screen.width - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        Vector2 screenCenter = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 fromCenter = new Vector2(screenPos.x, screenPos.y) - screenCenter;

        if (fromCenter == Vector2.zero)
            fromCenter = Vector2.up;

        Vector2 direction = fromCenter.normalized;
        Vector2 clampedPosition = screenCenter + direction * GetEdgeDistance(direction);

        icon.position = clampedPosition;
        icon.rotation = Quaternion.identity;

        if (!iconCanvasGroups.TryGetValue(icon, out CanvasGroup group) || group == null)
            return;

        if (!shouldBlink)
        {
            group.alpha = 1f;
            return;
        }

        group.alpha = Mathf.Lerp(
            0.25f,
            1f,
            (Mathf.Sin(Time.time * currentBlinkSpeed) + 1f) * 0.5f
        );
    }

    float GetEdgeDistance(Vector2 direction)
    {
        float halfWidth = Screen.width * 0.5f - edgePadding;
        float halfHeight = Screen.height * 0.5f - edgePadding;

        float distanceToVerticalEdge = Mathf.Abs(halfWidth / direction.x);
        float distanceToHorizontalEdge = Mathf.Abs(halfHeight / direction.y);

        return Mathf.Min(distanceToVerticalEdge, distanceToHorizontalEdge);
    }

    void RemoveMissingFighterIcons(HashSet<EnemyController> activeFighters)
    {
        fighterRemoveList.Clear();

        foreach (var pair in fighterWarningIcons)
        {
            if (pair.Key == null || !activeFighters.Contains(pair.Key))
            {
                if (pair.Value != null)
                    ReleaseIcon(pair.Value, fighterIconPool);

                fighterRemoveList.Add(pair.Key);
            }
        }

        foreach (EnemyController fighter in fighterRemoveList)
        {
            fighterWarningIcons.Remove(fighter);
        }
    }

    void RemoveMissingDebrisIcons()
    {
        debrisRemoveList.Clear();

        foreach (var pair in debrisWarningIcons)
        {
            if (pair.Key == null || !trackedDebrisSet.Contains(pair.Key))
            {
                if (pair.Value != null)
                    ReleaseIcon(pair.Value, debrisIconPool);

                debrisRemoveList.Add(pair.Key);
            }
        }

        foreach (DebrisController debris in debrisRemoveList)
        {
            debrisWarningIcons.Remove(debris);
        }
    }

    void HideAllDebrisIcons()
    {
        foreach (var pair in debrisWarningIcons)
        {
            if (pair.Value != null)
                pair.Value.gameObject.SetActive(false);
        }
    }
}
