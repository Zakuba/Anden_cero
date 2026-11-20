using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum SfxId
{
    UiClick, UiHover, UIClickTech,
    BombPlace, BombBat, Explosion,
    DroneExplosion,
    PlayerHit, PlayerDeath,
    PowerUpPickup,
    Shot
}

[System.Serializable]
public class SfxEntry
{
    public SfxId id;
    public AudioClip[] clips;                       // si hay varios, se elige uno al azar
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0f, 0.3f)] public float pitchVariation = 0.05f;
    public float minInterval = 0.05f;               // evita apilar el mismo sonido en el mismo instante
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Música")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioMixer mainAudioMixer; // reservado para ruteo/efectos futuros

    public AudioClip menuMusic;
    public AudioClip BattleRoyaleMusic;
    public AudioClip SupervivenciaMusic;
    public AudioClip PVPMusic;
    public AudioClip SalaDeEsperaMusic;
    public AudioClip SalaDeResultadoMusic;

    [Header("SFX")]
    [SerializeField] private SfxEntry[] sfxEntries;
    [SerializeField] private float sfxMinDistance = 15f; // ajustar a la altura de tu cámara cenital
    [SerializeField] private float sfxMaxDistance = 60f;

    private readonly Dictionary<SfxId, SfxEntry> sfxLookup = new Dictionary<SfxId, SfxEntry>();
    private readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var e in sfxEntries) sfxLookup[e.id] = e;
    }

    private void Start()
    {
        if (Instance != this) return;

        bool hasSettings = SettingsManager.Instance != null;
        SetMasterVolume(hasSettings ? SettingsManager.Instance.currentSettings.masterVolume : 1f);
        SetMusicMuted(hasSettings && SettingsManager.Instance.currentSettings.musicMuted);
    }

    // ---------------- Música ----------------
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource.isPlaying) musicSource.Stop();
    }

    public void PlayMenuMusic() => PlayMusic(menuMusic);
    public void PlayBattleRoyaleMusic() => PlayMusic(BattleRoyaleMusic);
    public void PlaySupervivenciaMusic() => PlayMusic(SupervivenciaMusic);
    public void PlayPVPMusic() => PlayMusic(PVPMusic);
    public void PlaySalaDeEsperaMusic() => PlayMusic(SalaDeEsperaMusic);
    public void PlaySalaDeResultadoMusic() => PlayMusic(SalaDeResultadoMusic);

    // ---------------- Volumen / mute ----------------
    public void SetMasterVolume(float value) => AudioListener.volume = Mathf.Clamp01(value);

    // "mute" deja la fuente reproduciendo: al reactivar, la música sigue donde iba
    public void SetMusicMuted(bool muted) => musicSource.mute = muted;

    // ---------------- SFX ----------------
    public void PlaySfx(SfxId id) => PlaySfxInternal(id, Vector3.zero, false);                 // UI, 2D
    public void PlaySfxAt(SfxId id, Vector3 position) => PlaySfxInternal(id, position, true);  // mundo, 3D

    private void PlaySfxInternal(SfxId id, Vector3 position, bool spatial)
    {
        if (!sfxLookup.TryGetValue(id, out var entry) || entry.clips == null || entry.clips.Length == 0) return;

        if (lastPlayed.TryGetValue(id, out float last) && Time.unscaledTime - last < entry.minInterval) return;
        lastPlayed[id] = Time.unscaledTime;

        AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
        if (clip == null) return;

        var go = new GameObject("SFX_" + id);
        go.transform.position = position;

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = entry.volume;
        src.pitch = 1f + Random.Range(-entry.pitchVariation, entry.pitchVariation);
        src.spatialBlend = spatial ? 1f : 0f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = sfxMinDistance;
        src.maxDistance = sfxMaxDistance;
        src.Play();

        Destroy(go, clip.length / src.pitch + 0.1f);
    }
}