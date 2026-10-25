using UnityEngine;


public static class MatchSettings
{
    public static bool HasDifficulty { get; private set; }

    public static DifficultyLevel Difficulty { get; private set; } = DifficultyLevel.Normal;

    public static void SetDifficulty(DifficultyLevel level)
    {
        Difficulty = level;
        HasDifficulty = true;
    }

    public static void SetDifficultyFromIndex(int index)
    {
        switch (index)
        {
            case 0: SetDifficulty(DifficultyLevel.Easy); break;
            case 2: SetDifficulty(DifficultyLevel.Hard); break;
            default: SetDifficulty(DifficultyLevel.Normal); break;
        }
    }

    public static void Clear()
    {
        Difficulty = DifficultyLevel.Normal;
        HasDifficulty = false;
    }

    // Por si tienes desactivada la recarga de dominio al entrar a Play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Clear();
    }
}