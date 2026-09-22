using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HostSessionUI : MonoBehaviour
{
    [SerializeField] private GameObject waitingIndicator;
    [SerializeField] private Button stopServerButton;
    [SerializeField] private string lobbySceneName = "Lobby";

    private void Awake()
    {
        stopServerButton.onClick.AddListener(OnStopServerClicked);
    }

    private void Start()
    {
        if (waitingIndicator != null)
        {
            waitingIndicator.SetActive(true);
        }
    }

    private void OnStopServerClicked()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("Servidor detenido.");
        }
        SceneManager.LoadScene(lobbySceneName);
    }
}