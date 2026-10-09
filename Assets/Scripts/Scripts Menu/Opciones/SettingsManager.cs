using UnityEngine;
using System.IO;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;
    public GameSettings currentSettings;
    
    private string savePath;

    private void Awake()
    {
        // Patrón Singleton para mantener el gestor entre escenas
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            savePath = Application.persistentDataPath + "/settings.json";
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadSettings()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            currentSettings = JsonUtility.FromJson<GameSettings>(json);
            Debug.Log("Opciones cargadas desde: " + savePath);
        }
        else
        {
            currentSettings = new GameSettings();
            Debug.Log("No se encontraron opciones. Cargando valores por defecto.");
        }
    }

    public void SaveSettings()
    {
        string json = JsonUtility.ToJson(currentSettings, true);
        File.WriteAllText(savePath, json);
        Debug.Log("Opciones guardadas en: " + savePath);
    }
}
