using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [Header("Referencias de UI de Pausa")]
    [SerializeField] private GameObject pauseMenuCanvas; // El Canvas contenedor de la pausa
    [SerializeField] private GameObject pausePanel;      // El panel con los botones principales
    [SerializeField] private GameObject optionsPanel;    // El panel de opciones

    private bool isPaused = false;
    private bool wasMatchRunning = false;

    public static bool IsGamePaused => _instance != null && _instance.isPaused;
    private static PauseManager _instance;

    private void Awake()
    {
        if (_instance == null)
            _instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // Al cargar la escena estamos en el Lobby: nos aseguramos de que 
        // la pausa esté apagada y el cursor esté visible para la interfaz del lobby.
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {

        // 1. Verificamos si la partida ya dio inicio (si el MatchManager existe y la partida corre)
        bool matchRunning = MatchManager.Instance == null || MatchManager.Instance.IsMatchRunning;

        // Detectar cuando la partida ACABA DE TERMINAR (estaba corriendo y ahora no)
        if (wasMatchRunning && !matchRunning)
        {
            // La partida finalizó: liberamos el cursor para que pueda tocar los botones de fin de partida
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        wasMatchRunning = matchRunning;

        // Si todavía estamos en el lobby (la partida no arrancó), ignoramos la tecla Escape por completo
        if (!matchRunning) return;

        // 2. Detectar la tecla Escape solo cuando ya empezó la partida
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                if (optionsPanel != null && optionsPanel.activeSelf)
                {
                    CerrarOpcionesEnPausa();
                }
                else
                {
                    ResumeGame();
                }
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        isPaused = true;

        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.SetActive(false);

        // Liberar el cursor para el menú de pausa
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // Reanuda la partida

        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(false);

        // Ocultar y bloquear el cursor de nuevo para jugar en tercera persona
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void AbrirOpcionesDesdePausa()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    public void CerrarOpcionesEnPausa()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void SalirAlMenuPrincipal(string nombreEscenaMenu)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(nombreEscenaMenu);
    }
}