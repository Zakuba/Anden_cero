using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkBootstrap : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private string gameSceneName = "MainGame";

    private void Awake()
    {
        hostButton.onClick.AddListener(OnHostClicked);
    }

    private void OnHostClicked()
    {
        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log("Host inicializado correctamente.");
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("No se pudo inicializar el Host.");
        }
    }
}