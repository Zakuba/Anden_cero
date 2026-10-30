using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsUI : MonoBehaviour
{
    [Header("Referencias de UI - Audio")]
    [SerializeField] private Slider volumeSlider;

    // --- Respaldo para el botón Volver / Cancelar ---
    private float originalVolume = 1.0f;
    private bool isDataSaved = false;

    private void Awake()
    {
        SetupAudioSlider();
    }

    private void OnEnable()
    {
        CaptureOriginalSettings();
    }

    /// <summary>
    /// Guarda el estado actual del volumen al abrir el menú para poder restaurarlo si se cancela.
    /// </summary>
    private void CaptureOriginalSettings()
    {
        isDataSaved = false;
        originalVolume = AudioListener.volume;
    }

    /// <summary>
    /// Configura el slider con el valor del SettingsManager o del AudioListener actual.
    /// </summary>
    private void SetupAudioSlider()
    {
        if (volumeSlider == null) return;

        // Cargar valor desde el Singleton si existe, o usar el actual del AudioListener
        float initialVolume = (SettingsManager.Instance != null)
            ? SettingsManager.Instance.currentSettings.masterVolume
            : AudioListener.volume;

        volumeSlider.value = initialVolume;
        SetMasterVolume(initialVolume);

        // Escuchar los cambios del Slider
        volumeSlider.onValueChanged.AddListener(SetMasterVolume);
    }

    /// <summary>
    /// Modifica el volumen global del juego y lo actualiza en los ajustes en memoria.
    /// </summary>
    public void SetMasterVolume(float value)
    {
        // Aplica el volumen a todo el motor de audio de Unity
        AudioListener.volume = value;

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.currentSettings.masterVolume = value;
        }
    }

    /// <summary>
    /// Debe llamarse desde el botón "Guardar" del menú de opciones.
    /// </summary>
    public void SaveData()
    {
        isDataSaved = true;
        
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SaveSettings();
        }
    }

    /// <summary>
    /// Debe llamarse desde el botón "Volver" o "Cancelar" del menú.
    /// </summary>
    public void VolverAlMenuSinGuardar()
    {
        if (!isDataSaved)
        {
            RevertirCambios();
        }
    }

    /// <summary>
    /// Restaura el volumen que estaba presente al abrir la pantalla de opciones.
    /// </summary>
    private void RevertirCambios()
    {
        SetMasterVolume(originalVolume);

        if (volumeSlider != null)
        {
            volumeSlider.value = originalVolume;
        }

        // Recarga el estado guardado en disco para descartar cambios en memoria
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.LoadSettings();
        }
    }
}