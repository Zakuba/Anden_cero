using UnityEngine;
using System.Collections.Generic;

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance;

    [Header("Catálogo de Logros")]
    [Tooltip("Arrastra aquí los ScriptableObjects de tus logros creados.")]
    [SerializeField] private List<AchievementSO> achievementsCatalog;

    [Header("Objetos asociados al logro")]
    [SerializeField] private string achievementId = "bateador_experto";
    [SerializeField] private GameObject btnDesbloqueado;
    [SerializeField] private GameObject objetoDesbloqueado;
    [SerializeField] private GameObject btnBloqueado;
    [SerializeField] private GameObject objetoBloqueado;

    private PlayerData currentData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        currentData = PersistenceManager.LoadData();

        ActualizarObjetosLogro();
    }
    // El récord de supervivencia lo guarda MatchManager por su cuenta.
    // Lo releemos antes de guardar para no pisarlo con una copia vieja.
    private void GuardarProgreso()
    {
        // Recargar el récord desde el disco para no pisarlo con datos viejos
        PlayerData guardado = PersistenceManager.LoadData();

        currentData.mejorTiempoSupervivencia = guardado.mejorTiempoSupervivencia;
        currentData.mejorOleadaSupervivencia = guardado.mejorOleadaSupervivencia;
        currentData.mejorTiempoParaOleada = guardado.mejorTiempoParaOleada;

        PersistenceManager.SaveData(currentData);
    }

    /// <summary>
    /// Llamado localmente cada vez que el jugador dueño batea una bomba.
    /// </summary>
    public void AddBatStat()
    {
        currentData.bombsBatted++;
        CheckAchievement("bateador_experto", currentData.bombsBatted);
        
        // Guardamos el progreso incrementalmente
        GuardarProgreso();
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
        GuardarProgreso();

        ActualizarObjetosLogro();

        if (NotificationManager.Instance != null)
        {
            // Pasamos título y descripción
            NotificationManager.Instance.ShowAchievement(achievement.title, achievement.description);
        }
        else
        {
            Debug.LogWarning("Logro obtenido: " + achievement.title + " (Falta NotificationManager en escena)");
        }
    }

/// <summary>
/// Método público para desbloquear logros usando directamente su ID (ej: "primera_baja").
/// </summary>
    public void UnlockAchievement(string targetId)
    {
        if (currentData == null)
            currentData = PersistenceManager.LoadData();

        // Si ya está desbloqueado, lo descartamos
        if (currentData.unlockedAchievements.Contains(targetId))
            return;

        // Busca el ScriptableObject en la lista que tenés en el Inspector
        AchievementSO achievement = achievementsCatalog.Find(a => a.id == targetId);

        if (achievement != null)
        {
            // Llama a tu método privado pasando el ScriptableObject
            UnlockAchievement(achievement);
        }
        else
        {
            Debug.LogWarning($"[AchievementManager] No se encontró ningún logro en el catálogo con el id: {targetId}");
        }
    }

private void ActualizarObjetosLogro()
{
    if (currentData == null) return;

    bool desbloqueado = currentData.unlockedAchievements.Contains(achievementId);

    if (objetoDesbloqueado != null)
    {
        objetoDesbloqueado.SetActive(desbloqueado);
    }

    if (btnDesbloqueado != null)
    {
        btnDesbloqueado.SetActive(desbloqueado);
    }

    if (objetoBloqueado != null)
    {
        objetoBloqueado.SetActive(!desbloqueado);
    }

    if (btnBloqueado != null)
    {
        btnBloqueado.SetActive(!desbloqueado);
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