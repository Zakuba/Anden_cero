using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

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
        hostButton.interactable = false;

        // Esperamos como máximo 8 segundos: si no hay internet, no queremos
        // dejar el botón muerto, porque queda el respaldo por IP directa
        float espera = 0f;
        while (!UnityServicesInitializer.IsReady && espera < 8f)
        {
            espera += Time.unscaledDeltaTime;
            yield return null;
        }

        hostButton.interactable = true;

        if (!UnityServicesInitializer.IsReady)
        {
            Debug.LogWarning("Los servicios online no estuvieron listos a tiempo. Se podrá crear la partida por IP directa.");
        }
    }

    private async void OnHostClicked()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError(
                "No se encontró el NetworkManager en la escena."
            );

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
                _                 => nombre // Si no coincide con ninguno, conserva el nombre original
            };
        }

        // Si por algún motivo el selector devuelve vacío,
        // usamos el mapa por defecto.
        if (string.IsNullOrEmpty(mapaSeleccionado))
        {
            mapaSeleccionado = mapaPorDefecto;
        }

        Debug.Log(
            "Mapa seleccionado: " +
            mapaSeleccionado
        );
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
                    : "La sala está llena (máximo 4 jugadores).";
            }
        };
        if (usarRelay)
        {
            // Evita doble clic mientras se crea la sala
            hostButton.interactable = false;

            // MaxPlayers cuenta al host; Relay probablemente cuente solo a los invitados
            // (la documentación no lo aclara, lo confirmamos probando)
            string joinCode = await RelayManager.CreateRelay(MatchManager.MaxPlayers - 1);

            hostButton.interactable = true;

            if (string.IsNullOrEmpty(joinCode))
            {
                // Si Relay falla (sin internet, etc.), seguimos con conexión directa por IP
                Debug.LogWarning("No se pudo crear la sala online. Se inicia el host con conexión directa (IP).");
            }
            else
            {
                // Lo copiamos al portapapeles para poder pegarlo y enviarlo
                GUIUtility.systemCopyBuffer = joinCode;
                Debug.Log("Código de sala (copiado al portapapeles): " + joinCode);
            }
        }

        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log(
                "Host inicializado correctamente."
            );

            NetworkManager.Singleton.SceneManager.LoadScene(
                mapaSeleccionado,
                LoadSceneMode.Single
            );
        }
        else
        {
            Debug.LogError(
                "No se pudo inicializar el Host."
            );
        }
    }
}