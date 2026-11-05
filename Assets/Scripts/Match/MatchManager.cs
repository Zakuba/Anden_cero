
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

    [Header("Spawns por equipo (2 vs 2)")]
    [SerializeField] private Transform[] spawnPointsAlfa;
    [SerializeField] private Transform[] spawnPointsBeta;
    private int nextSpawnAlfa;
    private int nextSpawnBeta;

   
    // Copias mezcladas de los puntos de spawn (se rehacen en cada partida)
    private List<Transform> spawnsMezclados = new List<Transform>();
    private List<Transform> alfaMezclados = new List<Transform>();
    private List<Transform> betaMezclados = new List<Transform>();

    private static List<Transform> Mezclar(Transform[] origen)
    {
    var lista = new List<Transform>();
    if (origen == null) return lista;

    foreach (var t in origen)
        if (t != null) lista.Add(t);

    // Fisher-Yates
    for (int i = lista.Count - 1; i > 0; i--)
    {
        int j = Random.Range(0, i + 1);
        (lista[i], lista[j]) = (lista[j], lista[i]);
    }
    return lista;
}


    [Header("Layouts de cajas")]
[SerializeField] private GameObject[] layoutsCajas;

private readonly NetworkVariable<int> layoutCajasIndex =
    new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private static int ultimoLayoutCajas = -1;

private void OnLayoutCajasChanged(int anterior, int nuevo)
{
    AplicarLayoutCajas(nuevo);
}

private void AplicarLayoutCajas(int indice)
{
    if (layoutsCajas == null || indice < 0) return;
    Debug.Log($"[Cajas] Layout activo: {indice}");

    for (int i = 0; i < layoutsCajas.Length; i++)
    {
        if (layoutsCajas[i] != null)
            layoutsCajas[i].SetActive(i == indice);
    }
}



    [Header("Spawns de resultados (Battle Royale)")]
    [SerializeField] private Transform puntoVictoria;
    [SerializeField] private Transform puntoVictoria2; // segundo lugar del podio para el equipo ganador (2 vs 2)
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

    public const int MaxPorEquipo = 2;

    [Header("Equipos (2 vs 2)")]
    [SerializeField] private bool modoEquipos = false;
    public bool ModoEquipos => modoEquipos;

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

layoutCajasIndex.OnValueChanged += OnLayoutCajasChanged;

if (IsServer && layoutsCajas != null && layoutsCajas.Length > 0)
{
    int nuevo = Random.Range(0, layoutsCajas.Length);

    // Si hay más de uno, evitamos repetir el layout anterior
    if (layoutsCajas.Length > 1 && nuevo == ultimoLayoutCajas)
        nuevo = (nuevo + 1 + Random.Range(0, layoutsCajas.Length - 1)) % layoutsCajas.Length;

    ultimoLayoutCajas = nuevo;
    layoutCajasIndex.Value = nuevo;
}

AplicarLayoutCajas(layoutCajasIndex.Value);
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

    // Equipos: escuchar cambios y limpiar al desconectarse un cliente.
    equiposTexto.OnValueChanged += OnEquiposTextoChanged;
if (!IsServer)
    ReconstruirEquipos(equiposTexto.Value.ToString()); // para quien entra con el estado ya armado
    if (IsServer)
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnectedEquipos;

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

layoutCajasIndex.OnValueChanged -= OnLayoutCajasChanged;
    equiposTexto.OnValueChanged -= OnEquiposTextoChanged;
    if (NetworkManager.Singleton != null)
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedEquipos;

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
        AsignarEquipoInicial(player.OwnerClientId);
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
        ConnectedPlayersCount() >= MinPlayersToStart &&
        EquiposListosParaIniciar();
}

// ---------- Equipos ----------

// Fuente de verdad en el servidor; en los clientes es una copia reconstruida.
private readonly Dictionary<ulong, int> equipos = new Dictionary<ulong, int>();

// El servidor publica todo el estado como un solo texto: "clientId:equipo,clientId:equipo"
private readonly NetworkVariable<Unity.Collections.FixedString128Bytes> equiposTexto =
    new NetworkVariable<Unity.Collections.FixedString128Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

public event System.Action EquiposChanged;

public int GetEquipo(ulong clientId)
{
    return equipos.TryGetValue(clientId, out int e) ? e : -1;
}

public int ContarEquipo(int equipo)
{
    int n = 0;
    foreach (var kv in equipos)
        if (kv.Value == equipo) n++;
    return n;
}

public List<ulong> ClientesDeEquipo(int equipo)
{
    var lista = new List<ulong>();
    foreach (var kv in equipos)
        if (kv.Value == equipo) lista.Add(kv.Key);
    return lista;
}

// Devuelve true si los dos jugadores están en el mismo equipo (incluye comparar a un jugador consigo mismo).
// Solo tiene efecto en el modo equipos; en los demás mapas siempre devuelve false.
public bool EsMismoEquipo(ulong clientA, ulong clientB)
{
    if (!modoEquipos) return false;

    int a = GetEquipo(clientA);
    int b = GetEquipo(clientB);

    return a != -1 && a == b;
}

// En el modo equipos hace falta al menos un jugador en cada equipo para iniciar.
// En los demás mapas siempre devuelve true, así que nada cambia ahí.
private bool EquiposListosParaIniciar()
{
    if (!modoEquipos) return true;

    return ContarEquipo(0) >= 1 && ContarEquipo(1) >= 1;
}

// Solo servidor: escribe el estado completo y avisa a la interfaz.
private void PublicarEquipos()
{
    var sb = new System.Text.StringBuilder();
    foreach (var kv in equipos)
    {
        if (sb.Length > 0) sb.Append(',');
        sb.Append(kv.Key).Append(':').Append(kv.Value);
    }
    equiposTexto.Value = sb.ToString();
    EquiposChanged?.Invoke();
    RefreshStartButton(); // <-- nueva
}

// En los clientes: reconstruye la copia local desde el texto.
private void ReconstruirEquipos(string texto)
{
    equipos.Clear();
    if (!string.IsNullOrEmpty(texto))
    {
        foreach (string par in texto.Split(','))
        {
            string[] p = par.Split(':');
            if (p.Length == 2 && ulong.TryParse(p[0], out ulong id) && int.TryParse(p[1], out int eq))
                equipos[id] = eq;
        }
    }
    EquiposChanged?.Invoke();
}

private void OnEquiposTextoChanged(Unity.Collections.FixedString128Bytes anterior,
                                   Unity.Collections.FixedString128Bytes nuevo)
{
    if (IsServer) return; // el servidor ya tiene el estado correcto
    ReconstruirEquipos(nuevo.ToString());
}

// Solo servidor: al entrar, va al equipo con menos jugadores.
private void AsignarEquipoInicial(ulong clientId)
{
    if (!IsServer || !modoEquipos || equipos.ContainsKey(clientId)) return;

    int equipo = ContarEquipo(0) <= ContarEquipo(1) ? 0 : 1;
    equipos[clientId] = equipo;
    PublicarEquipos();
    Debug.Log($"[Equipos] Cliente {clientId} -> {(Equipo)equipo}");
}

// El jugador pide cambiar de equipo; el servidor valida.
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
public void CambiarEquipoRpc(int equipo, RpcParams rpcParams = default)
{
    if (!modoEquipos) return;

    ulong clientId = rpcParams.Receive.SenderClientId;

    if (matchStarted.Value) return;                  // ya empezó
    if (equipo < 0 || equipo > 1) return;            // valor inválido
    if (!equipos.ContainsKey(clientId)) return;      // jugador sin equipo
    if (equipos[clientId] == equipo) return;         // ya está ahí
    if (ContarEquipo(equipo) >= MaxPorEquipo)        // equipo lleno
    {
        Debug.Log($"[Equipos] {(Equipo)equipo} está lleno, rechazado el cambio de {clientId}");
        return;
    }

    equipos[clientId] = equipo;
    PublicarEquipos();
    Debug.Log($"[Equipos] Cliente {clientId} cambió a {(Equipo)equipo}");
}

private void OnClientDisconnectedEquipos(ulong clientId)
{
    if (equipos.Remove(clientId))
        PublicarEquipos();

    // Si se fue en plena partida, lo sacamos de la lista
    // y revisamos si ya quedó un ganador.
    registeredPlayers.RemoveAll(p => p == null || p.OwnerClientId == clientId);

    if (matchStarted.Value && !matchEnded && !checkScheduled)
    {
        checkScheduled = true;
        StartCoroutine(CheckVictoryNextFrame());
    }
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

    if (!EquiposListosParaIniciar())
    {
        Debug.Log("[Equipos] No se puede iniciar: falta al menos un jugador en cada equipo.");
        return;
    }

        JoinsClosed = true;
        eliminatedPlayers.Clear();
        nextSpawnAlfa = 0;
        nextSpawnBeta = 0;
        nextSpawnIndex = 0;
        spawnsMezclados = Mezclar(spawnPoints);
        alfaMezclados = Mezclar(spawnPointsAlfa);
        betaMezclados = Mezclar(spawnPointsBeta);


        matchEnded = false;
        checkScheduled = false;

        // Primero marcamos la partida como iniciada.
        matchStarted.Value = true;

        foreach (PlayerStateManager player in registeredPlayers)
        {
            if (player == null)
                continue;

            // Reiniciamos el rastreador de daño para la nueva partida
            player.ResetMatchDamageTracker();    

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
        // ---------- Modo 2 vs 2 (por equipos) ----------

if (modoEquipos)
{
    bool hayAlfa = false;
    bool hayBeta = false;

    foreach (PlayerStateManager p in alive)
    {
        int e = GetEquipo(p.OwnerClientId);
        if (e == 0) hayAlfa = true;
        else if (e == 1) hayBeta = true;
    }

    // Siguen vivos jugadores de los dos equipos: la partida continúa
    if (hayAlfa && hayBeta)
        yield break;

    // Quedan vivos de un solo equipo: ese equipo gana
    if (hayAlfa || hayBeta)
    {
        matchEnded = true;

        int equipoGanador = hayAlfa ? 0 : 1;
        Debug.Log($"[Equipos] Gana el equipo {(Equipo)equipoGanador}");

        // Logro "Superviviente invencible" para los ganadores vivos que no recibieron daño
        foreach (PlayerStateManager p in alive)
        {
            if (!p.HasTakenDamageThisMatch)
                p.AwardInvincibleSurvivorAchievementClientRpc();
        }

        TeleportPlayersToTeamResultPositions(equipoGanador);
        AnnounceTeamResultClientRpc(equipoGanador);
        yield break;
    }

    // Si no quedó nadie vivo, sigue el código de empate de abajo
}

        if (alive.Count == 1)
        {
            matchEnded = true;

            PlayerStateManager winner = alive[0];

            Debug.Log("Se van a teletransportar los jugadores.");

            // VALIDACIÓN DEL LOGRO "SUPERVIVIENTE INVENCIBLE":
            // Si el ganador no recibió daño en toda la partida, se le otorga el logro
            if (!winner.HasTakenDamageThisMatch)
            {
                winner.AwardInvincibleSurvivorAchievementClientRpc();
            }

            // Primero comprobamos el easter egg. Si se activa, el jugador real
            // no va al podio: en su lugar queda la copia visual.
            bool winnerHasEasterEgg = TryTriggerEasterEgg(winner);

TeleportPlayersToResultPositions(winner, winnerHasEasterEgg);

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

        egg.TriggerFromServer(puntoVictoria);
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

        // GUARDADO DE ESTADÍSTICAS ACUMULADAS
        GuardarEstadisticasPartidaLocal();

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

        // GUARDADO DE ESTADÍSTICAS ACUMULADAS
        GuardarEstadisticasPartidaLocal();

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

    [Rpc(SendTo.ClientsAndHost)]
private void AnnounceTeamResultClientRpc(int equipoGanador)
{
    matchEnded = true;

    // GUARDADO DE ESTADÍSTICAS ACUMULADAS
    GuardarEstadisticasPartidaLocal();

    ActivarResultsCamera();

    if (resultsPanel != null)
        resultsPanel.SetActive(true);

    if (resultsText == null)
        return;

    string nombreGanador = ((Equipo)equipoGanador).ToString();
    int miEquipo = GetEquipo(NetworkManager.Singleton.LocalClientId);

    resultsText.text = miEquipo == equipoGanador
        ? $"¡Ganó tu equipo!\nEquipo {nombreGanador}"
        : $"Perdió tu equipo.\nGana el equipo {nombreGanador}";
}

    // ---------- Posiciones de resultados ----------

    private void TeleportPlayersToResultPositions(
    PlayerStateManager winner,
    bool saltarGanador = false)
    {
        if (!IsServer || winner == null)
            return;

        Debug.Log("Intentando teletransportar jugadores a resultados.");

        // El ganador va al punto de victoria, salvo que haya activado el easter egg.
        if (!saltarGanador)
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

    private void TeleportPlayersToTeamResultPositions(int equipoGanador)
{
    if (!IsServer) return;

    // Los dos del equipo ganador (vivos o caídos) van al podio de victoria
    Transform[] winnerPoints =
    {
        puntoVictoria,
        puntoVictoria2 != null ? puntoVictoria2 : puntoVictoria
    };

    int w = 0;
    foreach (PlayerStateManager p in registeredPlayers)
    {
        if (p == null || GetEquipo(p.OwnerClientId) != equipoGanador)
            continue;

        TeleportPlayerToPoint(p, winnerPoints[Mathf.Min(w, winnerPoints.Length - 1)]);
        w++;
    }

    // El equipo perdedor va a los puntos de derrota, en orden de eliminación
    Transform[] defeatPoints = { puntoDerrota1, puntoDerrota2, puntoDerrota3 };

    int l = 0;
    foreach (PlayerStateManager p in eliminatedPlayers)
    {
        if (p == null || GetEquipo(p.OwnerClientId) == equipoGanador)
            continue;

        if (l >= defeatPoints.Length) break;

        TeleportPlayerToPoint(p, defeatPoints[l]);
        l++;
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
    var handler = player.GetComponent<PlayerSpawnHandler>();

    if (handler == null)
        return;

    Transform point = null;

    // Modo equipos: cada equipo aparece en su lado, en puntos al azar
    if (modoEquipos)
    {
        int equipo = GetEquipo(player.OwnerClientId);

        if (equipo == 0 && alfaMezclados.Count > 0)
        {
            point = alfaMezclados[nextSpawnAlfa % alfaMezclados.Count];
            nextSpawnAlfa++;
        }
        else if (equipo == 1 && betaMezclados.Count > 0)
        {
            point = betaMezclados[nextSpawnBeta % betaMezclados.Count];
            nextSpawnBeta++;
        }
    }

    // Resto de mapas: todos los puntos mezclados
    if (point == null)
    {
        if (spawnsMezclados.Count == 0)
            return;

        point = spawnsMezclados[nextSpawnIndex % spawnsMezclados.Count];
        nextSpawnIndex++;
    }

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

    /// <summary>
    /// Registra la partida completada y suma el tiempo sobrevivido en el perfil local (JSON).
    /// </summary>
    private void GuardarEstadisticasPartidaLocal()
    {
        float tiempoSobrevivido = 0f;

        // Buscamos al jugador local para extraer el tiempo que sobrevivió
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayerObj != null)
            {
                PlayerStateManager localState = localPlayerObj.GetComponent<PlayerStateManager>();
                if (localState != null)
                {
                    tiempoSobrevivido = localState.FinalizarConteoSupervivencia();
                }
            }
        }

        // Carga, suma acumulativa y guardado en JSON local
        PlayerData data = PersistenceManager.LoadData();
        data.partidasJugadas++;
        data.tiempoTotalSobrevivido += tiempoSobrevivido;

        PersistenceManager.SaveData(data);

        Debug.Log($"[Estadísticas] Guardadas: Partidas={data.partidasJugadas}, TiempoTotal={data.tiempoTotalSobrevivido:F1}s (+{tiempoSobrevivido:F1}s)");
    }
}
