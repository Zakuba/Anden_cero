using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public enum WavePhase
{
    Idle,      // esperando que el host inicie la partida
    Rest,      // descanso entre oleadas (con cuenta regresiva)
    Combat,    // oleada en curso
    Finished   // la partida terminó
}

/// <summary>
/// Reglas:
/// - La oleada avanza únicamente cuando no quedan drones vivos ni en cola.
/// - Cantidad por oleada: base + (oleada - 1) * incremento.
/// - Hay un tope de drones vivos a la vez; el resto espera en cola.
/// - El Sanador es aliado: no cuenta para completar la oleada y se retira
///   cuando esta termina.
/// </summary>
public class WaveManager : NetworkBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Prefabs de drones (NetworkObject + WaveEnemy, registrados en NetworkManager)")]
    [SerializeField] private GameObject bomberPrefab;
    [SerializeField] private GameObject shooterPrefab;
    [SerializeField] private GameObject healerPrefab; // opcional

    [Header("Puntos de aparición")]
    [Tooltip("Sobre el NavMesh y en múltiplos de 5 en X/Z, así son centro de celda para todos los drones.")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("Distancia mínima a un jugador vivo. El Fusilero ve a 12, así que conviene más que eso.")]
    [SerializeField] private float minSpawnDistanceToPlayers = 14f;
    [SerializeField] private float navMeshSampleRadius = 1.5f;

    [Header("Dificultad")]
    [Tooltip("Los tres perfiles (Fácil, Normal y Difícil). El orden no importa: se buscan por su campo Level.")]
    [SerializeField] private SurvivalDifficultyProfile[] difficultyProfiles;
    [Tooltip("Dificultad que se usa si no se eligió ninguna (por ejemplo, al abrir la escena directo desde el Editor).")]
    [SerializeField] private DifficultyLevel fallbackDifficulty = DifficultyLevel.Normal;

    [Header("Tiempos")]
    [SerializeField] private float firstWaveDelay = 5f;
    // Cantidad por oleada, tope, intervalos, composición, Sanador y descanso salen del perfil de dificultad.
    // La velocidad de los drones es fija: no cambia con la oleada.

    // ---------- Estado replicado (escribe solo el servidor) ----------
    private readonly NetworkVariable<int> currentWave = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<WavePhase> phase = new NetworkVariable<WavePhase>(
        WavePhase.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Drones vivos en este momento
    private readonly NetworkVariable<int> enemiesAlive = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Drones vivos + los que esperan en la cola (lo que falta para terminar la oleada)
    private readonly NetworkVariable<int> enemiesRemaining = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Instante (ServerTime) en que termina el descanso. Se replica una vez y
    // cada cliente calcula la cuenta regresiva localmente.
    private readonly NetworkVariable<double> phaseEndServerTime = new NetworkVariable<double>(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Instante (ServerTime) en que empezó la partida y en que terminó (0 = en curso).
    private readonly NetworkVariable<double> runStartServerTime = new NetworkVariable<double>(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<double> runEndServerTime = new NetworkVariable<double>(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Dificultad de esta partida: la fija el servidor al nacer y no vuelve a cambiar.
    private readonly NetworkVariable<DifficultyLevel> difficulty = new NetworkVariable<DifficultyLevel>(
        DifficultyLevel.Normal, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int CurrentWave => currentWave.Value;
    public WavePhase Phase => phase.Value;
    public int EnemiesAlive => enemiesAlive.Value;
    public int EnemiesRemaining => enemiesRemaining.Value;
    public double PhaseEndServerTime => phaseEndServerTime.Value;

    /// <summary>Dificultad de la partida (la misma en el host y en los clientes).</summary>
    public DifficultyLevel Difficulty => difficulty.Value;

    /// <summary>Vidas máximas de cada jugador según la dificultad (solo servidor).</summary>
    public int PlayerMaxLives => activeProfile != null ? activeProfile.playerMaxLives : 3;

    /// <summary>
    /// Perfil de una dificultad, buscado por su campo Level. Funciona en el host y en los clientes.
    /// Devuelve null si falta.
    /// </summary>
    public SurvivalDifficultyProfile GetProfile(DifficultyLevel level)
    {
        if (difficultyProfiles == null) return null;

        foreach (SurvivalDifficultyProfile profile in difficultyProfiles)
        {
            if (profile != null && profile.level == level) return profile;
        }

        return null;
    }

        /// <summary>Tiempo de partida en segundos (en vivo; se congela al terminar).</summary>
    public float SurvivedSeconds
    {
        get
        {
            if (runStartServerTime.Value <= 0d) return 0f;

            double end = runEndServerTime.Value > 0d
                ? runEndServerTime.Value
                : NetworkManager.ServerTime.Time;

            return Mathf.Max(0f, (float)(end - runStartServerTime.Value));
        }
    }

    // ---------- Estado solo del servidor ----------
    private readonly Queue<GameObject> spawnQueue = new Queue<GameObject>();
    private readonly HashSet<WaveEnemy> activeEnemies = new HashSet<WaveEnemy>();
    private readonly HashSet<WaveEnemy> activeHealers = new HashSet<WaveEnemy>();
    private readonly List<Vector3> alivePlayerPositions = new List<Vector3>();
    private readonly List<Vector3> validPoints = new List<Vector3>();

    private SurvivalDifficultyProfile activeProfile; // perfil de la dificultad elegida (solo servidor)
    private float spawnTimer;
    private float restEndTime;

    private bool runStarted;
    private float runStartTime;
    private int frozenWaveReached;
    private float frozenSurvivedSeconds;

    // Segundo de la partida en que empezó la oleada actual (para el récord)
    private float currentWaveStartSeconds;
    private float frozenTimeToReachWave;

    /// <summary>Segundos que tardó el equipo en llegar a la oleada alcanzada. Válido tras terminar la partida (servidor).</summary>
    public float TimeToReachWaveSeconds => frozenTimeToReachWave;

    public override void OnNetworkSpawn()
    {
        Instance = this;

        if (!IsServer) return;

        if (!ValidateSetup())
        {
            Debug.LogError("[WaveManager] Configuración incompleta: el spawner queda desactivado.");
            enabled = false;
            return;
        }

        SelectDifficulty();
    }

    // La dificultad la decide el servidor una sola vez, al nacer, y no se vuelve a escribir.
    private void SelectDifficulty()
    {
        // La elegida en el menú del host; si no hubo elección (por ejemplo, escena abierta
        // directo desde el Editor), el valor de respaldo del Inspector.
        DifficultyLevel chosen = MatchSettings.HasDifficulty
            ? MatchSettings.Difficulty
            : fallbackDifficulty;

        activeProfile = GetProfile(chosen);
        difficulty.Value = chosen;

        Debug.Log("[WaveManager] Dificultad de la partida: " + chosen);
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned) return;

        bool running = MatchManager.Instance != null && MatchManager.Instance.IsMatchRunning;

        switch (phase.Value)
        {
            case WavePhase.Idle:
                if (running) StartRun();
                break;

            case WavePhase.Rest:
                if (!running) { FinishRun(); break; }
                if (Time.time >= restEndTime) StartWave();
                break;

            case WavePhase.Combat:
                if (!running) { FinishRun(); break; }
                UpdateCombat();
                break;
        }
    }

    // ---------- Ciclo de la partida ----------
    private void StartRun()
    {
        runStarted = true;
        runStartTime = Time.time;
        runStartServerTime.Value = NetworkManager.ServerTime.Time;
        runEndServerTime.Value = 0d;
        currentWaveStartSeconds = 0f;
        frozenTimeToReachWave = 0f;
        // Vidas máximas de los jugadores según la dificultad
        foreach (PlayerStateManager player in FindObjectsByType<PlayerStateManager>(FindObjectsSortMode.None))
            player.ApplyMaxLives(PlayerMaxLives);

        currentWave.Value = 1;
        BeginRest(firstWaveDelay);
    }

    private void BeginRest(float duration)
    {
        phase.Value = WavePhase.Rest;
        restEndTime = Time.time + duration;
        phaseEndServerTime.Value = NetworkManager.ServerTime.Time + duration;
        RefreshCounters();
    }

    private void StartWave()
    {
        int wave = currentWave.Value;

        int total = WaveCalculator.TotalEnemies(wave, activeProfile.baseDronesPerWave, activeProfile.dronesPerWaveIncrement);
        WaveCalculator.Composition(
            total, wave,
            activeProfile.shooterRatioStart, activeProfile.shooterRatioStep, activeProfile.shooterRatioMax,
            out int shooters, out int bombers);

        var composition = new List<GameObject>(total);
        for (int i = 0; i < shooters; i++) composition.Add(shooterPrefab);
        for (int i = 0; i < bombers; i++) composition.Add(bomberPrefab);
        Shuffle(composition);

        spawnQueue.Clear();
        foreach (GameObject prefab in composition) spawnQueue.Enqueue(prefab);

        spawnTimer = 0f;

        currentWaveStartSeconds = Time.time - runStartTime;

        phase.Value = WavePhase.Combat;

        // El Sanador no entra en la cola ni en el tope: aparece una vez al empezar la oleada.
        if (healerPrefab != null && activeHealers.Count == 0 &&
            WaveCalculator.HasHealer(wave, activeProfile.healerFirstWave, activeProfile.healerEveryNWaves))
        {
            TrySpawn(healerPrefab);
        }

        RefreshCounters();
    }

    private void UpdateCombat()
    {
        spawnTimer -= Time.deltaTime;

        if (spawnQueue.Count > 0 && spawnTimer <= 0f)
        {
            spawnTimer = activeProfile.spawnInterval; // el tope se reevalúa cada spawnInterval

            if (activeEnemies.Count < CurrentCap() && TrySpawn(spawnQueue.Peek()))
            {
                spawnQueue.Dequeue();
                RefreshCounters();
            }
        }

        // La oleada termina solo cuando no quedan drones vivos ni en cola.
        if (spawnQueue.Count == 0 && activeEnemies.Count == 0)
            EndWave();
    }

    private void EndWave()
    {
        // El Sanador se retira al terminar la oleada.
        var healers = new List<WaveEnemy>(activeHealers);
        activeHealers.Clear();
        foreach (WaveEnemy healer in healers) DespawnEnemy(healer);

        currentWave.Value++;
        BeginRest(activeProfile.restDuration);
    }

    private void FinishRun()
    {
        if (phase.Value == WavePhase.Finished) return;

        if (runStarted)
        {
            // Si cayeron durante el descanso, la oleada alcanzada es la anterior.
            frozenWaveReached = phase.Value == WavePhase.Combat
                ? currentWave.Value
                : Mathf.Max(0, currentWave.Value - 1);
            frozenSurvivedSeconds = Time.time - runStartTime;
            frozenTimeToReachWave = currentWaveStartSeconds;

            
            runEndServerTime.Value = NetworkManager.ServerTime.Time;
        }

        DespawnAllEnemies();
        phase.Value = WavePhase.Finished;
        RefreshCounters();
    }

    /// <summary>
    /// Lo llama MatchManager (servidor) cuando todos los jugadores murieron.
    /// Cierra la partida y devuelve la oleada alcanzada y el tiempo sobrevivido.
    /// </summary>
    public void EndRun(out int waveReached, out float survivedSeconds)
    {
        if (IsServer && IsSpawned) FinishRun();

        waveReached = frozenWaveReached;
        survivedSeconds = frozenSurvivedSeconds;
    }

    // ---------- Seguimiento de enemigos ----------
    public void NotifyEnemyDespawned(WaveEnemy enemy)
    {
        if (!IsServer) return;

        activeEnemies.Remove(enemy);
        activeHealers.Remove(enemy);
        RefreshCounters();
    }

    private void RefreshCounters()
    {
        if (!IsServer || !IsSpawned) return;

        enemiesAlive.Value = activeEnemies.Count;
        enemiesRemaining.Value = activeEnemies.Count + spawnQueue.Count;
    }

    private void DespawnAllEnemies()
    {
        spawnQueue.Clear();

        var snapshot = new List<WaveEnemy>(activeEnemies);
        snapshot.AddRange(activeHealers);
        activeEnemies.Clear();
        activeHealers.Clear();

        foreach (WaveEnemy enemy in snapshot) DespawnEnemy(enemy);
    }

    private static void DespawnEnemy(WaveEnemy enemy)
    {
        if (enemy == null) return;

        NetworkObject netObj = enemy.NetworkObject;
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);
    }

    // ---------- Spawn ----------
    private bool TrySpawn(GameObject prefab)
    {
        if (prefab == null) return false;
        if (!TryPickSpawnPosition(out Vector3 position)) return false;

        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        NetworkObject netObj = instance.GetComponent<NetworkObject>();
        WaveEnemy enemy = instance.GetComponent<WaveEnemy>();

        if (netObj == null || enemy == null)
        {
            Debug.LogError("[WaveManager] El prefab " + prefab.name + " necesita NetworkObject y WaveEnemy.");
            Destroy(instance);
            return false;
        }

        netObj.Spawn(true);

        if (enemy.CountsForWave) activeEnemies.Add(enemy);
        else activeHealers.Add(enemy);

        ApplyDifficulty(instance);
        return true;
    }

    // Pasa al dron los valores de la dificultad elegida. El Sanador no cambia con la dificultad.
    private void ApplyDifficulty(GameObject instance)
    {
        DroneDifficultyParams parameters = activeProfile.ToDroneParams();

        if (instance.TryGetComponent(out DroneShooterAI shooter))
            shooter.ApplyDifficulty(parameters);
        else if (instance.TryGetComponent(out DroneBomberAI bomber))
            bomber.ApplyDifficulty(parameters);
    }

    private bool TryPickSpawnPosition(out Vector3 position)
    {
        position = default;
        if (spawnPoints == null || spawnPoints.Length == 0) return false;

        RefreshAlivePlayers();
        validPoints.Clear();

        bool anyOnNavMesh = false;
        float bestDistance = -1f;
        Vector3 bestPoint = default;

        foreach (Transform spawnPoint in spawnPoints)
        {
            if (spawnPoint == null) continue;

            // Los drones usan NavMeshAgent: un punto fuera del NavMesh da error al instanciar.
            if (!NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                continue;

            anyOnNavMesh = true;
            float distance = MinHorizontalDistanceToPlayers(hit.position);

            if (distance >= minSpawnDistanceToPlayers) validPoints.Add(hit.position);

            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestPoint = hit.position;
            }
        }

        if (!anyOnNavMesh) return false;

        // Si ningún punto cumple la distancia mínima, usa el más lejano a los jugadores.
        position = validPoints.Count > 0
            ? validPoints[Random.Range(0, validPoints.Count)]
            : bestPoint;
        return true;
    }

    private int RefreshAlivePlayers()
    {
        alivePlayerPositions.Clear();

        foreach (PlayerStateManager player in FindObjectsByType<PlayerStateManager>(FindObjectsSortMode.None))
        {
            if (player != null && player.currentState.Value != PlayerState.Muerto)
                alivePlayerPositions.Add(player.transform.position);
        }

        return alivePlayerPositions.Count;
    }

    private int CurrentCap()
    {
        return WaveCalculator.MaxAlive(activeProfile.maxAliveBase, activeProfile.maxAlivePerPlayer, RefreshAlivePlayers());
    }

    private float MinHorizontalDistanceToPlayers(Vector3 point)
    {
        float min = float.PositiveInfinity;

        for (int i = 0; i < alivePlayerPositions.Count; i++)
        {
            float dx = alivePlayerPositions[i].x - point.x;
            float dz = alivePlayerPositions[i].z - point.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance < min) min = distance;
        }

        return min;
    }

    private static void Shuffle(List<GameObject> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ---------- Validación ----------
    private bool ValidateSetup()
    {
        bool ok = true;

        ok &= ValidatePrefab(bomberPrefab, "bomberPrefab", true);
        ok &= ValidatePrefab(shooterPrefab, "shooterPrefab", true);
        ok &= ValidatePrefab(healerPrefab, "healerPrefab", false);

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[WaveManager] Faltan los puntos de aparición (spawnPoints).");
            ok = false;
        }

        foreach (DifficultyLevel level in System.Enum.GetValues(typeof(DifficultyLevel)))
        {
            if (GetProfile(level) == null)
            {
                Debug.LogError("[WaveManager] Falta el perfil de dificultad " + level + " en difficultyProfiles.");
                ok = false;
            }
        }

        return ok;
    }

    private static bool ValidatePrefab(GameObject prefab, string label, bool required)
    {
        if (prefab == null)
        {
            if (required) Debug.LogError("[WaveManager] Falta asignar " + label + ".");
            return !required;
        }

        if (prefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogError("[WaveManager] " + label + " no tiene NetworkObject.");
            return false;
        }

        if (prefab.GetComponent<WaveEnemy>() == null)
        {
            Debug.LogError("[WaveManager] " + label + " no tiene el componente WaveEnemy.");
            return false;
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;

        Gizmos.color = Color.cyan;
        foreach (Transform spawnPoint in spawnPoints)
        {
            if (spawnPoint != null)
                Gizmos.DrawWireSphere(spawnPoint.position, 0.6f);
        }
    }
}