using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ApplySceneAtmosphere : MonoBehaviour
{
    private void Start()
    {
        ApplySettings();
    }

    public void ApplySettings()
    {
        if (SettingsManager.Instance == null) return;

        Volume volume = GetComponent<Volume>();
        if (volume != null && volume.profile.TryGet(out ColorAdjustments colorAdjustments))
        {
            colorAdjustments.postExposure.value = SettingsManager.Instance.currentSettings.brightness;
            colorAdjustments.contrast.value = SettingsManager.Instance.currentSettings.contrast;
        }
    }
}