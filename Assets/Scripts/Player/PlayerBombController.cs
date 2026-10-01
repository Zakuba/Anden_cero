using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class PlayerBombController : NetworkBehaviour
{
    [Header("Configuración de Bomba")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float bombTimer = 3f;
    [SerializeField] private int explosionRange = 2;
    [SerializeField] private LayerMask explosionLayerMask;
    [SerializeField] private KeyCode plantKey = KeyCode.Space;
    [SerializeField] private float alturaspawnbomba = -1f; 

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

    private void Awake()
{
    playerStats = GetComponent<PlayerStats>();
}

private int EffectiveMaxBombs => maxBombs + (playerStats != null ? playerStats.MaxBombsBonus : 0);
private int EffectiveExplosionRange => explosionRange + (playerStats != null ? playerStats.ExplosionRangeBonus : 0);

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(plantKey) && activeBombs < EffectiveMaxBombs)
        {
            Vector3 spawnPosition = GetGridCenter(transform.position);
            RequestPlantBombServerRpc(spawnPosition);
        }
    }

    private Vector3 GetGridCenter(Vector3 playerPos)
    {
        float x = Mathf.Round(playerPos.x / gridSize) * gridSize;
        float z = Mathf.Round(playerPos.z / gridSize) * gridSize;
        return new Vector3(x, alturaspawnbomba, z);
    }

    [ServerRpc]
    private void RequestPlantBombServerRpc(Vector3 spawnPosition)
    {
       if (activeBombs >= EffectiveMaxBombs) return;

        activeBombs++;
        UpdateBombCountClientRpc(activeBombs);

        GameObject bombInstance = Instantiate(bombPrefab, spawnPosition, Quaternion.identity);
        NetworkObject bombNetObj = bombInstance.GetComponent<NetworkObject>();
        bombNetObj.Spawn();

        StartCoroutine(BombExplosionRoutine(bombNetObj, spawnPosition));
    }

    private IEnumerator BombExplosionRoutine(NetworkObject bombNetObj, Vector3 centerPos)
    {
        yield return new WaitForSeconds(bombTimer);

        if (bombNetObj != null && bombNetObj.IsSpawned)
        {
            // 1. El servidor calcula la expansión y recopila las posiciones válidas
            List<Vector3> affectedCells = CalculateExplosionCells(centerPos);

            // 2. Notifica a todos los clientes (Host y Cliente) dónde spawnear el fuego
            SpawnVfxClientRpc(affectedCells.ToArray());

            // 3. Breve espera para garantizar que el paquete ClientRpc salga antes del despawn
            yield return new WaitForSeconds(0.05f);

            if (bombNetObj != null && bombNetObj.IsSpawned)
            {
                bombNetObj.Despawn();
            }
        }

        activeBombs--;
        UpdateBombCountClientRpc(activeBombs);
    }

/// Cálculo autoritativo en el Servidor: determina daños y obstáculos
private List<Vector3> CalculateExplosionCells(Vector3 center)
{
    List<Vector3> cells = new List<Vector3> { center };

    // --- CHEQUEO DE LA CASILLA CENTRAL ---
    CheckAndDestroyCell(center);

    // --- CHEQUEO DE LAS DIRECCIONES ---
    Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

    foreach (Vector3 dir in directions)
    {
        for (int i = 1; i <= EffectiveExplosionRange; i++)
        {
            Vector3 targetCell = center + (dir * gridSize * i);

            bool hitIndestructible;
            bool hitDestructible;

            CheckCell(targetCell, out hitIndestructible, out hitDestructible);

            if (hitIndestructible) break;

            cells.Add(targetCell);

            if (hitDestructible) break;
        }
    }

    return cells;
}

private void CheckCell(Vector3 cell, out bool hitIndestructible, out bool hitDestructible)
{
    hitIndestructible = false;
    hitDestructible = false;

    Vector3 halfExtents = new Vector3(gridSize * 0.45f, 2.0f, gridSize * 0.45f);
    Collider[] hits = Physics.OverlapBox(cell, halfExtents, Quaternion.identity, explosionLayerMask, QueryTriggerInteraction.Collide);

    foreach (Collider hit in hits)
    {
        if (hit.CompareTag("Indestructible"))
        {
            hitIndestructible = true;
            break;
        }

      if (hit.CompareTag("Destructible"))
{
    hitDestructible = true;
    DestroyObjectAtPositionClientRpc(hit.transform.position);
    TrySpawnPowerUpDrop(hit.transform.position);
    break;
}
    }
}

private void CheckAndDestroyCell(Vector3 cell)
{
    CheckCell(cell, out _, out _);
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

    /// Se ejecuta en TODOS los clientes para mostrar las partículas
    [ClientRpc]
    private void SpawnVfxClientRpc(Vector3[] firePositions)
    {
        if (firePositions == null || firePositions.Length == 0) return;

        // Casilla central forzada a ras del piso
        if (explosionVfx != null)
        {
            Vector3 centerPos = new Vector3(firePositions[0].x, -1.99f, firePositions[0].z);
            Instantiate(explosionVfx, centerPos, Quaternion.identity);
        }

        // Casillas de los brazos de la cruz forzadas a ras del piso
        for (int i = 1; i < firePositions.Length; i++)
        {
            if (fireVfx != null)
            {
                Vector3 firePos = new Vector3(firePositions[i].x, -1.99f, firePositions[i].z);
                Instantiate(fireVfx, firePos, Quaternion.identity);
            }
        }
    }

    [ClientRpc]
    private void UpdateBombCountClientRpc(int currentBombs)
    {
        activeBombs = currentBombs;
    }
}