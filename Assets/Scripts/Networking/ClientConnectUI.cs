using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ClientConnectUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TMP_InputField portInputField;
    [SerializeField] private Button connectButton;
    [SerializeField] private GameObject connectionFailedMessage;

    private UnityTransport transport;
    private bool isAttemptingConnection = false;

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
        connectionFailedMessage.SetActive(false);
        isAttemptingConnection = true;

        string ip = string.IsNullOrWhiteSpace(ipInputField.text) ? "127.0.0.1" : ipInputField.text.Trim();
        if (!ushort.TryParse(portInputField.text, out ushort port))
        {
            port = 7777;
        }

        transport.SetConnectionData(ip, port);

        if (!NetworkManager.Singleton.StartClient())
        {
            isAttemptingConnection = false;
            connectionFailedMessage.SetActive(true);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        isAttemptingConnection = false;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (isAttemptingConnection)
        {
            connectionFailedMessage.SetActive(true);
        }
        isAttemptingConnection = false;
    }
}