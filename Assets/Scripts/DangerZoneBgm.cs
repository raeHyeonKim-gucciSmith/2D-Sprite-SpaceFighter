using UnityEngine;

public sealed class DangerZoneBgm : MonoBehaviour
{
    private const string ClipResourcePath = "Audio/DangerZone";
    private const float BgmVolume = 0.45f;
    private static DangerZoneBgm instance;

    public static void EnsurePlaying()
    {
        if (instance != null)
            return;

        instance = FindFirstObjectByType<DangerZoneBgm>();
        if (instance != null)
            return;

        GameObject bgmObject = new GameObject("DangerZone BGM");
        instance = bgmObject.AddComponent<DangerZoneBgm>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        AudioClip clip = Resources.Load<AudioClip>(ClipResourcePath);
        if (clip == null)
            return;

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = BgmVolume;
        source.Play();
    }
}
