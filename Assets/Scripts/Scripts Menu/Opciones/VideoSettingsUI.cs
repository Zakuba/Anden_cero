using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using UnityEngine.UI;

public class VideoSettingsUI : MonoBehaviour
{
    [Header("Referencias de UI - Vídeo")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown qualityDropdown;

    [Header("Referencias de UI - Atmósfera")]
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private Slider contrastSlider;

    [Header("Referencias de UI - Audio")]
    [SerializeField] private Slider masterVolumeSlider;

    [Header("Post-Processing (URP)")]
    [SerializeField] private Volume globalVolume;
    private ColorAdjustments colorAdjustments;

    [Header("Referencias de UI - Rendimiento y Red")]
    [SerializeField] private Toggle fpsToggle;
    [SerializeField] private Toggle pingToggle;

    private Resolution[] availableResolutions;
    private List<Resolution> filteredResolutions;
    private int currentResolutionIndex = 0;

    // --- Respaldo: ÚLTIMO ESTADO GUARDADO (es lo que restaura el botón Volver) ---
    private int savedResolutionIndex;
    private bool savedFullscreen;
    private int savedQualityIndex;
    private float savedBrightness;
    private float savedContrast;
    private float savedMasterVolume;
    private bool savedShowFPS;
    private bool savedShowPing;

    private void Awake()
    {
        SetupFullscreenToggle();
        SetupResolutionDropdown();
        SetupQualityDropdown();
        SetupPostProcessingSliders();
        SetupPerformanceToggles();
        SetupAudioSlider();

        // Respaldo inicial: lo que hay al arrancar cuenta como "último estado guardado".
        CaptureSavedState();
    }

    // ------------------------------------------------------------------
    //  GUARDAR / VOLVER
    // ------------------------------------------------------------------

    /// <summary>Se conecta al botón Guardar.</summary>
    public void saveData()
    {
        // Lo que se ve ahora pasa a ser el nuevo "último estado guardado".
        CaptureSavedState();

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SaveSettings();
        }
    }

    /// <summary>Se conecta al botón Volver. Descarta todo lo no guardado.</summary>
    public void volverAlMenuSinGuardar()
    {
        RestoreSavedState();
    }

    // Se lee de los CONTROLES de la UI (no de Screen.fullScreen / QualitySettings),
    // porque el estado del motor puede aplicarse con retraso o directamente no
    // cambiar en el Editor.
    private void CaptureSavedState()
    {
        savedFullscreen = fullscreenToggle.isOn;
        savedResolutionIndex = resolutionDropdown.value;
        savedQualityIndex = qualityDropdown.value;
        savedBrightness = brightnessSlider.value;
        savedContrast = contrastSlider.value;
        savedMasterVolume = masterVolumeSlider.value;
        savedShowFPS = fpsToggle.isOn;
        savedShowPing = pingToggle.isOn;
    }

    private void RestoreSavedState()
    {
        SettingsManager settings = SettingsManager.Instance;

        // 1. Recargar el JSON primero (cubre campos que este script no maneja,
        //    como inputRebinds). Los Set... de abajo reescriben después en
        //    currentSettings los valores del respaldo, así queda todo coherente.
        if (settings != null)
        {
            settings.LoadSettings();
        }

        // 2. Calidad
        qualityDropdown.SetValueWithoutNotify(savedQualityIndex);
        qualityDropdown.RefreshShownValue();
        SetQuality(savedQualityIndex);

        // 3. Pantalla completa y resolución
        fullscreenToggle.SetIsOnWithoutNotify(savedFullscreen);
        resolutionDropdown.SetValueWithoutNotify(savedResolutionIndex);
        resolutionDropdown.RefreshShownValue();

        currentResolutionIndex = savedResolutionIndex;
        Resolution res = filteredResolutions[savedResolutionIndex];
        Screen.SetResolution(res.width, res.height, savedFullscreen);

        if (settings != null)
        {
            settings.currentSettings.fullScreen = savedFullscreen;
            settings.currentSettings.resolutionIndex = savedResolutionIndex;
        }

        // 4. Atmósfera (brillo y contraste)
        brightnessSlider.SetValueWithoutNotify(savedBrightness);
        contrastSlider.SetValueWithoutNotify(savedContrast);
        SetBrightness(savedBrightness);
        SetContrast(savedContrast);

        // 5. Volumen
        masterVolumeSlider.SetValueWithoutNotify(savedMasterVolume);
        SetMasterVolume(savedMasterVolume);

        // 6. Rendimiento y red
        fpsToggle.SetIsOnWithoutNotify(savedShowFPS);
        pingToggle.SetIsOnWithoutNotify(savedShowPing);
        SetShowFPS(savedShowFPS);
        SetShowPing(savedShowPing);
    }

    // ------------------------------------------------------------------
    //  ATMÓSFERA (brillo / contraste)
    // ------------------------------------------------------------------

    private void SetupPostProcessingSliders()
    {
        if (globalVolume != null && globalVolume.profile.TryGet(out colorAdjustments))
        {
            // Cargar valor guardado o valor actual del profile
            float initialBrightness = (SettingsManager.Instance != null)
                ? SettingsManager.Instance.currentSettings.brightness
                : colorAdjustments.postExposure.value;

            float initialContrast = (SettingsManager.Instance != null)
                ? SettingsManager.Instance.currentSettings.contrast
                : colorAdjustments.contrast.value;

            brightnessSlider.value = initialBrightness;
            contrastSlider.value = initialContrast;

            SetBrightness(initialBrightness);
            SetContrast(initialContrast);

            brightnessSlider.onValueChanged.AddListener(SetBrightness);
            contrastSlider.onValueChanged.AddListener(SetContrast);
        }
    }

    public void SetBrightness(float value)
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = value;
        }

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.currentSettings.brightness = value;
        }
    }

    public void SetContrast(float value)
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.contrast.value = value;
        }

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.currentSettings.contrast = value;
        }
    }

    // ------------------------------------------------------------------
    //  AUDIO
    // ------------------------------------------------------------------

    private void SetupAudioSlider()
    {
        float initialVolume = (SettingsManager.Instance != null)
            ? SettingsManager.Instance.currentSettings.masterVolume
            : AudioListener.volume;

        masterVolumeSlider.minValue = 0f;
        masterVolumeSlider.maxValue = 1f;
        masterVolumeSlider.value = initialVolume;

        masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
    }

    public void SetMasterVolume(float value)
    {
        AudioListener.volume = value;

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.currentSettings.masterVolume = value;
        }
    }

    // ------------------------------------------------------------------
    //  CALIDAD
    // ------------------------------------------------------------------

    private void SetupQualityDropdown()
    {
        qualityDropdown.ClearOptions();
        string[] qualityNames = QualitySettings.names;
        List<string> options = new List<string>(qualityNames);
        qualityDropdown.AddOptions(options);

        int currentQuality = QualitySettings.GetQualityLevel();
        if (SettingsManager.Instance != null && SettingsManager.Instance.currentSettings.qualityLevel >= 0)
        {
            currentQuality = SettingsManager.Instance.currentSettings.qualityLevel;
            QualitySettings.SetQualityLevel(currentQuality, true);
        }

        qualityDropdown.value = currentQuality;
        qualityDropdown.RefreshShownValue();
        qualityDropdown.onValueChanged.AddListener(SetQuality);
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex, true);
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.currentSettings.qualityLevel = qualityIndex;
        }
    }

    // ------------------------------------------------------------------
    //  PANTALLA COMPLETA Y RESOLUCIÓN
    // ------------------------------------------------------------------

    private void SetupFullscreenToggle()
    {
        fullscreenToggle.isOn = Screen.fullScreen;
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
    }

    private void SetupResolutionDropdown()
    {
        availableResolutions = Screen.resolutions;
        filteredResolutions = new List<Resolution>();
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();

        for (int i = 0; i < availableResolutions.Length; i++)
        {
            Resolution res = availableResolutions[i];
            bool alreadyExists = filteredResolutions.Exists(r => r.width == res.width && r.height == res.height);

            if (!alreadyExists)
            {
                filteredResolutions.Add(res);
                options.Add($"{res.width} x {res.height}");

                if (res.width == Screen.currentResolution.width && res.height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = filteredResolutions.Count - 1;
                }
            }
        }

        resolutionDropdown.AddOptions(options);

        if (SettingsManager.Instance != null && SettingsManager.Instance.currentSettings.resolutionIndex >= 0)
        {
            currentResolutionIndex = SettingsManager.Instance.currentSettings.resolutionIndex;
        }

        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    public void SetResolution(int index)
    {
        currentResolutionIndex = index;
        Resolution selectedRes = filteredResolutions[index];
        Screen.SetResolution(selectedRes.width, selectedRes.height, Screen.fullScreen);

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.currentSettings.resolutionIndex = index;
        }
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.currentSettings.fullScreen = isFullscreen;
        }
    }

    // ------------------------------------------------------------------
    //  RENDIMIENTO Y RED
    // ------------------------------------------------------------------

    private void SetupPerformanceToggles()
    {
        if (SettingsManager.Instance != null)
        {
            fpsToggle.isOn = SettingsManager.Instance.currentSettings.showFPS;
            pingToggle.isOn = SettingsManager.Instance.currentSettings.showPing;
        }

        fpsToggle.onValueChanged.AddListener(SetShowFPS);
        pingToggle.onValueChanged.AddListener(SetShowPing);
    }

    public void SetShowFPS(bool show)
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.currentSettings.showFPS = show;
    }

    public void SetShowPing(bool show)
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.currentSettings.showPing = show;
    }
}