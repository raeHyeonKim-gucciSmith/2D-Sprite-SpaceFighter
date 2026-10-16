using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum LockState
{
    None,
    Unlock,
    LockOn,
    Fire
}

public class LockOnUIManager : MonoBehaviour
{
    public static LockOnUIManager Instance;

    [Header("References")]
    public Camera mainCamera;
    public Canvas canvas;
    public Image lockIconPrefab;

    [Header("Sprites")]
    public Sprite unlockSprite;
    public Sprite lockonSprite;
    public Sprite fireSprite;

    [Header("UI Settings")]
    public Vector2 screenOffset = new Vector2(0f, 20f);
    public float iconSize = 48f;

    [Header("Lock Sounds")]
    public AudioClip unlockSound;
    public AudioClip lockOnSound;
    public float soundVolume = 0.6f;
    public float spatialBlend = 1f;
    public float minDistance = 1f;
    public float maxDistance = 12f;

    private bool lockSoundsEnabled = true;

    private Dictionary<EnemyController, Image> activeIcons = new Dictionary<EnemyController, Image>();
    private Dictionary<EnemyController, LockState> currentStates = new Dictionary<EnemyController, LockState>();
    private Dictionary<EnemyController, AudioSource> activeSounds = new Dictionary<EnemyController, AudioSource>();
    private readonly List<EnemyController> removeList = new List<EnemyController>();
    private readonly Stack<Image> iconPool = new Stack<Image>();
    private readonly Stack<AudioSource> soundPool = new Stack<AudioSource>();
    private RectTransform canvasRect;

    void Awake()
    {
        Instance = this;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
            canvasRect = canvas.GetComponent<RectTransform>();

#if !UNITY_WEBGL
        if (unlockSound != null)
            unlockSound.LoadAudioData();
        if (lockOnSound != null)
            lockOnSound.LoadAudioData();
#endif
    }

    public void ShowIcon(EnemyController enemy, LockState state)
    {
        if (enemy == null || enemy.IsDestroyed) return;

        Image icon = GetOrCreateIcon(enemy);

        switch (state)
        {
            case LockState.Unlock:
                icon.sprite = unlockSprite;
                break;

            case LockState.LockOn:
                icon.sprite = lockonSprite;
                break;

            case LockState.Fire:
                icon.sprite = fireSprite;
                break;
        }

        icon.enabled = true;
        icon.rectTransform.sizeDelta = Vector2.one * iconSize;

        currentStates[enemy] = state;

        UpdateIconPosition(enemy, icon);
    }

    public void HideIcon(EnemyController enemy)
    {
        if (enemy == null) return;

        if (activeIcons.TryGetValue(enemy, out Image icon))
            icon.enabled = false;

        StopLockSound(enemy);
    }

    public void HideAll()
    {
        foreach (var pair in activeIcons)
        {
            if (pair.Value != null)
                pair.Value.enabled = false;
        }
    }

    public void StopAllLockSounds()
    {
        lockSoundsEnabled = false;

        foreach (var pair in activeSounds)
        {
            AudioSource source = pair.Value;

            if (source != null)
                ReleaseAudioSource(source);
        }

        activeSounds.Clear();

        foreach (var pair in activeIcons)
        {
            if (pair.Value != null)
                pair.Value.enabled = false;
        }

        currentStates.Clear();
    }

    void LateUpdate()
    {
        removeList.Clear();

        foreach (var pair in activeIcons)
        {
            EnemyController enemy = pair.Key;
            Image icon = pair.Value;

            if (enemy == null || enemy.IsDestroyed)
            {
                if (icon != null)
                    ReleaseIcon(icon);

                DestroyLockSound(enemy);
                removeList.Add(enemy);
                continue;
            }

            if (icon != null && icon.enabled)
            {
                UpdateIconPosition(enemy, icon);

                LockState state = currentStates.TryGetValue(enemy, out LockState currentState)
                    ? currentState
                    : LockState.None;

                UpdateLockSound(enemy, state);
            }
            else
            {
                StopLockSound(enemy);
            }
        }

        foreach (EnemyController enemy in removeList)
        {
            activeIcons.Remove(enemy);
            currentStates.Remove(enemy);
        }
    }

    Image GetOrCreateIcon(EnemyController enemy)
    {
        if (activeIcons.TryGetValue(enemy, out Image icon))
            return icon;

        Transform parent = canvas != null ? canvas.transform : transform;

        Image newIcon;
        if (iconPool.Count > 0)
        {
            newIcon = iconPool.Pop();
            newIcon.transform.SetParent(parent, false);
            newIcon.gameObject.SetActive(true);
        }
        else
        {
            newIcon = Instantiate(lockIconPrefab, parent);
        }

        newIcon.enabled = false;

        RectTransform rect = newIcon.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = Vector2.zero;

        activeIcons.Add(enemy, newIcon);

        return newIcon;
    }

    void ReleaseIcon(Image icon)
    {
        if (icon == null)
            return;

        icon.enabled = false;
        icon.gameObject.SetActive(false);
        iconPool.Push(icon);
    }
    void UpdateIconPosition(EnemyController enemy, Image icon)
    {
        Vector3 screenPos = mainCamera.WorldToScreenPoint(enemy.transform.position);

        if (screenPos.z < 0)
        {
            icon.enabled = false;
            return;
        }

        if (canvasRect == null)
            return;

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            uiCamera,
            out Vector2 localPoint
        );

        icon.rectTransform.anchoredPosition = localPoint + screenOffset;
    }

    void UpdateLockSound(EnemyController enemy, LockState state)
    {
        if (!lockSoundsEnabled)
            return;

        AudioClip clip = null;

        if (state == LockState.Unlock)
            clip = unlockSound;
        else if (state == LockState.LockOn)
            clip = lockOnSound;

        if (clip == null)
        {
            StopLockSound(enemy);
            return;
        }

        AudioSource source = GetOrCreateAudioSource(enemy);

        source.transform.position = enemy.transform.position;

        if (source.clip != clip)
        {
            source.clip = clip;
            source.Play();
        }
        else if (!source.isPlaying)
        {
            source.Play();
        }
    }

    AudioSource GetOrCreateAudioSource(EnemyController enemy)
    {
        if (activeSounds.TryGetValue(enemy, out AudioSource source) && source != null)
            return source;

        if (soundPool.Count > 0)
        {
            source = soundPool.Pop();
            source.gameObject.SetActive(true);
        }
        else
        {
            GameObject soundObject = new GameObject("LockOnSound");
            soundObject.transform.SetParent(transform);
            source = soundObject.AddComponent<AudioSource>();
        }

        source.transform.SetParent(transform);
        source.transform.position = enemy.transform.position;
        source.playOnAwake = false;
        source.loop = true;
        source.volume = soundVolume;
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;

        activeSounds[enemy] = source;

        return source;
    }

    void StopLockSound(EnemyController enemy)
    {
        if (!activeSounds.TryGetValue(enemy, out AudioSource source))
            return;

        if (source != null)
        {
            source.Stop();
            source.clip = null;
        }
    }

    void DestroyLockSound(EnemyController enemy)
    {
        if (!activeSounds.TryGetValue(enemy, out AudioSource source))
            return;

        if (source != null)
            ReleaseAudioSource(source);

        activeSounds.Remove(enemy);
    }

    void ReleaseAudioSource(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
        source.gameObject.SetActive(false);
        soundPool.Push(source);
    }
}
