using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    // Se pone en true cuando arranca la partida: lo usa el ConnectionApproval para rechazar late joiners
    public static bool JoinsClosed { get; private set; }

    public const int MaxPlayers = 6;
    private const int MinPlayersToStart = 2;

    [Header("Resultados")]
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TMP_Text resultsText;

    [Header("Inicio de partida (solo host)")]
    [SerializeField] private Button startMatchButton;
    [SerializeField] private GameObject waitingText;   // "Esperando jugadores..."
    [SerializeField] private GameObject waitingForHostText; // Texto para clientes: "Esperando al host..."
    
    [Header("Spawns")]
    [SerializeField] private Transform[] spawnPoints;
    private int nextSpawnIndex;

    private readonly NetworkVariable<bool> matchStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly List<PlayerStateManager> registeredPlayers = new List<PlayerStateManager>();
    private bool matchEnded;
    private bool checkScheduled;

    public bool IsMatchRunning => matchStarted.Value && !matchEnded;

        private void Awake()
    {
        Instance = this;

        // Arrancan ocultos; solo el host los activa cuando el MatchManager aparece en red
                if (startMatchButton != null) startMatchButton.gameObject.SetActive(false);
        if (waitingText != null) waitingText.SetActive(false);
        if (waitingForHostText != null) waitingForHostText.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        JoinsClosed = false;
        if (resultsPanel != null) resultsPanel.SetActive(false);

        matchStarted.OnValueChanged += OnMatchStartedChanged;

              if (startMatchButton != null)
        {
            startMatchButton.onClick.AddListener(OnStartButtonClicked);
            startMatchButton.gameObject.SetActive(IsHost && !matchStarted.Value);
            startMatchButton.interactable = false;
        }
        if (waitingText != null) waitingText.SetActive(IsHost && !matchStarted.Value);
        if (waitingForHostText != null) waitingForHostText.SetActive(!IsHost && !matchStarted.Value);
        if (IsServer)
        {
            foreach (var p in FindObjectsByType<PlayerStateManager>(FindObjectsSortMode.None))
                RegisterPlayer(p);
        }
    }

    public override void OnNetworkDespawn()
    {
        matchStarted.OnValueChanged -= OnMatchStartedChanged;
        if (startMatchButton != null) startMatchButton.onClick.RemoveListener(OnStartButtonClicked);
        JoinsClosed = false;
        if (Instance == this) Instance = null;
    }

    private void OnMatchStartedChanged(bool oldValue, bool newValue)
    {
        if (!newValue) return;
        if (startMatchButton != null) startMatchButton.gameObject.SetActive(false);
        if (waitingText != null) waitingText.SetActive(false);
        if (waitingForHostText != null) waitingForHostText.SetActive(false);
    }

    // ---------- Registro de jugadores (solo servidor) ----------
    public void RegisterPlayer(PlayerStateManager player)
    {
        if (!IsServer || player == null || registeredPlayers.Contains(player)) return;

        registeredPlayers.Add(player);
        AssignSpawn(player);
        player.currentState.OnValueChanged += (_, newState) => OnPlayerStateChanged(newState);
        RefreshStartButton();
    }

    private int ConnectedPlayersCount()
    {
        registeredPlayers.RemoveAll(p => p == null);
        return registeredPlayers.Count;
    }

    private void RefreshStartButton()
    {
        if (startMatchButton == null || matchStarted.Value) return;
        startMatchButton.interactable = ConnectedPlayersCount() >= MinPlayersToStart;
    }

    // ---------- Inicio de partida ----------
    private void OnStartButtonClicked()
    {
        if (!IsServer) return; // el host es el servidor
        StartMatch();
    }

    private void StartMatch()
    {
        if (matchStarted.Value || ConnectedPlayersCount() < MinPlayersToStart) return;
        matchStarted.Value = true;
        JoinsClosed = true;
    }

    // ---------- Victoria ----------
    private void OnPlayerStateChanged(PlayerState newState)
    {
        if (!matchStarted.Value || matchEnded || checkScheduled) return;
        if (newState != PlayerState.Muerto) return;
        checkScheduled = true;
        StartCoroutine(CheckVictoryNextFrame());
    }

    private IEnumerator CheckVictoryNextFrame()
    {
        yield return null;
        checkScheduled = false;
        if (matchEnded) yield break;

        registeredPlayers.RemoveAll(p => p == null);
        var alive = registeredPlayers.FindAll(p => p.currentState.Value != PlayerState.Muerto);

        if (alive.Count == 1)
        {
            matchEnded = true;
            AnnounceResultClientRpc(alive[0].OwnerClientId, alive[0].NetworkObject, false);
        }
        else if (alive.Count == 0)
        {
            matchEnded = true;
            AnnounceResultClientRpc(0, default, true);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceResultClientRpc(ulong winnerClientId, NetworkObjectReference winnerRef, bool isDraw)
    {
        matchEnded = true; // para que IsMatchRunning sea false también en los clientes
        if (resultsPanel != null) resultsPanel.SetActive(true);
        if (resultsText == null) return;

        if (isDraw) resultsText.text = "¡Empate!";
        else if (NetworkManager.Singleton.LocalClientId == winnerClientId) resultsText.text = "¡Ganaste!";
        else resultsText.text = $"Perdiste. Jugador {winnerClientId} gana la partida.";

        if (!isDraw && winnerRef.TryGet(out NetworkObject winnerObj))
        {
            // Hook para la animación de celebración de tus compañeros
        }
    }

        private void AssignSpawn(PlayerStateManager player)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        var handler = player.GetComponent<PlayerSpawnHandler>();
        if (handler == null) return;

        Transform point = spawnPoints[nextSpawnIndex % spawnPoints.Length];
        nextSpawnIndex++;

        StartCoroutine(SendSpawnNextFrame(handler, point.position, point.rotation));
    }

    private IEnumerator SendSpawnNextFrame(PlayerSpawnHandler handler, Vector3 pos, Quaternion rot)
    {
        yield return null; // espera un frame a que el avatar esté listo en red
        if (handler != null && handler.IsSpawned)
            handler.TeleportToSpawnRpc(pos, rot);
    }
}