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

    [Header("Post-Processing (URP)")]
    [SerializeField] private Volume globalVolume;
    private ColorAdjustments colorAdjustments;

    [Header("Referencias de UI - Rendimiento y Red")]
    [SerializeField] private Toggle fpsToggle;
    [SerializeField] private Toggle pingToggle;


    private Resolution[] availableResolutions;
    private List<Resolution> filteredResolutions;
    private int currentResolutionIndex = 0;

    // --- Respaldo para el botón Volver ---
    private bool dataSave = false;
    private int originalResolutionIndex;
    private bool originalFullscreen;
    private int originalQualityIndex;
    private float originalBrightness;
    private float originalContrast;

    private void Awake()
    {
        SetupFullscreenToggle();
        SetupResolutionDropdown();
        SetupQualityDropdown();
        SetupPostProcessingSliders();
        SetupPerformanceToggles();
    }

    private void OnEnable()
    {
        CaptureOriginalSettings();
    }

    private void CaptureOriginalSettings()
    {
        originalFullscreen = Screen.fullScreen;
        originalQualityIndex = QualitySettings.GetQualityLevel();
        originalResolutionIndex = currentResolutionIndex;

        if (colorAdjustments != null)
        {
            originalBrightness = colorAdjustments.postExposure.value;
            originalContrast = colorAdjustments.contrast.value;
        }
    }

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

    public void saveData(){
        this.dataSave = true;
    }

    public void volverAlMenuSinGuardar(){
        if(!this.dataSave){
            Debug.Log("asodasd" + this.dataSave);
            VolverSinGuardar();
        } else{
            this.dataSave = false;
        }
    }

    private void VolverSinGuardar()
    {
        // 1. Restaurar Gráficos y Pantalla
        SetQuality(originalQualityIndex);
        qualityDropdown.value = originalQualityIndex;
        qualityDropdown.RefreshShownValue();

        fullscreenToggle.isOn = originalFullscreen;
        resolutionDropdown.value = originalResolutionIndex;
        resolutionDropdown.RefreshShownValue();
        
        Resolution originalRes = filteredResolutions[originalResolutionIndex];
        Screen.SetResolution(originalRes.width, originalRes.height, originalFullscreen);

        // 2. Restaurar Atmósfera (Brillo y Contraste)
        if (colorAdjustments != null)
        {
            brightnessSlider.value = originalBrightness;
            contrastSlider.value = originalContrast;
            SetBrightness(originalBrightness);
            SetContrast(originalContrast);
        }

        // 3. Recargar JSON en memoria
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.LoadSettings();
        }
    }

    // --- Métodos de resolución y calidad ya existentes ---
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