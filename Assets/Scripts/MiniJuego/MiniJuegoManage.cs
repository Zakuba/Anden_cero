using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MiniJuegoManager : MonoBehaviour
{
    public static MiniJuegoManager Instance { get; private set; }

    [Header("Cámaras")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera lobbyCamera;

    [Header("Sala de Espera (Pre-Juego)")]
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private Button startMatchButton;

    [Header("Configuración del Jugador")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerSpawnPoint;

    [Header("Configuración del Suelo / Casillas")]
    [Tooltip("Arrastra aquí el objeto 'Suelo' que contiene todas las casillas hijas.")]
    [SerializeField] private Transform sueloParent;
    [Tooltip("Tamaño de casilla para la expansión en cruz.")]
    [SerializeField] private float gridSize = 2.5f;

    [Header("Mecánica de Bombas")]
    [SerializeField] private GameObject bombPrefab;
    [Tooltip("Tiempo en segundos que espera la bomba en el piso antes de detonar.")]
    [SerializeField] private float bombTimer = 2.5f;
    [Tooltip("Rango de casillas en cruz que abarca la explosión.")]
    [SerializeField] private int explosionRange = 1;
    [SerializeField] private float spawnHeightY = 10f;
    [SerializeField] private float fallSpeed = 14f;
    [SerializeField] private float initialSpawnInterval = 3f;
    [SerializeField] private float minSpawnInterval = 0.9f;
    [SerializeField] private float difficultyRate = 0.05f;

    [Header("Efectos Visuales de Explosión (VFX)")]
    [Tooltip("Prefab del fuego/explosión para el centro (el mismo que usas en PlayerBombController).")]
    [SerializeField] private GameObject explosionVfx;
    [Tooltip("Prefab del fuego para los brazos de la cruz.")]
    [SerializeField] private GameObject fireVfx;

    [Header("Regla Anti-Camper")]
    [SerializeField] private float stationaryThreshold = 3f;

    [Header("HUD In-Game y Resultados")]
    [SerializeField] private GameObject inGameHUD;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private PlayerLivesUI playerLivesUI; // <--- AGREGAR ESTA LÍNEA
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverText;
    [SerializeField] private Button restartButton;

    // Control interno
    private List<Transform> casillasDisponibles = new List<Transform>();
    private PlayerStateManager localPlayer;
    private float survivalTimer = 0f;
    private bool isGameActive = false;
    private float currentSpawnInterval;
    private Vector3 lastPlayerPosition;
    private float stationaryTimer = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (inGameHUD != null) inGameHUD.SetActive(false);

        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(true);
        if (mainCamera != null) mainCamera.gameObject.SetActive(false);

        if (startMatchButton != null)
            startMatchButton.onClick.AddListener(OnStartMatchClicked);

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);

        // Recopilar todas las casillas hijas bajo 'Suelo'
        RecopilarCasillas();
    }

    private void Start()
    {
        currentSpawnInterval = initialSpawnInterval;
    }

    private void RecopilarCasillas()
    {
        casillasDisponibles.Clear();
        if (sueloParent != null)
        {
            foreach (Transform hijo in sueloParent)
            {
                // Agrega todas las casillas
                casillasDisponibles.Add(hijo);
            }
        }

        // Si no se asignó 'Suelo', busca casillas por nombre en la escena
        if (casillasDisponibles.Count == 0)
        {
            foreach (GameObject obj in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (obj.name.ToLower().Contains("casil") && obj.transform.parent != null && obj.transform.parent.name.ToLower().Contains("suelo"))
                {
                    casillasDisponibles.Add(obj.transform);
                }
            }
        }
    }

    public void OnStartMatchClicked()
    {
        if (isGameActive) return;

        if (lobbyUI != null) lobbyUI.SetActive(false);
        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(false);
        if (mainCamera != null) mainCamera.gameObject.SetActive(true);
        if (inGameHUD != null) inGameHUD.SetActive(true);

        StartCoroutine(SpawnPlayerAndStartGame());
    }

private IEnumerator SpawnPlayerAndStartGame()
{
    // 1. Asegurar que el HUD del cronómetro esté encendido en pantalla
    if (inGameHUD != null)
    {
        inGameHUD.SetActive(true);
    }

    Vector3 spawnPos = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.up;
    Quaternion spawnRot = playerSpawnPoint != null ? playerSpawnPoint.rotation : Quaternion.identity;

    // Buscar si ya existe el jugador o instanciarlo
    localPlayer = FindFirstObjectByType<PlayerStateManager>();

    if (localPlayer == null && playerPrefab != null)
    {
        GameObject playerObj = Instantiate(playerPrefab, spawnPos, spawnRot);
        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();

        if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            netObj.SpawnWithOwnership(NetworkManager.Singleton.LocalClientId);
        }
        localPlayer = playerObj.GetComponent<PlayerStateManager>();
    }
    else if (localPlayer != null)
    {
        CharacterController cc = localPlayer.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        localPlayer.transform.SetPositionAndRotation(spawnPos, spawnRot);
        if (cc != null) cc.enabled = true;
    }

    // Configurar al jugador una vez que YA EXISTE
    if (localPlayer != null)
    {
        // Desactiva el script de poner bombas para este modo de juego
        PlayerBombController bombController = localPlayer.GetComponent<PlayerBombController>();
        if (bombController != null)
        {
            bombController.enabled = false;
        }

        // 2. FORZAR ACTUALIZACIÓN DE CORAZONES (Ahora sí es seguro porque localPlayer existe)
        if (playerLivesUI != null)
        {
            playerLivesUI.UpdateHearts(localPlayer.currentLives.Value);
        }
        else
        {
            PlayerLivesUI fallbackUI = FindFirstObjectByType<PlayerLivesUI>(FindObjectsInactive.Include);
            if (fallbackUI != null)
            {
                fallbackUI.UpdateHearts(localPlayer.currentLives.Value);
            }
        }

        CinemachineCamera vcam = FindFirstObjectByType<CinemachineCamera>();
        if (vcam != null)
        {
            vcam.Target.TrackingTarget = localPlayer.transform;
            vcam.Follow = localPlayer.transform;
        }

        lastPlayerPosition = localPlayer.transform.position;
        localPlayer.currentState.OnValueChanged += OnPlayerStateChanged;

        // Encender explícitamente el CharacterController para asegurar movimiento
        CharacterController cc = localPlayer.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = true;
        }
    }

    yield return new WaitForSeconds(0.5f);

    isGameActive = true;
    StartCoroutine(BombSpawnLoop());
}

    private void Update()
    {
        if (!isGameActive || localPlayer == null) return;

        survivalTimer += Time.deltaTime;
        if (timerText != null)
        {
            timerText.text = FormatTime(survivalTimer);
        }

        CheckPlayerMovement();
    }

    private void CheckPlayerMovement()
    {
        Vector3 currentPos = localPlayer.transform.position;
        currentPos.y = 0;
        Vector3 previousPos = lastPlayerPosition;
        previousPos.y = 0;

        if (Vector3.Distance(currentPos, previousPos) < 0.1f)
        {
            stationaryTimer += Time.deltaTime;
        }
        else
        {
            stationaryTimer = 0f;
            lastPlayerPosition = localPlayer.transform.position;
        }
    }

    private IEnumerator BombSpawnLoop()
    {
        yield return new WaitForSeconds(1.5f);

        while (isGameActive)
        {
            SpawnBomb();
            currentSpawnInterval = Mathf.Max(minSpawnInterval, currentSpawnInterval - difficultyRate);
            yield return new WaitForSeconds(currentSpawnInterval);
        }
    }

    private void SpawnBomb()
    {
        if (bombPrefab == null || localPlayer == null) return;

        Vector3 targetGridCenter;

        // Si estuvo quieto >= 3s, apuntar a su casilla
        if (stationaryTimer >= stationaryThreshold)
        {
            targetGridCenter = GetClosestCellCenter(localPlayer.transform.position);
            stationaryTimer = 0f;
        }
        else
        {
            targetGridCenter = GetRandomCellCenter();
        }

        Vector3 startSkyPos = new Vector3(targetGridCenter.x, targetGridCenter.y + spawnHeightY, targetGridCenter.z);

        GameObject bombObj = Instantiate(bombPrefab, startSkyPos, Quaternion.identity);
        NetworkObject bombNetObj = bombObj.GetComponent<NetworkObject>();

        if (bombNetObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            bombNetObj.Spawn();
        }

        // Iniciar caída y luego cuenta regresiva de explosión
        StartCoroutine(DropAndExplodeRoutine(bombNetObj, bombObj.transform, targetGridCenter));
    }

    private IEnumerator DropAndExplodeRoutine(NetworkObject bombNetObj, Transform bombTransform, Vector3 groundTarget)
    {
        // 1. Caída suave hacia el centro de la casilla
        while (bombTransform != null && Vector3.Distance(bombTransform.position, groundTarget) > 0.05f)
        {
            bombTransform.position = Vector3.MoveTowards(bombTransform.position, groundTarget, fallSpeed * Time.deltaTime);
            yield return null;
        }

        if (bombTransform != null)
        {
            bombTransform.position = groundTarget;
        }

        // 2. Temporizador en el piso antes de detonar
        yield return new WaitForSeconds(bombTimer);

        if (bombNetObj == null || !bombNetObj.IsSpawned) yield break;

        Vector3 explosionCenter = bombTransform.position;

        // 3. Ejecutar explosión: daño y visuales
        ExecuteExplosion(explosionCenter);

        // 4. Despawnear bomba
        yield return new WaitForSeconds(0.05f);
        if (bombNetObj != null && bombNetObj.IsSpawned)
        {
            bombNetObj.Despawn();
        }
    }

private void ExecuteExplosion(Vector3 center)
{
    List<Vector3> cellsToHit = new List<Vector3> { center };
    Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

    // 1. Escaneo físico en cruz para frenar el fuego en muros
    foreach (Vector3 dir in directions)
    {
        for (int i = 1; i <= explosionRange; i++)
        {
            Vector3 target = center + (dir * gridSize * i);

            // Escaneamos un volumen generoso en altura para interceptar el Mesh Collider del muro
            Vector3 halfExtents = new Vector3(gridSize * 0.45f, 1.5f, gridSize * 0.45f);
            Vector3 boxCenter = new Vector3(target.x, center.y + 0.5f, target.z);

            Collider[] hits = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

            bool blocked = false;
            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Indestructible") || hit.gameObject.name.ToLower().Contains("wall") || hit.gameObject.name.ToLower().Contains("pared"))
                {
                    blocked = true;
                    break;
                }
            }

            // Si hay un muro en esa casilla o en el camino, frenamos la expansión en esta dirección
            if (blocked) break;

            cellsToHit.Add(target);
        }
    }

    // 2. Instanciación visual de los efectos de fuego
    float floorY = center.y - 0.45f; 

    if (explosionVfx != null)
    {
        Vector3 vfxCenter = new Vector3(center.x, floorY, center.z);
        Instantiate(explosionVfx, vfxCenter, explosionVfx.transform.rotation);
    }

    for (int i = 1; i < cellsToHit.Count; i++)
    {
        GameObject prefabToSpawn = fireVfx != null ? fireVfx : explosionVfx;
        if (prefabToSpawn != null)
        {
            Vector3 vfxPos = new Vector3(cellsToHit[i].x, floorY, cellsToHit[i].z);
            Instantiate(prefabToSpawn, vfxPos, prefabToSpawn.transform.rotation);
        }
    }

    // 3. Daño al jugador local
        if (localPlayer != null && localPlayer.currentState.Value == PlayerState.Vivo)
        {
            Vector2 playerPos2D = new Vector2(localPlayer.transform.position.x, localPlayer.transform.position.z);

            foreach (Vector3 cell in cellsToHit)
            {
                Vector2 cellPos2D = new Vector2(cell.x, cell.z);

                // Radio de cobertura generoso en la casilla
                if (Vector2.Distance(playerPos2D, cellPos2D) <= (gridSize * 0.65f))
                {
                    ApplyMinijuegoDamage(localPlayer);
                    break;
                }
            }
        }
}

    private Vector3 GetRandomCellCenter()
    {
        if (casillasDisponibles.Count > 0)
        {
            Transform t = casillasDisponibles[Random.Range(0, casillasDisponibles.Count)];
            return GetCenterFromTransform(t);
        }

        // Fallback por si la lista estuviera vacía
        return new Vector3(0f, 0.5f, 0f);
    }

    private Vector3 GetClosestCellCenter(Vector3 pos)
    {
        if (casillasDisponibles.Count == 0) return pos;

        Transform closest = casillasDisponibles[0];
        float minDst = float.MaxValue;

        foreach (Transform c in casillasDisponibles)
        {
            float dst = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(c.position.x, c.position.z));
            if (dst < minDst)
            {
                minDst = dst;
                closest = c;
            }
        }

        return GetCenterFromTransform(closest);
    }

    private Vector3 GetCenterFromTransform(Transform cellTransform)
    {
        Collider col = cellTransform.GetComponent<Collider>();
        if (col != null)
        {
            // Apoyar la bomba sobre la cara superior de la baldosa (+0.5f de radio de la bomba)
            return new Vector3(col.bounds.center.x, col.bounds.max.y + 0.5f, col.bounds.center.z);
        }

        return new Vector3(cellTransform.position.x, cellTransform.position.y + 0.5f, cellTransform.position.z);
    }

    private void OnPlayerStateChanged(PlayerState previous, PlayerState current)
    {
        if (current == PlayerState.Muerto && isGameActive)
        {
            EndGame();
        }
    }

    private void EndGame()
    {
        isGameActive = false;
        StopAllCoroutines();

        PlayerData data = PersistenceManager.LoadData();
        bool newRecord = survivalTimer > data.recordMinijuegoBombas;

        if (newRecord)
        {
            data.recordMinijuegoBombas = survivalTimer;
            PersistenceManager.SaveData(data);
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        if (gameOverText != null)
        {
            string recordMsg = newRecord
                ? "¡NUEVO RÉCORD PERSONAL!"
                : $"Mejor Récord: {FormatTime(data.recordMinijuegoBombas)}";

            gameOverText.text = $"FIN DEL JUEGO\n\nTiempo: {FormatTime(survivalTimer)}\n{recordMsg}";
        }
    }

    private void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }

    private void OnDestroy()
    {
        if (localPlayer != null)
        {
            localPlayer.currentState.OnValueChanged -= OnPlayerStateChanged;
        }
    }

/// <summary>
/// Aplica el daño directamente al jugador en el minijuego evitando los bloqueos de MatchManager
/// </summary>
private void ApplyMinijuegoDamage(PlayerStateManager player)
{
    if (player == null) return;

    if (player.currentState.Value == PlayerState.Muerto || 
        player.currentState.Value == PlayerState.Aturdido)
        return;

    PlayerStats stats = player.GetComponent<PlayerStats>();
    if (stats != null && stats.IsShielded)
    {
        stats.ConsumeShield();
        return;
    }

    // 1. Descontar vida
    player.currentLives.Value--;

    // 2. Actualizar corazones en pantalla directamente
    if (playerLivesUI != null)
    {
        playerLivesUI.UpdateHearts(player.currentLives.Value);
    }
    else
    {
        PlayerLivesUI fallbackUI = FindFirstObjectByType<PlayerLivesUI>();
        if (fallbackUI != null) fallbackUI.UpdateHearts(player.currentLives.Value);
    }

    // 3. Evaluar estado
    if (player.currentLives.Value <= 0)
    {
        player.currentState.Value = PlayerState.Muerto;
    }
    else
    {
        StartCoroutine(MinijuegoStunRoutine(player));
    }
}

private IEnumerator MinijuegoStunRoutine(PlayerStateManager player)
{
    player.currentState.Value = PlayerState.Aturdido;

    yield return new WaitForSeconds(2f);

    if (player != null && player.currentState.Value != PlayerState.Muerto)
    {
        player.currentState.Value = PlayerState.Vivo;
    }
}
}