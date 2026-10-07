using System;
using System.Collections.Generic;

[Serializable]
public class PlayerData
{
    // Contadores de estadísticas
    public int bombsBatted = 0;
    
    // Lista de IDs de logros ya desbloqueados
    public List<string> unlockedAchievements = new List<string>();
}
