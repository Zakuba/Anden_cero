/// <summary>
/// Dificultad del modo Supervivencia. Es un byte para poder replicarla
/// con una NetworkVariable sin costo.
/// </summary>
public enum DifficultyLevel : byte
{
    Easy = 0,
    Normal = 1,
    Hard = 2
}

/// <summary>
/// Parámetros que reciben los drones al aparecer. Es una estructura simple
/// (sin ScriptableObject) para que los drones no dependan del perfil.
/// </summary>
public readonly struct DroneDifficultyParams
{
    /// <summary>Multiplicador fijo sobre las velocidades del prefab (1 = sin cambio).</summary>
    public readonly float SpeedMultiplier;

    // Fusilero
    public readonly float ShooterFireRate;     // segundos entre disparos
    public readonly float ShooterSightRange;   // radio de visión
    public readonly float ShooterScanInterval; // segundos entre búsquedas de jugadores

    // Bombardero
    public readonly float BomberDropInterval;  // segundos entre bombas
    public readonly float BombFuseTime;        // segundos de mecha de cada bomba

    public DroneDifficultyParams(
        float speedMultiplier,
        float shooterFireRate, float shooterSightRange, float shooterScanInterval,
        float bomberDropInterval, float bombFuseTime)
    {
        SpeedMultiplier = speedMultiplier;
        ShooterFireRate = shooterFireRate;
        ShooterSightRange = shooterSightRange;
        ShooterScanInterval = shooterScanInterval;
        BomberDropInterval = bomberDropInterval;
        BombFuseTime = bombFuseTime;
    }
}
