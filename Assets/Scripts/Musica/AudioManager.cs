using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Componentes")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioMixer mainAudioMixer; // <--- Referencia al Mixer

    [Header("Clips de Música")]
    public AudioClip menuMusic;
    public AudioClip BattleRoyaleMusic;
    public AudioClip SupervivenciaMusic;
    public AudioClip PVPMusic;
    public AudioClip SalaDeEsperaMusic;
    public AudioClip SalaDeResultadoMusic;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            PlayMenuMusic();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource.isPlaying)
        {
            musicSource.Stop();
        }
    }

    public void PlayMenuMusic() => PlayMusic(menuMusic);
    public void PlayBattleRoyaleMusic() => PlayMusic(BattleRoyaleMusic);
    public void PlaySupervivenciaMusic() => PlayMusic(SupervivenciaMusic);
    public void PlayPVPMusic() => PlayMusic(PVPMusic);
    public void PlaySalaDeEsperaMusic() => PlayMusic(SalaDeEsperaMusic);
    public void PlaySalaDeResultadoMusic() => PlayMusic(SalaDeResultadoMusic);

    // --- NUEVO: Control de Volumen con el Audio Mixer ---
    public void SetMusicVolume(float sliderValue)
    {
        // Los sliders van de 0.0001 a 1 (escala lineal). 
        // El Audio Mixer usa Decibeles (-80dB a 20dB). Usamos Log10 para que la curva de volumen sea natural.
        float volumeInDb = Mathf.Log10(Mathf.Clamp(sliderValue, 0.0001f, 1f)) * 20;
        mainAudioMixer.SetFloat("MusicVolume", volumeInDb);

        // Opcional: Guardar en PlayerPrefs para que recuerde la configuración
        PlayerPrefs.SetFloat("MusicVolumePref", sliderValue);
    }
}