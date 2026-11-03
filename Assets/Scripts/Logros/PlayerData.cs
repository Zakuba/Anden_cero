using System;
using System.Collections.Generic;

[Serializable]
public class PlayerData
{
    // Estadísticas acumulativas
    public int partidasJugadas = 0;             // NUEVO: Total de partidas acumuladas
    public float tiempoTotalSobrevivido = 0f;     // NUEVO: Tiempo acumulado en segundos (todos los modos excepto minijuego)
    public string nombreJugador;
    public string avatarBase64 = "";
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
