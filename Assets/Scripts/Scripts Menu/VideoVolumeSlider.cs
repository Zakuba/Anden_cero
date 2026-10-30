using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Slider de volumen propio del VideoPlayer.
/// Volumen final del video = volumen general del menú (AudioListener.volume) x este slider.
/// Requiere que el VideoPlayer tenga Audio Output Mode = Audio Source.
/// </summary>
public class VideoVolumeSlider : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource videoAudioSource;
    [SerializeField] private Slider volumeSlider;

    private void Awake()
    {
        // En modo Direct el audio ignora AudioListener.volume y este slider no haría nada.
        if (videoPlayer != null && videoPlayer.audioOutputMode != VideoAudioOutputMode.AudioSource)
        {
            Debug.LogWarning("VideoVolumeSlider: el VideoPlayer no está en Audio Output Mode = Audio Source. " +
                             "El volumen del menú y este slider no afectarán al video.");
        }

        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.value = videoAudioSource.volume;   // antes de suscribirse: no dispara el evento

        volumeSlider.onValueChanged.AddListener(SetVideoVolume);
    }

    private void OnDestroy()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(SetVideoVolume);
        }
    }

    public void SetVideoVolume(float value)
    {
        videoAudioSource.volume = value;
    }
}
