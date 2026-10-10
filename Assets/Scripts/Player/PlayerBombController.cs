using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class PlayerBombController : NetworkBehaviour
{
    /*
    [Header("Configuración de Bomba")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float bombTimer = 3f;
    [SerializeField] private int explosionRange = 2;
    [SerializeField] private LayerMask explosionLayerMask;
    [SerializeField] private KeyCode plantKey = KeyCode.Space;
    [SerializeField] private float alturaspawnbomba = -1f;
    */

    [Header("Configuración de Bomba")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float bombTimer = 3f;
    [SerializeField] private int explosionRange = 2;
    [SerializeField] private LayerMask explosionLayerMask;
    [SerializeField] private KeyCode plantKey = KeyCode.Space;
    [Tooltip("Radio o elevación para que la bomba descanse sobre el piso sin enterrarse.")]
    [SerializeField] private float bombRadiusOffset = 0.5f;

    [Header("Power-ups (Drops)")]
    [SerializeField] private GameObject[] powerUpPrefabs; // orden: Expansor, Bomba Extra, Botas, Guante, Escudo
    [SerializeField] private float powerUpDropChance = 0.5f;
    [SerializeField] private float powerUpSpawnHeight = -1f;

    [Header("Efectos Visuales (VFX)")]
    [SerializeField] private GameObject explosionVfx;
    [SerializeField] private GameObject fireVfx;

       
    private int activeBombs = 0;
    private int maxBombs = 1;
    private PlayerStats playerStats;
    private PlayerStateManager stateManager;
    private BombCooldownUI localBombUI;

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        stateManager = GetComponent<PlayerStateManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Busca la UI en la escena localmente para este jugador
            localBombUI = FindObjectOfType<BombCooldownUI>(true);
        }
    }

private int EffectiveMaxBombs => maxBombs + (playerStats != null ? playerStats.MaxBombsBonus : 0);
private int EffectiveExplosionRange => explosionRange + (playerStats != null ? playerStats.ExplosionRangeBonus : 0);

    private void Update()
    {
        if (!IsOwner) return;

        // Bloqueo si el jugador está aturdido o muerto
        if (stateManager != null && stateManager.currentState.Value != PlayerState.Vivo) return;
        if (MatchManager.Instance != null && !MatchManager.Instance.IsMatchRunning) return;

        if (localBombUI == null)
        {
            localBombUI = FindObjectOfType<BombCooldownUI>(true);
            
            // Si sigue sin existir, cancelamos el Update para no tirar errores, 
            // pero el jugador podrá moverse igual.
            if (localBombUI == null) return; 
        }

        if (localBombUI == null)
        {
            Debug.Log("No se encontro el objeto");
        }
        else
        {
            // Si gastamos todas las bombas y la UI no está en cooldown, lo activamos
            if (activeBombs >= EffectiveMaxBombs && !localBombUI.IsCooldownActive)
            {
                localBombUI.StartCooldown(bombTimer);
            }
            // Si recuperamos bombas (explotó una o agarramos power-up) y la UI sigue bloqueada, la liberamos
            else if (activeBombs < EffectiveMaxBombs && localBombUI.IsCooldownActive)
            {
                localBombUI.ResetUI();
            }
        }

        if (Input.GetKeyDown(plantKey) && activeBombs < EffectiveMaxBombs)
        {   
            Vector3 spawnPosition = GetGridCenter(transform.position);
            RequestPlantBombServerRpc(spawnPosition);
        }
    }

/*
private Vector3 GetGridCenter(Vector3 playerPos)
{
    float x = Mathf.Round(playerPos.x / gridSize) * gridSize;
    float z = Mathf.Round(playerPos.z / gridSize) * gridSize;
    
    // (playerPos.y - 1.0f) es el nivel del piso donde apoya el jugador.
    // Sumamos bombRadiusOffset (0.5f) para que la base de la esfera toque el suelo.
    float bombCenterY = (playerPos.y - 1.0f) + bombRadiusOffset;

    return new Vector3(x, bombCenterY, z);
}
*/
//Prueba
private Vector3 GetGridCenter(Vector3 playerPos)
{
    // Lanza un rayo vertical desde el torso del jugador hacia el piso
    Vector3 rayOrigin = new Vector3(playerPos.x, playerPos.y + 0.5f, playerPos.z);
    
    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
    {
        string objName = hit.collider.gameObject.name.ToLower();
        string rootName = hit.collider.transform.root.gameObject.name.ToLower();

        // Contempla "casila" (con una sola L), "casilla", "piso", "anden" y "tile"
        if (objName.Contains("casil") || objName.Contains("piso") || objName.Contains("anden") || objName.Contains("floor")
            || rootName.Contains("casil") || rootName.Contains("piso") || rootName.Contains("anden"))
        {
            // hit.collider.bounds.center devuelve el centro geométrico exacto en el mundo del collider
            Vector3 tileCenter = hit.collider.bounds.center;
            
            // Apoya la bomba con su radio (+0.5f) sobre la cara superior detectada
            float bombY = hit.point.y + bombRadiusOffset;

            return new Vector3(tileCenter.x, bombY, tileCenter.z);
        }
    }

    // Fallback si por alguna razón no detecta una baldosa específica
    float x = Mathf.Round(playerPos.x / gridSize) * gridSize;
    float z = Mathf.Round(playerPos.z / gridSize) * gridSize;
    float floorY = (playerPos.y - 1.0f) + bombRadiusOffset;

    return new Vector3(x, floorY, z);
}

    [ServerRpc]
    private void RequestPlantBombServerRpc(Vector3 spawnPosition)
    {
        if (MatchManager.Instance != null && !MatchManager.Instance.IsMatchRunning) return;
        if (activeBombs >= EffectiveMaxBombs) return;

        activeBombs++;
        UpdateBombCountClientRpc(activeBombs);

        GameObject bombInstance = Instantiate(bombPrefab, spawnPosition, Quaternion.identity);
        NetworkObject bombNetObj = bombInstance.GetComponent<NetworkObject>();
        bombNetObj.Spawn();

        // Llamada con un solo parámetro
        StartCoroutine(BombExplosionRoutine(bombNetObj));
    }

private IEnumerator BombExplosionRoutine(NetworkObject bombNetObj)
{
        BombInteractable bombLogic = bombNetObj.GetComponent<BombInteractable>();
        float currentTimer = 0f;

        try
        {
            while (currentTimer < bombTimer)
            {
                // Si la bomba se destruyó externamente, salimos del bucle para no causar errores de referencia nula
                if (bombNetObj == null || !bombNetObj.IsSpawned)
                {
                    Debug.LogWarning("[Bomba] La bomba fue destruida o despawneada antes de completar el temporizador.");
                    yield break;
                }

                if (bombLogic == null || !bombLogic.isMoving.Value)
                {
                    currentTimer += Time.deltaTime;
                }

                yield return null;
            }

            // Si completó el tiempo con la bomba viva, ejecuta la explosión normalmente
            if (bombNetObj != null && bombNetObj.IsSpawned)
            {
                Vector3 finalPos = bombNetObj.transform.position;
                List<Vector3> affectedCells = CalculateExplosionCells(finalPos);
                SpawnVfxClientRpc(affectedCells.ToArray());

                yield return new WaitForSeconds(0.05f);

                if (bombNetObj != null && bombNetObj.IsSpawned)
                {
                    bombNetObj.Despawn();
                }
            }
        }
        finally
        {
            // Esto se ejecuta SIEMPRE, incluso si la corrutina hace 'yield break' o si se interrumpe
            activeBombs--;
            if (activeBombs < 0) activeBombs = 0;
            UpdateBombCountClientRpc(activeBombs);
    }
}

/// Cálculo autoritativo en el Servidor: determina daños y obstáculos proyectando a cada desnivel
    private List<Vector3> CalculateExplosionCells(Vector3 center)
    {
        List<Vector3> cells = new List<Vector3>();
        HashSet<ulong> playersHitThisExplosion = new HashSet<ulong>();

        // Casilla central proyectada al suelo
        Vector3 groundCenter = GetGroundPosition(center);
        cells.Add(groundCenter);
        CheckAndDestroyCell(groundCenter, playersHitThisExplosion);

        // Direcciones cardinales
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        foreach (Vector3 dir in directions)
        {
            for (int i = 1; i <= EffectiveExplosionRange; i++)
            {
                // Posición horizontal de la siguiente celda
                Vector3 horizontalCell = center + (dir * gridSize * i);
                
                // Proyecta la celda al piso real (vía, andén o escalón)
                Vector3 targetCell = GetGroundPosition(horizontalCell);

                bool hitIndestructible;
                bool hitDestructible;

                CheckCell(targetCell, playersHitThisExplosion, out hitIndestructible, out hitDestructible);

                if (hitIndestructible) break;

                cells.Add(targetCell);

                if (hitDestructible) break;
            }
        }

        return cells;
    }

/// Encuentra la cota real del suelo debajo de una coordenada, ignorando cajas y objetos rompibles
private Vector3 GetGroundPosition(Vector3 pos)
{
    Vector3 rayOrigin = new Vector3(pos.x, pos.y + 2.5f, pos.z);
    
    // Proyecta hacia abajo atravesando todos los colliders en el camino
    RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
    
    // Ordena los impactos de mayor a menor altura (de arriba hacia abajo)
    System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));

    foreach (var hit in hits)
    {
        // 1. Si impacta con una caja destructible, la ignora porque va a desaparecer en la explosión
        if (hit.collider.CompareTag("Destructible"))
        {
            continue;
        }

        // 2. Si impacta con la propia bomba o su modelo hijo, también la ignora
        if (hit.collider.GetComponentInParent<BombInteractable>() != null)
        {
            continue;
        }

        // 3. El primer collider sólido que no sea destructible ni la bomba es el suelo real (piso o andén)
        return new Vector3(pos.x, hit.point.y, pos.z);
    }

    // En caso de no encontrar ningún piso por debajo, conserva la posición original
    return pos;
}

    private void CheckAndDestroyCell(Vector3 cell, HashSet<ulong> playersHitThisExplosion)
    {
        CheckCell(cell, playersHitThisExplosion, out _, out _);
    }

    private void CheckCell(Vector3 cell, HashSet<ulong> playersHitThisExplosion, out bool hitIndestructible, out bool hitDestructible)
    {
        hitIndestructible = false;
        hitDestructible = false;
        HashSet<GameObject> dronesHitThisCell = new HashSet<GameObject>();

        // La caja se apoya desde el suelo (cell.y) elevándose +1.0m para cubrir la altura del personaje y cajas
        Vector3 boxCenter = new Vector3(cell.x, cell.y + 1.0f, cell.z);
        Vector3 halfExtents = new Vector3(gridSize * 0.45f, 1.0f, gridSize * 0.45f);
        Collider[] hits = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.identity, explosionLayerMask, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            // Ignorar la propia bomba
            if (hit.GetComponentInParent<BombInteractable>() != null) continue;

            // 1. Detección y aplicación de daño al Jugador
            PlayerStateManager player = hit.GetComponent<PlayerStateManager>();
            if (player == null) player = hit.GetComponentInParent<PlayerStateManager>();

            if (player != null)
            {
                ulong playerId = player.NetworkObjectId;
                if (!playersHitThisExplosion.Contains(playerId))
                {
                    playersHitThisExplosion.Add(playerId);
                    player.TakeDamageServerRpc();
                }
            }

            // 2. Obstáculos indestructibles (detienen la propagación)
            if (!hitIndestructible && hit.CompareTag("Indestructible"))
            {
                hitIndestructible = true;
            }

            // 3. Obstáculos destructibles
            if (!hitDestructible && hit.CompareTag("Destructible"))
            {
                hitDestructible = true;
                DestroyObjectAtPositionClientRpc(hit.transform.position);
                TrySpawnPowerUpDrop(hit.transform.position);
            }

            // 4. Drones
            if (hit.CompareTag("Drone"))
            {
                GameObject droneRoot = hit.transform.root.gameObject;
                if (dronesHitThisCell.Add(droneRoot))
                {
                    DroneBomberAI bombardero = hit.GetComponentInParent<DroneBomberAI>();
                    if (bombardero != null) bombardero.TakeDamage();

                    DroneShooterAI fusilero = hit.GetComponentInParent<DroneShooterAI>();
                    if (fusilero != null) fusilero.TakeDamage();
                }

                hitDestructible = true;
            }
        }
    }

    /// Spawnea las partículas en las cotas reales de cada casilla (con offset anti-parpadeo)
    [ClientRpc]
    private void SpawnVfxClientRpc(Vector3[] firePositions)
    {
        if (firePositions == null || firePositions.Length == 0) return;

        // Elevación mínima milimétrica sobre la cara del suelo para evitar Z-fighting
        float groundOffset = 0.02f;

        // Casilla central
        if (explosionVfx != null)
        {
            Vector3 centerPos = new Vector3(firePositions[0].x, firePositions[0].y + groundOffset, firePositions[0].z);
            Instantiate(explosionVfx, centerPos, Quaternion.identity);
        }

        // Casillas de los brazos de la cruz
        for (int i = 1; i < firePositions.Length; i++)
        {
            if (fireVfx != null)
            {
                Vector3 firePos = new Vector3(firePositions[i].x, firePositions[i].y + groundOffset, firePositions[i].z);
                Instantiate(fireVfx, firePos, Quaternion.identity);
            }
        }
    }

private void TrySpawnPowerUpDrop(Vector3 boxPosition)
{
    if (Random.value > powerUpDropChance) return;
    if (powerUpPrefabs == null || powerUpPrefabs.Length == 0) return;

    int index = Random.Range(0, powerUpPrefabs.Length);
    GameObject prefab = powerUpPrefabs[index];
    if (prefab == null) return;

    Vector3 spawnPos = new Vector3(boxPosition.x, powerUpSpawnHeight, boxPosition.z);
    GameObject dropInstance = Instantiate(prefab, spawnPos, Quaternion.identity);
    dropInstance.GetComponent<NetworkObject>().Spawn();
}

/// Se ejecuta en todos los clientes para destruir el objeto estético en esa ubicación
[ClientRpc]
private void DestroyObjectAtPositionClientRpc(Vector3 pos)
{
    // Busca en la máquina local cualquier collider destructible en esa coordenada
    Collider[] colliders = Physics.OverlapSphere(pos, 0.5f, explosionLayerMask);
    foreach (var col in colliders)
    {
        if (col.CompareTag("Destructible"))
        {
            Destroy(col.gameObject);
        }
    }
}

    [ClientRpc]
    private void UpdateBombCountClientRpc(int currentBombs)
    {
        activeBombs = currentBombs;
    }
}