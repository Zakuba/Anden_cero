using System.Collections;
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