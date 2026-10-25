using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class NewMonoBehaviourScript : MonoBehaviour
{
    [SerializeField] private Button stopServerButtonMenu;
    [SerializeField] private string lobbySceneName = "Lobby";

    private void Awake()
    {
        stopServerButtonMenu.onClick.AddListener(OnExitClicked);
    }

    private void OnExitClicked()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("Desconexion completada.");
        }
        SceneManager.LoadScene(lobbySceneName);
    }
}
