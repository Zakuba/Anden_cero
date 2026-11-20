using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class HostSessionUI : MonoBehaviour
{
    [SerializeField] private GameObject waitingIndicator;
    [SerializeField] private Button stopServerButton;
    [SerializeField] private Button stopServerButtonMenu;
    [SerializeField] private string lobbySceneName = "Lobby";

    private void Awake()
    {
        stopServerButton.onClick.AddListener(OnExitClicked);
        stopServerButtonMenu.onClick.AddListener(OnExitClicked);
    }

    private void Start()
    {
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        TMP_Text label = stopServerButton.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = isHost ? "Salir de Partida" : "Salir de Partida";
        }

        if (waitingIndicator != null)
        {
            waitingIndicator.SetActive(isHost);
        }
    }

    private void OnExitClicked()
{
    StartCoroutine(SalirYVolver());
}

private IEnumerator SalirYVolver()
{
    if (NetworkManager.Singleton != null)
    {
        NetworkManager.Singleton.Shutdown();

        // Esperamos a que Netcode termine de apagarse antes de cambiar de escena
        while (NetworkManager.Singleton != null &&
               NetworkManager.Singleton.ShutdownInProgress)
            yield return null;

        Debug.Log("Desconexion completada.");
    }

    SceneManager.LoadScene(lobbySceneName);
}
}