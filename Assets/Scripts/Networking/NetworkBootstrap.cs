using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class NetworkBootstrap : MonoBehaviour
{
    [Header("Botón iniciar juego")]
    [SerializeField] private Button hostButton;

    [Header("Botón Minijuego")]
    [SerializeField] private Button minijuegoButton;

    [Header("Selector de escenario")]
    [SerializeField] private SelectorEscenario selectorEscenario;

    [Header("Mapa por defecto")]
    [SerializeField] private string mapaPorDefecto = "MainGame";

    [Header("Conexión online")]
    [SerializeField] private bool usarRelay = true;

    [Header("Mensajes")]
    [SerializeField] private TMP_Text mensajeErrorSala;

    private void Awake()
    {
        if (hostButton != null)
        {
            hostButton.onClick.AddListener(OnHostClicked);
        }

        if (minijuegoButton != null)
        {
            minijuegoButton.onClick.AddListener(OnMinijuegoClicked);
        }
    }

    private void Start()
    {
        if (usarRelay && (hostButton != null || minijuegoButton != null))
        {
            StartCoroutine(EsperarServicios());
        }
    }

    private IEnumerator EsperarServicios()
    {
        // Los botones quedan apagados hasta que Unity Services esté listo
        if (hostButton != null) hostButton.interactable = false;
        if (minijuegoButton != null) minijuegoButton.interactable = false;

        // Esperamos como máximo 8 segundos para no dejar los botones muertos
        float espera = 0f;
        while (!UnityServicesInitializer.IsReady && espera < 8f)
        {
            espera += Time.unscaledDeltaTime;
            yield return null;
        }

        if (hostButton != null) hostButton.interactable = true;
        if (minijuegoButton != null) minijuegoButton.interactable = true;

        if (!UnityServicesInitializer.IsReady)
        {
            MostrarErrorSala("No se pudo conectar con los servicios online. Revisá tu conexión.");
        }
    }

    private void MostrarErrorSala(string mensaje)
    {
        Debug.LogWarning(mensaje);

        if (mensajeErrorSala != null)
        {
            mensajeErrorSala.text = mensaje;
            mensajeErrorSala.gameObject.SetActive(true);
        }
    }

    private async void OnHostClicked()
    {
        // Ocultamos el mensaje de un intento anterior
        if (mensajeErrorSala != null)
        {
            mensajeErrorSala.gameObject.SetActive(false);
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No se encontró el NetworkManager en la escena.");
            return;
        }

        if (!NetworkManager.Singleton.NetworkConfig.EnableSceneManagement)
        {
            Debug.LogWarning(
                "Enable Scene Management debe estar tildado " +
                "en el NetworkManager para cargar escenas en red."
            );
        }

        // Obtenemos el mapa seleccionado
        string mapaSeleccionado = mapaPorDefecto;

        if (selectorEscenario != null)
        {
            string nombre = selectorEscenario.ObtenerNombreEscenario();

            mapaSeleccionado = nombre switch
            {
                "Andén Central"   => "MainGame",
                "Túneles y Vías"  => "ViasYAndenes",
                "Sala de control" => "SalaDeControl",
                "MiniJuego"       => "MiniJuego",
                _                 => nombre
            };
        }

        if (string.IsNullOrEmpty(mapaSeleccionado))
        {
            mapaSeleccionado = mapaPorDefecto;
        }

        Debug.Log("Mapa seleccionado: " + mapaSeleccionado);

        ConfigurarCallbackConexion();

        if (usarRelay)
        {
            hostButton.interactable = false;

            string joinCode = await RelayManager.CreateRelay(MatchManager.MaxPlayers - 1);

            if (this == null) return;

            hostButton.interactable = true;

            if (string.IsNullOrEmpty(joinCode))
            {
                MostrarErrorSala("No se pudo crear la sala online. Revisá tu conexión a internet.");
                return;
            }

            GUIUtility.systemCopyBuffer = joinCode;
            Debug.Log("Código de sala (copiado al portapapeles): " + joinCode);
        }

        IniciarHostYCargarEscena(mapaSeleccionado);
    }

    private async void OnMinijuegoClicked()
    {
        if (mensajeErrorSala != null)
        {
            mensajeErrorSala.gameObject.SetActive(false);
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No se encontró el NetworkManager en la escena.");
            return;
        }

        if (!NetworkManager.Singleton.NetworkConfig.EnableSceneManagement)
        {
            Debug.LogWarning(
                "Enable Scene Management debe estar tildado " +
                "en el NetworkManager para cargar escenas en red."
            );
        }

        string mapaSeleccionado = "MiniJuego";
        Debug.Log("Mapa seleccionado: " + mapaSeleccionado);

        ConfigurarCallbackConexion();

        if (usarRelay)
        {
            minijuegoButton.interactable = false;

            string joinCode = await RelayManager.CreateRelay(MatchManager.MaxPlayers - 1);

            if (this == null) return;

            minijuegoButton.interactable = true;

            if (string.IsNullOrEmpty(joinCode))
            {
                MostrarErrorSala("No se pudo crear la sala online. Revisá tu conexión a internet.");
                return;
            }

        }

        IniciarHostYCargarEscena(mapaSeleccionado);
    }

    private void ConfigurarCallbackConexion()
    {
        // Rechaza entradas tarde y limita a MatchManager.MaxPlayers
        NetworkManager.Singleton.ConnectionApprovalCallback = (request, response) =>
        {
            int connected = NetworkManager.Singleton.ConnectedClientsIds.Count;
            bool hasRoom = connected < MatchManager.MaxPlayers;
            bool joinsOpen = !MatchManager.JoinsClosed;

            response.Approved = hasRoom && joinsOpen;
            response.CreatePlayerObject = true;
            response.Pending = false;

            if (!response.Approved)
            {
                response.Reason = !joinsOpen
                    ? "La partida ya comenzó."
                    : $"La sala está llena (máximo {MatchManager.MaxPlayers} jugadores).";
            }
        };
    }

    private void IniciarHostYCargarEscena(string escena)
    {
        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log("Host inicializado correctamente.");

            NetworkManager.Singleton.SceneManager.LoadScene(
                escena,
                LoadSceneMode.Single
            );
        }
        else
        {
            Debug.LogError("No se pudo inicializar el Host.");
        }
    }
}