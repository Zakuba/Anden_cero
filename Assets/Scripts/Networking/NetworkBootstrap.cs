using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NetworkBootstrap : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private string gameSceneName = "MainGame";

    private void Awake()
    {
        if (hostButton != null)
        {
            hostButton.onClick.AddListener(OnHostClicked);
        }
    }

    private void OnHostClicked()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No se encontró el NetworkManager en la escena.");
            return;
        }

        // Verificamos que Scene Management esté habilitado
        if (!NetworkManager.Singleton.NetworkConfig.EnableSceneManagement)
        {
            Debug.LogWarning("Enable Scene Management debe estar tildado en el NetworkManager para cargar escenas en red.");
        }

        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log("Host inicializado correctamente.");
            
            // Carga autoritativa en red: traslada a todos los clientes a la escena de juego
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("No se pudo inicializar el Host.");
        }
    }
}