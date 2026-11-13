using UnityEngine;
using TMPro;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private TMP_Text statsText; // Arrastrá tu TextMeshPro aquí

    private void OnEnable()
    {
        // Cada vez que se active este panel/objeto, se actualizan los datos del JSON
        CargarYMostrarEstadisticas();
    }

    public void CargarYMostrarEstadisticas()
    {
        if (statsText == null)
        {
            Debug.LogWarning("No se ha asignado el componente TextMeshPro en PlayerStatsUI.");
            return;
        }

        // Cargamos los datos actuales desde el archivo JSON local
        PlayerData data = PersistenceManager.LoadData();

        // Opcional: formatear el tiempo total de segundos a formato legible (Minutos:Segundos)
        string tiempoFormateado = FormatearTiempo(data.tiempoTotalSobrevivido);

        // Asignamos el texto al TextMeshPro
        statsText.text = $"Partidas Jugadas: {data.partidasJugadas}\n" +
                         $"Tiempo Sobrevivido: {tiempoFormateado}";
    }

    private string FormatearTiempo(float segundosTotales)
    {
        int minutos = Mathf.FloorToInt(segundosTotales / 60f);
        int segundos = Mathf.FloorToInt(segundosTotales % 60f);
        return $"{minutos:00}:{segundos:00}";
    }
}
