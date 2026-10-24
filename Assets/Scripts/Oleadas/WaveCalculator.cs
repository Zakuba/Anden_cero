using System;

/// <summary>
/// Cálculos puros del modo Supervivencia (sin dependencias de Unity).
/// Se mantiene separado de WaveManager para poder probarlo en EditMode.
/// </summary>
public static class WaveCalculator
{
    /// <summary>
    /// Cantidad total de drones (sin contar al Sanador) de una oleada:
    /// base + (oleada - 1) * incremento. Nunca menor a 1.
    /// </summary>
    public static int TotalEnemies(int wave, int baseDrones, int increment)
    {
        wave = Math.Max(1, wave);
        return Math.Max(1, baseDrones + (wave - 1) * increment);
    }

    /// <summary>
    /// Tope de drones vivos al mismo tiempo, según los jugadores vivos.
    /// Con capPerPlayer = 0 el tope es fijo (capBase).
    /// </summary>
    public static int MaxAlive(int capBase, int capPerPlayer, int alivePlayers)
    {
        alivePlayers = Math.Max(1, alivePlayers);
        return Math.Max(1, capBase + capPerPlayer * alivePlayers);
    }

    /// <summary>
    /// Reparte el total entre Fusileros y Bombarderos. La proporción de
    /// Fusileros crece con la oleada hasta ratioMax. Si hay 2 o más drones,
    /// siempre hay al menos uno de cada tipo.
    /// </summary>
    public static void Composition(
        int total, int wave,
        float ratioStart, float ratioStep, float ratioMax,
        out int shooters, out int bombers)
    {
        total = Math.Max(0, total);
        wave = Math.Max(1, wave);

        float ratio = ratioStart + (wave - 1) * ratioStep;
        ratio = Math.Min(ratio, ratioMax);
        ratio = Math.Max(0f, Math.Min(1f, ratio));

        shooters = (int)Math.Round((double)(total * ratio), MidpointRounding.AwayFromZero);

        if (total >= 2)
            shooters = Math.Max(1, Math.Min(shooters, total - 1));
        else
            shooters = Math.Min(shooters, total);

        bombers = total - shooters;
    }

    /// <summary>
    /// ¿Esta oleada trae un Dron Sanador? Aparece desde firstWave y luego
    /// cada everyN oleadas. Con firstWave o everyN menores a 1, nunca.
    /// </summary>
    public static bool HasHealer(int wave, int firstWave, int everyN)
    {
        if (firstWave < 1 || everyN < 1) return false;
        return wave >= firstWave && (wave - firstWave) % everyN == 0;
    }
}
