using UnityEngine;
using System.IO;

public static class PersistenceManager
{
    private static string GetFilePath()
    {
        return Path.Combine(Application.persistentDataPath, "player_profile.json");
    }

    public static void SaveData(PlayerData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetFilePath(), json);
        Debug.Log("Datos guardados en: " + GetFilePath());
    }

    public static PlayerData LoadData()
    {
        string path = GetFilePath();
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<PlayerData>(json);
        }
        
        return new PlayerData(); // Retorna un perfil limpio si es la primera vez
    }
}
