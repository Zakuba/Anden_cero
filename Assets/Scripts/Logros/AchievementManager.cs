
using UnityEngine;
using System.Collections.Generic;

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance;

    [Header("Catálogo de Logros")]
    [Tooltip("Arrastra aquí los ScriptableObjects de tus logros.")]
    [SerializeField] private List<AchievementSO> achievementsCatalog;

    [System.Serializable]
    public class AchievementVisuals
    {
        [Header("Identificación")]
        public string achievementId;

        [Header("Objetos del logro desbloqueado")]
        public GameObject btnDesbloqueado;
        public GameObject objetoDesbloqueado;

        [Header("Objetos del logro bloqueado")]
        public GameObject btnBloqueado;
        public GameObject objetoBloqueado;
    }

    [Header("Configuración visual de logros")]
    [Tooltip("Cada elemento representa un logro y sus objetos asociados.")]
    [SerializeField] private List<AchievementVisuals> achievementsVisuals;

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

    // ---------- GUARDADO ----------

    private void GuardarProgreso()
    {
        PlayerData guardado = PersistenceManager.LoadData();

        currentData.mejorTiempoSupervivencia =
            guardado.mejorTiempoSupervivencia;

        currentData.mejorOleadaSupervivencia =
            guardado.mejorOleadaSupervivencia;

        currentData.mejorTiempoParaOleada =
            guardado.mejorTiempoParaOleada;

        PersistenceManager.SaveData(currentData);
    }

    // ---------- ESTADÍSTICAS ----------

    public void AddBatStat()
    {
        currentData.bombsBatted++;

        CheckAchievement("bateador_experto", currentData.bombsBatted);

        GuardarProgreso();
    }

    // ---------- COMPROBACIÓN DE LOGROS ----------

    private void CheckAchievement(string targetId, int currentAmount)
    {
        if (currentData.unlockedAchievements.Contains(targetId))
            return;

        AchievementSO achievement =
            achievementsCatalog.Find(a => a.id == targetId);

        if (achievement != null &&
            currentAmount >= achievement.targetGoal)
        {
            UnlockAchievement(achievement);
        }
    }

    // ---------- DESBLOQUEO ----------

    private void UnlockAchievement(AchievementSO achievement)
    {
        currentData.unlockedAchievements.Add(achievement.id);

        GuardarProgreso();

        ActualizarObjetosLogro();

        if (NotificationManager.Instance != null)
        {
            NotificationManager.Instance.ShowAchievement(
                achievement.title,
                achievement.description
            );
        }
        else
        {
            Debug.LogWarning(
                "Logro obtenido: " + achievement.title +
                " (Falta NotificationManager en escena)"
            );
        }
    }

    /// <summary>
    /// Desbloquea un logro directamente mediante su ID.
    /// Ejemplo: UnlockAchievement("primera_baja");
    /// </summary>
    public void UnlockAchievement(string targetId)
    {
        if (currentData == null)
            currentData = PersistenceManager.LoadData();

        if (currentData.unlockedAchievements.Contains(targetId))
            return;

        AchievementSO achievement =
            achievementsCatalog.Find(a => a.id == targetId);

        if (achievement != null)
        {
            UnlockAchievement(achievement);
        }
        else
        {
            Debug.LogWarning(
                $"[AchievementManager] No se encontró ningún logro " +
                $"con el ID: {targetId}"
            );
        }
    }

    // ---------- ACTUALIZACIÓN VISUAL ----------

    private void ActualizarObjetosLogro()
    {
        if (currentData == null)
            return;

        foreach (AchievementVisuals visual in achievementsVisuals)
        {
            if (visual == null)
                continue;

            bool desbloqueado =
                currentData.unlockedAchievements.Contains(
                    visual.achievementId
                );

            if (visual.objetoDesbloqueado != null)
                visual.objetoDesbloqueado.SetActive(desbloqueado);

            if (visual.btnDesbloqueado != null)
                visual.btnDesbloqueado.SetActive(desbloqueado);

            if (visual.objetoBloqueado != null)
                visual.objetoBloqueado.SetActive(!desbloqueado);

            if (visual.btnBloqueado != null)
                visual.btnBloqueado.SetActive(!desbloqueado);
        }
    }

    // ---------- DEBUG ----------

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F1))
        {
            for (int i = 0; i < 50; i++)
            {
                AddBatStat();
            }

            Debug.Log(
                "Logro forzado. Total actual: " +
                currentData.bombsBatted
            );
        }
#endif
    }
}
