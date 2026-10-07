using UnityEngine;
using System.Collections.Generic;

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance;

    [Header("Catálogo de Logros")]
    [Tooltip("Arrastra aquí los ScriptableObjects de tus logros creados.")]
    [SerializeField] private List<AchievementSO> achievementsCatalog;

    private PlayerData currentData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persiste entre cambios de escena
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        currentData = PersistenceManager.LoadData();
    }

    /// <summary>
    /// Llamado localmente cada vez que el jugador dueño batea una bomba.
    /// </summary>
    public void AddBatStat()
    {
        currentData.bombsBatted++;
        CheckAchievement("bateador_experto", currentData.bombsBatted);
        
        // Guardamos el progreso incrementalmente
        PersistenceManager.SaveData(currentData);
    }

    private void CheckAchievement(string targetId, int currentAmount)
    {
        // Si ya lo tiene desbloqueado, lo ignoramos
        if (currentData.unlockedAchievements.Contains(targetId)) return;

        AchievementSO achievement = achievementsCatalog.Find(a => a.id == targetId);
        
        if (achievement != null && currentAmount >= achievement.targetGoal)
        {
            UnlockAchievement(achievement);
        }
    }

    private void UnlockAchievement(AchievementSO achievement)
    {
        currentData.unlockedAchievements.Add(achievement.id);
        PersistenceManager.SaveData(currentData);

        if (NotificationManager.Instance != null)
        {
            NotificationManager.Instance.ShowAchievement(achievement.title);
        }
        else
        {
            Debug.LogWarning("Logro obtenido: " + achievement.title + " (Falta NotificationManager en escena)");
        }
    }


    private void Update()
    {
        // Solo funciona en el Editor de Unity
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F1))
        {
            // Suma 50 bateos al instante para forzar el logro
            for (int i = 0; i < 50; i++)
            {
                AddBatStat();
            }
            Debug.Log("Logro forzado. Total actual: " + currentData.bombsBatted);
        }
#endif
    }
}