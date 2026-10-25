using UnityEngine;

/// <summary>
/// Perfil de dificultad del modo Supervivencia. Hay un asset por dificultad
/// (Fácil, Normal y Difícil). Todo lo que cambia entre dificultades vive aquí,
/// así que se puede ajustar jugando sin tocar código.
///
/// Cómo crear los assets: clic derecho en Project > Create > Anden Cero >
/// Perfil de dificultad (Supervivencia). Luego, clic derecho sobre el componente
/// en el Inspector > "Cargar valores sugeridos: ..." para llenarlo.
///
/// La velocidad es FIJA: no cambia con la oleada.
/// </summary>
[CreateAssetMenu(
    fileName = "Dificultad_Normal",
    menuName = "Anden Cero/Perfil de dificultad (Supervivencia)")]
public class SurvivalDifficultyProfile : ScriptableObject
{
    [Header("Identidad")]
    public DifficultyLevel level = DifficultyLevel.Normal;
    public string displayName = "Normal";

    [Header("Presión de las oleadas")]
    [Tooltip("Drones de la oleada 1.")]
    public int baseDronesPerWave = 3;
    [Tooltip("Drones que se suman en cada oleada.")]
    public int dronesPerWaveIncrement = 2;
    [Tooltip("Tope de drones vivos a la vez = base + porJugadorVivo * jugadoresVivos.")]
    public int maxAliveBase = 4;
    public int maxAlivePerPlayer = 2;
    [Tooltip("Segundos entre una aparición y la siguiente.")]
    public float spawnInterval = 0.6f;
    [Tooltip("Segundos de descanso entre oleadas.")]
    public float restDuration = 8f;

    [Header("Composición (proporción de Fusileros; el resto son Bombarderos)")]
    [Range(0f, 1f)] public float shooterRatioStart = 0.34f;
    [Range(0f, 1f)] public float shooterRatioStep = 0.03f;
    [Range(0f, 1f)] public float shooterRatioMax = 0.6f;

    [Header("Dron Sanador (0 = nunca)")]
    public int healerFirstWave = 2;
    public int healerEveryNWaves = 3;

    [Header("Jugadores")]
    [Tooltip("Vidas máximas de cada jugador en esta dificultad (también es el tope al curarse).")]
    public int playerMaxLives = 3;

    [Header("Drones: velocidad fija (1 = la del prefab)")]
    public float droneSpeedMultiplier = 1f;

    [Header("Fusilero")]
    [Tooltip("Segundos entre disparos.")]
    public float shooterFireRate = 1f;
    [Tooltip("Radio de visión.")]
    public float shooterSightRange = 12f;
    [Tooltip("Segundos entre búsquedas de jugadores (tiempo de reacción).")]
    public float shooterScanInterval = 0.15f;

    [Header("Bombardero")]
    [Tooltip("Segundos entre bombas.")]
    public float bomberDropInterval = 4f;
    [Tooltip("Segundos de mecha de cada bomba.")]
    public float bombFuseTime = 3f;

    /// <summary>Lo que necesitan los drones al aparecer.</summary>
    public DroneDifficultyParams ToDroneParams()
    {
        return new DroneDifficultyParams(
            droneSpeedMultiplier,
            shooterFireRate, shooterSightRange, shooterScanInterval,
            bomberDropInterval, bombFuseTime);
    }

    /// <summary>
    /// Carga los valores sugeridos de una dificultad. Normal reproduce el comportamiento
    /// anterior (sin el aumento de velocidad por oleada).
    /// </summary>
    public void ApplyPreset(DifficultyLevel preset)
    {
        level = preset;

        switch (preset)
        {
            case DifficultyLevel.Easy:
                displayName = "Fácil";
                baseDronesPerWave = 2;
                dronesPerWaveIncrement = 1;
                maxAliveBase = 3;
                maxAlivePerPlayer = 1;
                spawnInterval = 1.0f;
                restDuration = 12f;
                shooterRatioStart = 0.25f;
                shooterRatioStep = 0.02f;
                shooterRatioMax = 0.40f;
                healerFirstWave = 2;
                healerEveryNWaves = 2;
                droneSpeedMultiplier = 0.8f;
                shooterFireRate = 1.5f;
                shooterSightRange = 9f;
                shooterScanInterval = 0.3f;
                bomberDropInterval = 5f;
                bombFuseTime = 3.5f;
                playerMaxLives = 4;
                break;

            case DifficultyLevel.Hard:
                displayName = "Difícil";
                baseDronesPerWave = 4;
                dronesPerWaveIncrement = 3;
                maxAliveBase = 8;
                maxAlivePerPlayer = 3;
                spawnInterval = 0.4f;
                restDuration = 5f;
                shooterRatioStart = 0.40f;
                shooterRatioStep = 0.04f;
                shooterRatioMax = 0.65f;
                healerFirstWave = 3;
                healerEveryNWaves = 4;
                droneSpeedMultiplier = 1.2f;
                shooterFireRate = 0.7f;
                shooterSightRange = 14f;
                shooterScanInterval = 0.1f;
                bomberDropInterval = 3f;
                bombFuseTime = 2.5f;
                playerMaxLives = 2;
                break;

            default: // Normal
                level = DifficultyLevel.Normal;
                displayName = "Normal";
                baseDronesPerWave = 3;
                dronesPerWaveIncrement = 2;
                maxAliveBase = 6;
                maxAlivePerPlayer = 2;
                spawnInterval = 0.6f;
                restDuration = 8f;
                shooterRatioStart = 0.34f;
                shooterRatioStep = 0.03f;
                shooterRatioMax = 0.60f;
                healerFirstWave = 2;
                healerEveryNWaves = 3;
                droneSpeedMultiplier = 1f;
                shooterFireRate = 1f;
                shooterSightRange = 12f;
                shooterScanInterval = 0.15f;
                bomberDropInterval = 4f;
                bombFuseTime = 3f;
                playerMaxLives = 3;
                break;
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Cargar valores sugeridos: Fácil")]
    private void LoadEasy() { LoadFromMenu(DifficultyLevel.Easy); }

    [ContextMenu("Cargar valores sugeridos: Normal")]
    private void LoadNormal() { LoadFromMenu(DifficultyLevel.Normal); }

    [ContextMenu("Cargar valores sugeridos: Difícil")]
    private void LoadHard() { LoadFromMenu(DifficultyLevel.Hard); }

    private void LoadFromMenu(DifficultyLevel preset)
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Cargar valores de dificultad");
#endif
        ApplyPreset(preset);
    }

    // Evita valores que romperían el juego (por ejemplo una cadencia de 0 dispararía en cada frame).
    private void OnValidate()
    {
        baseDronesPerWave = Mathf.Max(1, baseDronesPerWave);
        dronesPerWaveIncrement = Mathf.Max(0, dronesPerWaveIncrement);
        maxAliveBase = Mathf.Max(1, maxAliveBase);
        maxAlivePerPlayer = Mathf.Max(0, maxAlivePerPlayer);
        spawnInterval = Mathf.Max(0.1f, spawnInterval);
        restDuration = Mathf.Max(1f, restDuration);

        shooterRatioMax = Mathf.Max(shooterRatioStart, shooterRatioMax);

        healerFirstWave = Mathf.Max(0, healerFirstWave);
        healerEveryNWaves = Mathf.Max(0, healerEveryNWaves);

        droneSpeedMultiplier = Mathf.Clamp(droneSpeedMultiplier, 0.3f, 3f);

        shooterFireRate = Mathf.Max(0.1f, shooterFireRate);
        shooterSightRange = Mathf.Max(1f, shooterSightRange);
        shooterScanInterval = Mathf.Max(0.02f, shooterScanInterval);

        bomberDropInterval = Mathf.Max(0.5f, bomberDropInterval);
        bombFuseTime = Mathf.Max(0.5f, bombFuseTime);

        playerMaxLives = Mathf.Clamp(playerMaxLives, 1, 6);
    }
}