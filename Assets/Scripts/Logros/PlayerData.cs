using System;
using System.Collections.Generic;

[Serializable]
public class PlayerData
{
    public string nombreJugador;
    // Contadores de estadísticas
    public int bombsBatted = 0;
    // Mejor tiempo sobrevivido en modo Supervivencia (en segundos)
    public float mejorTiempoSupervivencia = 0f;

    // Récord de Supervivencia: oleada más alta; a igual oleada, menor tiempo en llegar a ella
    public int mejorOleadaSupervivencia = 0;
    public float mejorTiempoParaOleada = 0f;
    
    // Lista de IDs de logros ya desbloqueados
    public List<string> unlockedAchievements = new List<string>();
    //Record Minijuego Bombas
    public float recordMinijuegoBombas = 0f;
}
