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
    }

    private void Start()
    {
        if (usarRelay && hostButton != null)
        {
            StartCoroutine(EsperarServicios());
        }
    }

    private IEnumerator EsperarServicios()
    {
        // El botón queda apagado hasta que Unity Services esté listo
        if (hostButton != null) hostButton.interactable = false;

        // Esperamos como máximo 8 segundos para no dejar el botón muerto
        float espera = 0f;
        while (!UnityServicesInitializer.IsReady && espera < 8f)
        {
            espera += Time.unscaledDeltaTime;
            yield return null;
        }

        if (hostButton != null) hostButton.interactable = true;

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
                "Sala de Control" => "SalaDeControl",
                "MiniJuego"       => "MiniJuego",
                _                 => nombre
            };
        }

        if (string.IsNullOrEmpty(mapaSeleccionado))
        {
            mapaSeleccionado = mapaPorDefecto;
        }

        Debug.Log("Mapa seleccionado: " + mapaSeleccionado);

        bool esMinijuego = mapaSeleccionado == "MiniJuego";
        int maxJugadores = esMinijuego ? 1 : MatchManager.MaxPlayers;
        ConfigurarCallbackConexion(maxJugadores, esMinijuego);

        if (usarRelay)
        {
            hostButton.interactable = false;

            string joinCode = await RelayManager.CreateRelay(Mathf.Max(1, maxJugadores - 1));

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

 private void ConfigurarCallbackConexion(int maxJugadores, bool esMinijuego)
{
    // Rechaza entradas tarde y limita la cantidad de jugadores según el modo
    NetworkManager.Singleton.ConnectionApprovalCallback = (request, response) =>
    {
        int connected = NetworkManager.Singleton.ConnectedClientsIds.Count;
        bool hasRoom = connected < maxJugadores;
        bool joinsOpen = !MatchManager.JoinsClosed;

        response.Approved = hasRoom && joinsOpen;
        response.CreatePlayerObject = true;
        response.Pending = false;

        if (!response.Approved)
        {
            if (!joinsOpen)
                response.Reason = "La partida ya comenzó.";
            else if (esMinijuego)
                response.Reason = "El minijuego es de un solo jugador.";
            else
                response.Reason = $"La sala está llena (máximo {maxJugadores} jugadores).";
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