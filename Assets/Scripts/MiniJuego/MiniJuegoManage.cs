using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MiniJuegoManager : MonoBehaviour
{
    public static MiniJuegoManager Instance { get; private set; }

    [Header("Cámaras")]
    [Tooltip("Cámara cenital que enfoca la arena de juego.")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("Cámara que enfoca el lobby o sala de espera.")]
    [SerializeField] private Camera lobbyCamera;

    [Header("Sala de Espera (Pre-Juego)")]
    [Tooltip("Objeto raíz o Canvas de la sala de espera a ocultar al empezar.")]
    [SerializeField] private GameObject lobbyUI;
    [Tooltip("Botón para iniciar la partida (BTNempezar).")]
    [SerializeField] private Button startMatchButton;

    [Header("Configuración del Jugador")]
    [Tooltip("Prefab del jugador con PlayerStateManager y NetworkObject.")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerSpawnPoint;

    [Header("Configuración de Cuadrícula (Arena)")]
    [SerializeField] private float gridSize = 2.5f;
    [SerializeField] private int gridDimension = 4;
    [SerializeField] private LayerMask groundLayerMask = ~0;

    [Header("Mecánica de Bombas")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private float spawnHeightY = 10f;
    [SerializeField] private float fallSpeed = 12f;
    [SerializeField] private float initialSpawnInterval = 3f;
    [SerializeField] private float minSpawnInterval = 0.8f;
    [SerializeField] private float difficultyRate = 0.05f;

    [Header("Regla Anti-Camper")]
    [SerializeField] private float stationaryThreshold = 3f;

    [Header("HUD In-Game y Resultados")]
    [SerializeField] private GameObject inGameHUD;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverText;
    [SerializeField] private Button restartButton;

    // Estado interno
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

        // Inicialización de UI y Cámaras
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (inGameHUD != null) inGameHUD.SetActive(false);

        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(true);
        if (mainCamera != null) mainCamera.gameObject.SetActive(false);

        if (startMatchButton != null)
            startMatchButton.onClick.AddListener(OnStartMatchClicked);

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
    }

    private void Start()
    {
        currentSpawnInterval = initialSpawnInterval;
    }

    private void OnStartMatchClicked()
    {
        if (isGameActive) return;

        // 1. Ocultar sala de espera y cambiar a cámara principal
        if (lobbyUI != null) lobbyUI.SetActive(false);
        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(false);
        if (mainCamera != null) mainCamera.gameObject.SetActive(true);

        if (inGameHUD != null) inGameHUD.SetActive(true);

        // 2. Spawnear o reposicionar al jugador
        StartCoroutine(SpawnPlayerAndStartGame());
    }

    private IEnumerator SpawnPlayerAndStartGame()
    {
        Vector3 spawnPos = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.up;
        Quaternion spawnRot = playerSpawnPoint != null ? playerSpawnPoint.rotation : Quaternion.identity;

        // Verificar si el jugador ya fue instanciado por Netcode o si lo creamos
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
            // Reposicionar el jugador existente en la arena
            CharacterController cc = localPlayer.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            localPlayer.transform.SetPositionAndRotation(spawnPos, spawnRot);

            if (cc != null) cc.enabled = true;
        }

        if (localPlayer != null)
        {
            lastPlayerPosition = localPlayer.transform.position;
            localPlayer.currentState.OnValueChanged += OnPlayerStateChanged;
            localPlayer.ActualizarColisionesPartida();
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
        currentPos.y = 0f;
        Vector3 previousPos = lastPlayerPosition;
        previousPos.y = 0f;

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
            targetGridCenter = GetCellCenterFromPosition(localPlayer.transform.position);
            stationaryTimer = 0f;
        }
        else
        {
            targetGridCenter = GetRandomGridCenter();
        }

        Vector3 startSkyPos = new Vector3(targetGridCenter.x, targetGridCenter.y + spawnHeightY, targetGridCenter.z);

        GameObject bombObj = Instantiate(bombPrefab, startSkyPos, Quaternion.identity);
        NetworkObject bombNetObj = bombObj.GetComponent<NetworkObject>();

        if (bombNetObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            bombNetObj.Spawn();
        }

        StartCoroutine(DropBombRoutine(bombObj.transform, targetGridCenter));
    }

    private IEnumerator DropBombRoutine(Transform bombTransform, Vector3 groundTarget)
    {
        while (bombTransform != null && Vector3.Distance(bombTransform.position, groundTarget) > 0.05f)
        {
            bombTransform.position = Vector3.MoveTowards(
                bombTransform.position,
                groundTarget,
                fallSpeed * Time.deltaTime
            );
            yield return null;
        }

        if (bombTransform != null)
        {
            bombTransform.position = groundTarget;
        }
    }

    private Vector3 GetCellCenterFromPosition(Vector3 worldPos)
    {
        Vector3 rayOrigin = new Vector3(worldPos.x, worldPos.y + 2f, worldPos.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 6f, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 center = hit.collider.bounds.center;
            return new Vector3(center.x, hit.point.y + 0.5f, center.z);
        }

        float x = Mathf.Round(worldPos.x / gridSize) * gridSize;
        float z = Mathf.Round(worldPos.z / gridSize) * gridSize;
        return new Vector3(x, 0.5f, z);
    }

    private Vector3 GetRandomGridCenter()
    {
        int halfDim = gridDimension / 2;
        int randXIndex = Random.Range(-halfDim, halfDim);
        int randZIndex = Random.Range(-halfDim, halfDim);

        float worldX = (randXIndex * gridSize) + (gridSize * 0.5f);
        float worldZ = (randZIndex * gridSize) + (gridSize * 0.5f);

        return GetCellCenterFromPosition(new Vector3(worldX, 0f, worldZ));
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

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

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