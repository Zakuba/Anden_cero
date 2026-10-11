using UnityEngine;

public class HUDController : MonoBehaviour
{
    [Header("Referencias de UI")]
    [SerializeField] private GameObject hudCanvasContainer; // El Canvas o Panel que agrupa el HUD (Vidas, Bombas, etc.)

    private void Start()
    {
        // Al arrancar la escena (estando en el Lobby), nos aseguramos de que el HUD esté apagado
        if (hudCanvasContainer != null)
        {
            hudCanvasContainer.SetActive(false);
        }
    }

    private void Update()
    {
        // Si el MatchManager existe y la partida ya dio inicio...
        if (MatchManager.Instance != null && MatchManager.Instance.IsMatchRunning)
        {
            // ...activamos el HUD automáticamente para este cliente si es que estaba apagado
            if (hudCanvasContainer != null && !hudCanvasContainer.activeSelf)
            {
                hudCanvasContainer.SetActive(true);
            }
        }
        else
        {
            // Opcional: Si por alguna razón vuelve al estado de lobby, lo apaga
            if (hudCanvasContainer != null && hudCanvasContainer.activeSelf)
            {
                hudCanvasContainer.SetActive(false);
            }
        }
    }
}