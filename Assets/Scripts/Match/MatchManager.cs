
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public enum MatchMode
{
    BattleRoyale,
    Survival
}

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    // Se pone en true cuando arranca la partida:
    // lo usa ConnectionApproval para rechazar late joiners.
    public static bool JoinsClosed { get; private set; }

    public const int MaxPlayers = 4;
    private const int MinPlayersToStart = 1;

    [Header("Modo de juego")]
    [SerializeField] private MatchMode mode = MatchMode.BattleRoyale;
    public MatchMode Mode => mode;

    [Header("Resultados")]
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TMP_Text resultsText;

    [Header("Inicio de partida (solo host)")]
    [SerializeField] private Button startMatchButton;
    [SerializeField] private GameObject waitingText;
    [SerializeField] private GameObject waitingForHostText;
    [SerializeField] private TMP_Text roomCodeText;

    [Header("Cámaras")]
    [SerializeField] private Camera lobbyCamera;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera resultsCamera;

    [Header("Spawns")]
    [SerializeField] private Transform[] spawnPoints;
    private int nextSpawnIndex;

    [Header("Spawns de resultados (Battle Royale)")]
    [SerializeField] private Transform puntoVictoria;
    [SerializeField] private Transform puntoDerrota1;
    [SerializeField] private Transform puntoDerrota2;
    [SerializeField] private Transform puntoDerrota3;

    [Header("Spawns de resultados (Empate)")]
    [SerializeField] private Transform puntoEmpate1;
    [SerializeField] private Transform puntoEmpate2;
    [SerializeField] private Transform puntoEmpate3;
    [SerializeField] private Transform puntoEmpate4;

    // Orden de eliminación:
    // el primero que muere ocupa puntoDerrota1, etc.
    private readonly List<PlayerStateManager> eliminatedPlayers =
        new List<PlayerStateManager>();

    [Header("Posiciones del lobby")]
    [SerializeField] private Transform[] lobbySpawnPoints;
    private int nextLobbySpawnIndex;

    private readonly NetworkVariable<bool> matchStarted =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public event System.Action<bool, bool> MatchStartedChanged;

    private readonly List<PlayerStateManager> registeredPlayers =
        new List<PlayerStateManager>();

    private bool matchEnded;
    private bool checkScheduled;

    public bool IsMatchRunning => matchStarted.Value && !matchEnded;

    private void Awake()
    {
        Instance = this;

        if (startMatchButton != null)
            startMatchButton.gameObject.SetActive(false);

        if (waitingText != null)
            waitingText.SetActive(false);

        if (waitingForHostText != null)
            waitingForHostText.SetActive(false);

        if (roomCodeText != null)
            roomCodeText.gameObject.SetActive(false);

        ActivarLobbyCamera();
    }

    public override void OnNetworkSpawn()
    {
        JoinsClosed = false;

        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        matchStarted.OnValueChanged += OnMatchStartedChanged;

        if (startMatchButton != null)
        {
            startMatchButton.onClick.AddListener(OnStartButtonClicked);
            startMatchButton.gameObject.SetActive(
                IsHost && !matchStarted.Value
            );
            startMatchButton.interactable = false;
        }

        if (waitingText != null)
            waitingText.SetActive(IsHost && !matchStarted.Value);

        if (waitingForHostText != null)
            waitingForHostText.SetActive(!IsHost && !matchStarted.Value);

        // El host ve el código de sala mientras espera.
        if (roomCodeText != null)
        {
            bool mostrarCodigo = IsHost
                && !matchStarted.Value
                && !string.IsNullOrEmpty(RelayManager.JoinCodeActual);

            roomCodeText.gameObject.SetActive(mostrarCodigo);

            if (mostrarCodigo)
                roomCodeText.text =
                    "Código de sala: " + RelayManager.JoinCodeActual;
        }

        if (IsServer)
        {
            foreach (var p in FindObjectsByType<PlayerStateManager>(
                         FindObjectsSortMode.None))
            {
                RegisterPlayer(p);
            }
        }

        // Conectar los jugadores existentes en cada cliente.
        foreach (PlayerStateManager player in
                 FindObjectsByType<PlayerStateManager>(
                     FindObjectsSortMode.None))
        {
            if (player != null)
                player.ConectarMatchManager(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        matchStarted.OnValueChanged -= OnMatchStartedChanged;

        if (startMatchButton != null)
            startMatchButton.onClick.RemoveListener(OnStartButtonClicked);

        JoinsClosed = false;

        if (Instance == this)
            Instance = null;
    }

    private void OnMatchStartedChanged(bool oldValue, bool newValue)
    {
        MatchStartedChanged?.Invoke(oldValue, newValue);

        if (!newValue)
        {
            ActivarLobbyCamera();
            return;
        }

        ActivarMainCamera();

        if (startMatchButton != null)
            startMatchButton.gameObject.SetActive(false);

        if (waitingText != null)
            waitingText.SetActive(false);

        if (waitingForHostText != null)
            waitingForHostText.SetActive(false);

        if (roomCodeText != null)
            roomCodeText.gameObject.SetActive(false);
    }

    // ---------- Registro de jugadores (solo servidor) ----------

    public void RegisterPlayer(PlayerStateManager player)
    {
        if (!IsServer || player == null || registeredPlayers.Contains(player))
            return;

        registeredPlayers.Add(player);
        AssignLobbySpawn(player);

        player.currentState.OnValueChanged += (_, newState) =>
            OnPlayerStateChanged(player, newState);

        RefreshStartButton();
    }

    private int ConnectedPlayersCount()
    {
        registeredPlayers.RemoveAll(p => p == null);
        return registeredPlayers.Count;
    }

    private void RefreshStartButton()
    {
        if (startMatchButton == null || matchStarted.Value)
            return;

        startMatchButton.interactable =
            ConnectedPlayersCount() >= MinPlayersToStart;
    }

    // ---------- Inicio de partida ----------

    private void OnStartButtonClicked()
    {
        if (!IsServer)
            return;

        StartMatch();
    }

    private void StartMatch()
    {
        if (matchStarted.Value ||
            ConnectedPlayersCount() < MinPlayersToStart)
            return;

        JoinsClosed = true;
        eliminatedPlayers.Clear();

        matchEnded = false;
        checkScheduled = false;

        // Primero marcamos la partida como iniciada.
        matchStarted.Value = true;

        foreach (PlayerStateManager player in registeredPlayers)
        {
            if (player == null)
                continue;

            PlayerCharacterVisual visual =
                player.GetComponent<PlayerCharacterVisual>();

            if (visual != null)
                visual.ResetLobbyAnimation();

            AssignSpawn(player);
            player.ActualizarColisionesPartida();
        }
    }

    // ---------- Victoria y eliminación ----------

    private void OnPlayerStateChanged(
        PlayerStateManager player,
        PlayerState newState)
    {
        if (!matchStarted.Value || matchEnded)
            return;

        if (newState != PlayerState.Muerto || player == null)
            return;

        // Registramos el orden real de eliminación y evitamos duplicados.
        if (!eliminatedPlayers.Contains(player))
            eliminatedPlayers.Add(player);

        if (checkScheduled)
            return;

        checkScheduled = true;
        StartCoroutine(CheckVictoryNextFrame());
    }

    private IEnumerator CheckVictoryNextFrame()
    {
        yield return null;

        checkScheduled = false;

        if (matchEnded)
            yield break;

        registeredPlayers.RemoveAll(p => p == null);

        var alive = registeredPlayers.FindAll(
            p => p.currentState.Value != PlayerState.Muerto
        );

        // ---------- Modo Supervivencia ----------

        if (mode == MatchMode.Survival)
        {
            if (alive.Count == 0)
            {
                matchEnded = true;

                int waveReached = 0;
                float survivedSeconds = 0f;
                float timeToReachWave = 0f;

                if (WaveManager.Instance != null)
                {
                    WaveManager.Instance.EndRun(
                        out waveReached,
                        out survivedSeconds
                    );

                    timeToReachWave =
                        WaveManager.Instance.TimeToReachWaveSeconds;
                }

                AnnounceSurvivalResultClientRpc(
                    waveReached,
                    survivedSeconds,
                    timeToReachWave
                );
            }

            yield break;
        }

        // ---------- Modo Battle Royale ----------

        if (alive.Count == 1)
        {
            matchEnded = true;

            PlayerStateManager winner = alive[0];

            Debug.Log("Se van a teletransportar los jugadores.");

            TeleportPlayersToResultPositions(winner);

            // Comprobamos y activamos el easter egg antes de anunciar
            // los resultados. El resultado indica al cliente ganador
            // si debe omitir la pantalla de resultados.
            bool winnerHasEasterEgg = TryTriggerEasterEgg(winner);

            AnnounceResultClientRpc(
                winner.OwnerClientId,
                winner.NetworkObject,
                false,
                winnerHasEasterEgg
            );
        }
        else if (alive.Count == 0)
        {
            matchEnded = true;

            Debug.Log("La partida terminó en empate.");

            TeleportPlayersToDrawPositions(registeredPlayers);

            AnnounceResultClientRpc(0, default, true, false);
        }
    }

    // ---------- Easter Egg ----------

    private bool TryTriggerEasterEgg(PlayerStateManager winner)
    {
        if (!IsServer || winner == null)
            return false;

        if (mode != MatchMode.BattleRoyale)
            return false;

        if (MapCameraMover.Instance == null)
            return false;

        PlayerBombController bombs =
            winner.GetComponent<PlayerBombController>();

        EasterEggManager egg =
            winner.GetComponent<EasterEggManager>();

        if (bombs == null || egg == null)
            return false;

        // El easter egg solamente se activa si el ganador
        // no colocó ninguna bomba durante la partida.
        if (bombs.BombsPlacedThisMatch > 0)
            return false;

        Debug.Log(
            $"El jugador {winner.OwnerClientId} ganó sin colocar bombas. " +
            "Se activa el easter egg y se omitirá su pantalla de resultados."
        );

        egg.TriggerFromServer();
        return true;
    }

    // ---------- Resultados de Supervivencia ----------

    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceSurvivalResultClientRpc(
        int waveReached,
        float survivedSeconds,
        float timeToReachWave)
    {
        matchEnded = true;

        ActivarResultsCamera();

        if (resultsPanel != null)
            resultsPanel.SetActive(true);

        PlayerData data = PersistenceManager.LoadData();

        bool nuevoRecord = waveReached > 0 && (
            waveReached > data.mejorOleadaSupervivencia ||
            (waveReached == data.mejorOleadaSupervivencia &&
             timeToReachWave < data.mejorTiempoParaOleada)
        );

        if (nuevoRecord)
        {
            data.mejorOleadaSupervivencia = waveReached;
            data.mejorTiempoParaOleada = timeToReachWave;

            PersistenceManager.SaveData(data);
        }

        if (resultsText == null)
            return;

        string lineaRecord = nuevoRecord
            ? "¡Nuevo récord!"
            : $"Mejor: oleada {data.mejorOleadaSupervivencia} en {FormatearTiempo(data.mejorTiempoParaOleada)}";

        resultsText.text =
            $"Derrota\n" +
            $"Oleada alcanzada: {waveReached}\n" +
            $"Tiempo en llegar: {FormatearTiempo(timeToReachWave)}\n" +
            $"Tiempo total: {FormatearTiempo(survivedSeconds)}\n" +
            lineaRecord;

        if (WaveManager.Instance != null)
        {
            SurvivalDifficultyProfile profile =
                WaveManager.Instance.GetProfile(
                    WaveManager.Instance.Difficulty
                );

            if (profile != null)
                resultsText.text += "\nDificultad: " + profile.displayName;
        }
    }

    private static string FormatearTiempo(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }

    // ---------- Resultados de Battle Royale ----------

    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceResultClientRpc(
        ulong winnerClientId,
        NetworkObjectReference winnerRef,
        bool isDraw,
        bool winnerHasEasterEgg)
    {
        matchEnded = true;

        // Si el ganador activó el easter egg, su cliente no debe
        // cambiar a la cámara ni mostrar el panel de resultados.
        // Los demás jugadores sí ven los resultados normalmente.
        bool localPlayerIsEasterEggWinner =
            !isDraw &&
            winnerHasEasterEgg &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.LocalClientId == winnerClientId;

        if (localPlayerIsEasterEggWinner)
        {
            Debug.Log(
                "Ganador del easter egg: se omite la cámara y el panel de resultados."
            );
            return;
        }

        ActivarResultsCamera();

        if (resultsPanel != null)
            resultsPanel.SetActive(true);

        if (resultsText == null)
            return;

        if (isDraw)
            resultsText.text = "¡Empate!";
        else if (NetworkManager.Singleton.LocalClientId == winnerClientId)
            resultsText.text = "¡Ganaste!";
        else
            resultsText.text =
                $"Perdiste. Jugador {winnerClientId} gana la partida.";

        if (!isDraw && winnerRef.TryGet(out NetworkObject winnerObj))
        {
            // Aquí se pueden agregar futuras acciones de celebración.
        }
    }

    // ---------- Posiciones de resultados ----------

    private void TeleportPlayersToResultPositions(
        PlayerStateManager winner)
    {
        if (!IsServer || winner == null)
            return;

        Debug.Log("Intentando teletransportar jugadores a resultados.");

        // El ganador siempre va al punto de victoria.
        TeleportPlayerToPoint(winner, puntoVictoria);

        // Los perdedores se ordenan según el momento en que fueron eliminados.
        Transform[] defeatPoints =
        {
            puntoDerrota1,
            puntoDerrota2,
            puntoDerrota3
        };

        int pointCount = Mathf.Min(
            eliminatedPlayers.Count,
            defeatPoints.Length
        );

        for (int i = 0; i < pointCount; i++)
        {
            PlayerStateManager loser = eliminatedPlayers[i];

            if (loser == null || loser == winner)
                continue;

            TeleportPlayerToPoint(loser, defeatPoints[i]);
        }
    }

    private void TeleportPlayersToDrawPositions(
        List<PlayerStateManager> players)
    {
        if (!IsServer || players == null)
            return;

        Transform[] drawPoints =
        {
            puntoEmpate1,
            puntoEmpate2,
            puntoEmpate3,
            puntoEmpate4
        };

        int assignedCount = 0;

        foreach (PlayerStateManager player in players)
        {
            if (player == null)
                continue;

            // En un empate todos los participantes terminaron muertos.
            if (player.currentState.Value != PlayerState.Muerto)
                continue;

            if (assignedCount >= drawPoints.Length)
            {
                Debug.LogWarning(
                    "Hay más jugadores empatados que puntos configurados."
                );
                break;
            }

            Transform point = drawPoints[assignedCount];

            if (point == null)
            {
                Debug.LogWarning(
                    $"Falta asignar el punto de empate {assignedCount + 1}."
                );

                assignedCount++;
                continue;
            }

            TeleportPlayerToPoint(player, point);
            assignedCount++;
        }

        Debug.Log(
            $"Jugadores asignados a posiciones de empate: {assignedCount}"
        );
    }

    private void TeleportPlayerToPoint(
        PlayerStateManager player,
        Transform point)
    {
        if (player == null || point == null)
            return;

        PlayerSpawnHandler handler =
            player.GetComponent<PlayerSpawnHandler>();

        if (handler == null || !handler.IsSpawned)
        {
            Debug.LogWarning(
                $"No se pudo teletransportar a {player.name}: " +
                "PlayerSpawnHandler inexistente o no está spawneado."
            );

            return;
        }

        // En resultados los jugadores ya no necesitan moverse.
        // Lo desactivamos también en el servidor y luego en todos los clientes.
        CharacterController serverController =
            player.GetComponent<CharacterController>();

        if (serverController != null)
            serverController.enabled = false;

        DisableCharacterControllerClientRpc(player.NetworkObject);

        // Reutilizamos el sistema de animaciones del lobby.
        AssignCharacterAnimation(player, point);

        Debug.Log(
            $"Teletransportando a {player.name} hacia {point.name}"
        );

        // La coroutine espera un frame para dar tiempo a que los clientes
        // desactiven el CharacterController antes de recibir el teletransporte.
        StartCoroutine(
            SendSpawnNextFrame(handler, point.position, point.rotation)
        );
    }

    // ---------- Asignación de animaciones ----------

    private void AssignCharacterAnimation(
        PlayerStateManager player,
        Transform point)
    {
        if (player == null || point == null)
            return;

        LobbySpawnPoint spawnPoint =
            point.GetComponent<LobbySpawnPoint>();

        if (spawnPoint == null)
        {
            Debug.LogWarning(
                $"El punto {point.name} no tiene el componente LobbySpawnPoint."
            );
            return;
        }

        PlayerCharacterVisual visual =
            player.GetComponent<PlayerCharacterVisual>();

        if (visual == null)
        {
            Debug.LogWarning(
                $"El jugador {player.name} no tiene PlayerCharacterVisual."
            );
            return;
        }

        visual.SetLobbyAnimation(spawnPoint.LobbyAnimation);

        Debug.Log(
            $"Animación {spawnPoint.LobbyAnimation} asignada a {player.name}"
        );
    }

    // ---------- Spawns normales ----------

    private void AssignSpawn(PlayerStateManager player)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return;

        var handler = player.GetComponent<PlayerSpawnHandler>();

        if (handler == null)
            return;

        Transform point =
            spawnPoints[nextSpawnIndex % spawnPoints.Length];

        nextSpawnIndex++;

        StartCoroutine(
            SendSpawnNextFrame(handler, point.position, point.rotation)
        );
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void DisableCharacterControllerClientRpc(
        NetworkObjectReference playerReference)
    {
        if (!playerReference.TryGet(out NetworkObject playerObject))
            return;

        CharacterController controller =
            playerObject.GetComponent<CharacterController>();

        if (controller != null)
        {
            controller.enabled = false;

            Debug.Log(
                $"CharacterController desactivado para {playerObject.name} " +
                "durante la pantalla de resultados."
            );
        }
        else
        {
            Debug.LogWarning(
                $"El jugador {playerObject.name} no tiene CharacterController."
            );
        }
    }

    private IEnumerator SendSpawnNextFrame(
        PlayerSpawnHandler handler,
        Vector3 pos,
        Quaternion rot)
    {
        yield return null;

        if (handler != null && handler.IsSpawned)
            handler.TeleportToSpawnRpc(pos, rot);
    }

    // ---------- Cámaras ----------

    private void ActivarLobbyCamera()
    {
        if (lobbyCamera != null)
            lobbyCamera.gameObject.SetActive(true);

        if (mainCamera != null)
            mainCamera.gameObject.SetActive(false);

        if (resultsCamera != null)
            resultsCamera.gameObject.SetActive(false);
    }

    private void ActivarMainCamera()
    {
        if (lobbyCamera != null)
            lobbyCamera.gameObject.SetActive(false);

        if (mainCamera != null)
            mainCamera.gameObject.SetActive(true);

        if (resultsCamera != null)
            resultsCamera.gameObject.SetActive(false);
    }

    private void ActivarResultsCamera()
    {
        Debug.Log("Se activó la cámara de resultados.");

        if (lobbyCamera != null)
            lobbyCamera.gameObject.SetActive(false);

        if (mainCamera != null)
            mainCamera.gameObject.SetActive(false);

        if (resultsCamera != null)
            resultsCamera.gameObject.SetActive(true);
    }

    // ---------- Spawns del lobby ----------

    private void AssignLobbySpawn(PlayerStateManager player)
    {
        if (lobbySpawnPoints == null || lobbySpawnPoints.Length == 0)
            return;

        Transform point = lobbySpawnPoints[
            nextLobbySpawnIndex % lobbySpawnPoints.Length
        ];

        nextLobbySpawnIndex++;

        // El lobby también utiliza el método compartido.
        AssignCharacterAnimation(player, point);

        PlayerSpawnHandler handler =
            player.GetComponent<PlayerSpawnHandler>();

        if (handler == null)
            return;

        StartCoroutine(
            SendSpawnNextFrame(handler, point.position, point.rotation)
        );
    }
}
