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
    [SerializeField] private TMP_InputField codigoSalaInputField;

    private const string CaracteresValidosCodigo = "6789BCDFGHJKLMNPQRTW";

    private UnityTransport transport;
    private bool isAttemptingConnection = false;
    private Coroutine timeoutCoroutine;
    private TMP_Text connectionFailedText;

    private void Awake()
    {
        connectButton.onClick.AddListener(OnConnectClicked);
        connectionFailedText = connectionFailedMessage.GetComponent<TMP_Text>();
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

    private async void OnConnectClicked()
    {
        // Evita doble clic mientras ya hay un intento en curso.
        if (isAttemptingConnection) return;

        connectionFailedMessage.SetActive(false);
        isAttemptingConnection = true;
        connectButton.interactable = false;

        string codigo = codigoSalaInputField != null
            ? codigoSalaInputField.text.Trim().ToUpper()
            : "";

        if (!string.IsNullOrEmpty(codigo))
        {
            // Validamos el formato antes de molestar al servicio de Relay
            if (!CodigoSalaValido(codigo))
            {
                FailConnection("Código de sala inválido (6 caracteres).");
                return;
            }

            // Conexión online con código de sala
            ResultadoUnirse resultado = await RelayManager.JoinRelay(codigo);

            // Si mientras esperábamos se destruyó este objeto, no seguimos
            if (this == null) return;

            if (resultado != ResultadoUnirse.Ok)
            {
                string mensaje = resultado switch
                {
                    ResultadoUnirse.SinInternet      => "Sin conexión a internet.",
                    ResultadoUnirse.SalaNoEncontrada => "No existe una sala con ese código.",
                    _                                => "No se pudo unir a la sala. Intentá de nuevo."
                };

                FailConnection(mensaje);
                return;
            }

            // Con Relay hace falta más margen de conexión que en red local
            transport.MaxConnectAttempts = 60;
            transport.ConnectTimeoutMS = 1000;
        }
        else
        {
            // Conexión directa por IP (como antes)
            string ip = string.IsNullOrWhiteSpace(ipInputField.text)
                ? "127.0.0.1"
                : ipInputField.text.Trim();

            if (!ushort.TryParse(portInputField.text, out ushort port))
            {
                port = 7777;
            }

            // Volvemos a los valores normales por si antes se usó Relay
            transport.MaxConnectAttempts = 3;
            transport.ConnectTimeoutMS = 500;

            transport.SetConnectionData(ip, port);
        }

        if (!NetworkManager.Singleton.StartClient())
        {
            FailConnection();
            return;
        }

        // Respaldo propio: si en X segundos no llegó OnClientConnected,
        // lo tratamos como fallo sin esperar el timeout interno de UTP.
        timeoutCoroutine = StartCoroutine(ConnectionTimeoutWatcher());
    }

    private bool CodigoSalaValido(string codigo)
    {
        if (codigo.Length < 6 || codigo.Length > 12) return false;

        foreach (char c in codigo)
        {
            if (!CaracteresValidosCodigo.Contains(c)) return false;
        }

        return true;
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

    private void FailConnection(string mensaje = "Conexión fallida. Verifica IP o Puerto.")
    {
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }

        if (connectionFailedText != null)
            connectionFailedText.text = mensaje;

        connectionFailedMessage.SetActive(true);
        isAttemptingConnection = false;
        connectButton.interactable = true;
    }
}