using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class HostSessionUI : MonoBehaviour
{
    [SerializeField] private GameObject waitingIndicator;
    [SerializeField] private Button stopServerButton;
    [SerializeField] private string lobbySceneName = "Lobby";

    private void Awake()
    {
        stopServerButton.onClick.AddListener(OnExitClicked);
    }

    private void Start()
    {
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        TMP_Text label = stopServerButton.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = isHost ? "Detener Servidor" : "Desconectar";
        }

        if (waitingIndicator != null)
        {
            waitingIndicator.SetActive(isHost);
        }
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