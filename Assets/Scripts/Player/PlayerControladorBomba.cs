using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class PlayerBombController : NetworkBehaviour
{
    [Header("Configuración de Bomba")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private float gridSize = 2.5f;
    [SerializeField] private float bombTimer = 3f;
    [SerializeField] private int explosionRange = 2;
    [SerializeField] private LayerMask explosionLayerMask;
    [SerializeField] private KeyCode plantKey = KeyCode.Space;
    [SerializeField] private float alturaspawnbomba = -1f; 

    [Header("Efectos Visuales (VFX)")]
    [SerializeField] private GameObject explosionVfx;
    [SerializeField] private GameObject fireVfx;

       
    private int activeBombs = 0;
    private int maxBombs = 1;

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(plantKey) && activeBombs < maxBombs)
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
        if (activeBombs >= maxBombs) return;

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

    /// <summary>
    /// Cálculo autoritativo en el Servidor: determina daños y obstáculos
    /// </summary>
    private List<Vector3> CalculateExplosionCells(Vector3 center)
    {
        List<Vector3> cells = new List<Vector3> { center };
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        foreach (Vector3 dir in directions)
        {
            for (int i = 1; i <= explosionRange; i++)
            {
                Vector3 targetCell = center + (dir * gridSize * i);
                Collider[] hits = Physics.OverlapSphere(targetCell, gridSize * 0.4f, explosionLayerMask);

                bool hitIndestructible = false;
                bool hitDestructible = false;

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
                        // Aquí podés destruir el bloque en el servidor
                    }
                }

                if (hitIndestructible) break;

                cells.Add(targetCell);

                if (hitDestructible) break;
            }
        }

        return cells;
    }

    /// <summary>
    /// Se ejecuta en TODOS los clientes para mostrar las partículas
    /// </summary>
    [ClientRpc]
    private void SpawnVfxClientRpc(Vector3[] firePositions)
    {
        if (firePositions == null || firePositions.Length == 0) return;

        // Casilla central
        if (explosionVfx != null)
        {
            Instantiate(explosionVfx, firePositions[0], Quaternion.identity);
        }

        // Casillas de los brazos de la cruz
        for (int i = 1; i < firePositions.Length; i++)
        {
            if (fireVfx != null)
            {
                Instantiate(fireVfx, firePositions[i], Quaternion.identity);
            }
        }
    }

    [ClientRpc]
    private void UpdateBombCountClientRpc(int currentBombs)
    {
        activeBombs = currentBombs;
    }
}