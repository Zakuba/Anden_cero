using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class MiniJuegoEscena : MonoBehaviour
{
    [Header("Configuración del Botón")]
    [Tooltip("Botón de la interfaz del Lobby asignado para abrir el minijuego.")]
    [SerializeField] private Button minijuegoButton;

    [Header("Nombre de la Escena")]
    [Tooltip("Nombre exacto de la escena del minijuego configurada en Build Settings.")]
    [SerializeField] private string minijuegoSceneName = "MiniJuego";

    private void Awake()
    {
        if (minijuegoButton == null)
            minijuegoButton = GetComponent<Button>();

        if (minijuegoButton != null)
            minijuegoButton.onClick.AddListener(OnMinijuegoButtonClicked);
    }

    private void OnDestroy()
    {
        if (minijuegoButton != null)
            minijuegoButton.onClick.RemoveListener(OnMinijuegoButtonClicked);
    }

    private void OnMinijuegoButtonClicked()
    {
        // 1. Si existe NetworkManager activo con Scene Management habilitado
        if (NetworkManager.Singleton != null && 
            NetworkManager.Singleton.IsListening && 
            NetworkManager.Singleton.NetworkConfig.EnableSceneManagement)
        {
            // Solo el servidor/Host puede ordenar el cambio de escena por red
            if (NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(
                    minijuegoSceneName, 
                    LoadSceneMode.Single
                );
            }
            else
            {
                Debug.LogWarning("[LobbyMiniJuegoButton] Solo el Host puede cambiar a la escena del minijuego.");
            }
        }
        else
        {
            // 2. Si es una navegación local fuera de sesión de red
            SceneManager.LoadScene(minijuegoSceneName, LoadSceneMode.Single);
        }
    }
}