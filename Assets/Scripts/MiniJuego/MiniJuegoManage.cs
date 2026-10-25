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

    [Header("Spawns y Posiciones")]
    [Tooltip("Punto de inicio en la arena de juego.")]
    [SerializeField] private Transform playerSpawnPoint;
    [Tooltip("Punto de espera en la sala de espera / lobby.")]
    [SerializeField] private Transform lobbySpawnPoint;

    [Header("Configuración del Jugador")]
    [SerializeField] private GameObject playerPrefab;

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
    [Tooltip("Prefab del fuego/explosión para el centro.")]
    [SerializeField] private GameObject explosionVfx;
    [Tooltip("Prefab del fuego para los brazos de la cruz.")]
    [SerializeField] private GameObject fireVfx;

    [Header("Regla Anti-Camper")]
    [SerializeField] private float stationaryThreshold = 3f;

    [Header("HUD In-Game y Resultados")]
    [SerializeField] private GameObject inGameHUD;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private PlayerLivesUI playerLivesUI;
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

        // Vista de inicio: Sala de espera activa
        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(true);
        if (mainCamera != null) mainCamera.gameObject.SetActive(false);

        if (startMatchButton != null)
            startMatchButton.onClick.AddListener(OnStartMatchClicked);

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);

        RecopilarCasillas();
    }

    private void Start()
    {
        currentSpawnInterval = initialSpawnInterval;
        // Posicionar al personaje en el punto de espera de la sala
        StartCoroutine(SetupPlayerInLobbyRoutine());
    }

    private void RecopilarCasillas()
    {
        casillasDisponibles.Clear();
        if (sueloParent != null)
        {
            foreach (Transform hijo in sueloParent)
            {
                casillasDisponibles.Add(hijo);
            }
        }

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

    // ---------- 1. Spawn y colocación en Sala de Espera ----------
    private IEnumerator SetupPlayerInLobbyRoutine()
    {
        yield return null; // Espera un frame para que la red y componentes terminen de inicializar

        Vector3 targetPos = lobbySpawnPoint != null ? lobbySpawnPoint.position : Vector3.up;
        Quaternion targetRot = lobbySpawnPoint != null ? lobbySpawnPoint.rotation : Quaternion.identity;

        localPlayer = FindFirstObjectByType<PlayerStateManager>();

        if (localPlayer == null && playerPrefab != null)
        {
            GameObject playerObj = Instantiate(playerPrefab, targetPos, targetRot);
            NetworkObject netObj = playerObj.GetComponent<NetworkObject>();

            if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                netObj.SpawnWithOwnership(NetworkManager.Singleton.LocalClientId);
            }
            localPlayer = playerObj.GetComponent<PlayerStateManager>();
        }
        else if (localPlayer != null)
        {
            PlayerSpawnHandler handler = localPlayer.GetComponent<PlayerSpawnHandler>();
            if (handler != null && handler.IsSpawned)
            {
                handler.TeleportToSpawnRpc(targetPos, targetRot);
            }
            else
            {
                CharacterController cc = localPlayer.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                localPlayer.transform.SetPositionAndRotation(targetPos, targetRot);
                if (cc != null) cc.enabled = true;
            }
        }

        // Aplicar animación de lobby del punto asignado
        if (localPlayer != null && lobbySpawnPoint != null)
        {
            LobbySpawnPoint lsp = lobbySpawnPoint.GetComponent<LobbySpawnPoint>();
            PlayerCharacterVisual visual = localPlayer.GetComponent<PlayerCharacterVisual>();
            if (lsp != null && visual != null)
            {
                visual.SetLobbyAnimation(lsp.LobbyAnimation);
            }
        }
    }

    // ---------- 2. Inicio de Partida ----------
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
        if (inGameHUD != null)
        {
            inGameHUD.SetActive(true);
        }

        Vector3 spawnPos = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.up;
        Quaternion spawnRot = playerSpawnPoint != null ? playerSpawnPoint.rotation : Quaternion.identity;

        if (localPlayer == null)
            localPlayer = FindFirstObjectByType<PlayerStateManager>();

        if (localPlayer != null)
        {
            // Restaurar animación habitual de juego
            PlayerCharacterVisual visual = localPlayer.GetComponent<PlayerCharacterVisual>();
            if (visual != null)
            {
                visual.ResetLobbyAnimation();
            }

            // Mover al spawn del juego
            PlayerSpawnHandler handler = localPlayer.GetComponent<PlayerSpawnHandler>();
            if (handler != null && handler.IsSpawned)
            {
                handler.TeleportToSpawnRpc(spawnPos, spawnRot);
            }
            else
            {
                CharacterController ccMove = localPlayer.GetComponent<CharacterController>();
                if (ccMove != null) ccMove.enabled = false;
                localPlayer.transform.SetPositionAndRotation(spawnPos, spawnRot);
                if (ccMove != null) ccMove.enabled = true;
            }

            // Desactivar colocación de bombas manual en este modo
            PlayerBombController bombController = localPlayer.GetComponent<PlayerBombController>();
            if (bombController != null)
            {
                bombController.enabled = false;
            }

            // Actualizar corazones
            if (playerLivesUI != null)
            {
                playerLivesUI.UpdateHearts(localPlayer.currentLives.Value);
            }
            else
            {
                PlayerLivesUI fallbackUI = FindFirstObjectByType<PlayerLivesUI>(FindObjectsInactive.Include);
                if (fallbackUI != null) fallbackUI.UpdateHearts(localPlayer.currentLives.Value);
            }

            CinemachineCamera vcam = FindFirstObjectByType<CinemachineCamera>();
            if (vcam != null)
            {
                vcam.Target.TrackingTarget = localPlayer.transform;
                vcam.Follow = localPlayer.transform;
            }

            lastPlayerPosition = localPlayer.transform.position;
            localPlayer.currentState.OnValueChanged += OnPlayerStateChanged;

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

        StartCoroutine(DropAndExplodeRoutine(bombNetObj, bombObj.transform, targetGridCenter));
    }

    private IEnumerator DropAndExplodeRoutine(NetworkObject bombNetObj, Transform bombTransform, Vector3 groundTarget)
    {
        while (bombTransform != null && Vector3.Distance(bombTransform.position, groundTarget) > 0.05f)
        {
            bombTransform.position = Vector3.MoveTowards(bombTransform.position, groundTarget, fallSpeed * Time.deltaTime);
            yield return null;
        }

        if (bombTransform != null)
        {
            bombTransform.position = groundTarget;
        }

        yield return new WaitForSeconds(bombTimer);

        if (bombNetObj == null || !bombNetObj.IsSpawned) yield break;

        Vector3 explosionCenter = bombTransform.position;
        ExecuteExplosion(explosionCenter);

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

        foreach (Vector3 dir in directions)
        {
            for (int i = 1; i <= explosionRange; i++)
            {
                Vector3 target = center + (dir * gridSize * i);

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

                if (blocked) break;
                cellsToHit.Add(target);
            }
        }

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

        if (localPlayer != null && localPlayer.currentState.Value == PlayerState.Vivo)
        {
            Vector2 playerPos2D = new Vector2(localPlayer.transform.position.x, localPlayer.transform.position.z);

            foreach (Vector3 cell in cellsToHit)
            {
                Vector2 cellPos2D = new Vector2(cell.x, cell.z);
                if (Vector2.Distance(playerPos2D, cellPos2D) <= (gridSize * 0.65f))
                {
                    ApplyMinijuegoDamage(localPlayer);
                    break;
                }
            }
        }
    }

    private void ApplyMinijuegoDamage(PlayerStateManager player)
    {
        if (player == null) return;

        if (player.currentState.Value == PlayerState.Muerto || player.currentState.Value == PlayerState.Aturdido)
            return;

        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null && stats.IsShielded)
        {
            stats.ConsumeShield();
            return;
        }

        player.currentLives.Value--;

        if (playerLivesUI != null)
        {
            playerLivesUI.UpdateHearts(player.currentLives.Value);
        }
        else
        {
            PlayerLivesUI fallbackUI = FindFirstObjectByType<PlayerLivesUI>();
            if (fallbackUI != null) fallbackUI.UpdateHearts(player.currentLives.Value);
        }

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

    private Vector3 GetRandomCellCenter()
    {
        if (casillasDisponibles.Count > 0)
        {
            Transform t = casillasDisponibles[Random.Range(0, casillasDisponibles.Count)];
            return GetCenterFromTransform(t);
        }
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
}