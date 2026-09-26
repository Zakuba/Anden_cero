using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ClientConnectUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TMP_InputField portInputField;
    [SerializeField] private Button connectButton;
    [SerializeField] private GameObject connectionFailedMessage;
    [SerializeField] private float connectionTimeoutSeconds = 5f;

    private UnityTransport transport;
    private bool isAttemptingConnection = false;
    private Coroutine timeoutCoroutine;

    private void Awake()
    {
        connectButton.onClick.AddListener(OnConnectClicked);
    }

    private void Start()
    {
        transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnConnectClicked()
    {
        // Evita doble clic mientras ya hay un intento en curso.
        if (isAttemptingConnection) return;

        connectionFailedMessage.SetActive(false);
        isAttemptingConnection = true;
        connectButton.interactable = false;

        string ip = string.IsNullOrWhiteSpace(ipInputField.text) ? "127.0.0.1" : ipInputField.text.Trim();
        if (!ushort.TryParse(portInputField.text, out ushort port))
        {
            port = 7777;
        }

        transport.SetConnectionData(ip, port);

        if (!NetworkManager.Singleton.StartClient())
        {
            FailConnection();
            return;
        }

        // Respaldo propio: si en X segundos no llegó OnClientConnected,
        // lo tratamos como fallo sin esperar el timeout interno de UTP.
        timeoutCoroutine = StartCoroutine(ConnectionTimeoutWatcher());
    }

    private IEnumerator ConnectionTimeoutWatcher()
    {
        yield return new WaitForSeconds(connectionTimeoutSeconds);

        if (isAttemptingConnection)
        {
            NetworkManager.Singleton.Shutdown();
            FailConnection();
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
        isAttemptingConnection = false;
        connectButton.interactable = true;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (isAttemptingConnection)
        {
            NetworkManager.Singleton.Shutdown();
            FailConnection();
        }
    }

    private void FailConnection()
    {
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
        connectionFailedMessage.SetActive(true);
        isAttemptingConnection = false;
        connectButton.interactable = true;
    }
}