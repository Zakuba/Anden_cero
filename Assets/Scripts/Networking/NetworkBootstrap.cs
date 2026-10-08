using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NetworkBootstrap : MonoBehaviour
{
    [Header("Botón iniciar juego")]
    [SerializeField] private Button hostButton;

    [Header("Selector de escenario")]
    [SerializeField] private SelectorEscenario selectorEscenario;

    [Header("Mapa por defecto")]
    [SerializeField] private string mapaPorDefecto = "MainGame";

    private void Awake()
    {
        if (hostButton != null)
        {
            hostButton.onClick.AddListener(OnHostClicked);
        }
    }

    private void OnHostClicked()
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