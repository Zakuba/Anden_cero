using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System;

public class NetworkBomb : NetworkBehaviour
{
    [Header("Configuración")]
    public float gridSize = 1f;
    public float bombTimer = 3f;
    public int explosionRange = 2;
    public LayerMask explosionLayerMask;
    [Tooltip("Radio o elevación para que la bomba descanse sobre el piso sin enterrarse.")]
    [SerializeField] private float bombRadiusOffset = 0.5f;

    [Header("VFX")]
    public GameObject explosionVfx;
    public GameObject fireVfx;

    // Evento para avisarle al jugador que la bomba explotó (y recupere su carga)
    public Action OnBombExploded; 

    public override void OnNetworkSpawn()
    {
        // Solo el servidor controla el tiempo y la posición inicial
        if (IsServer)
        {
            // Obtiene la altura real del piso y le suma el offset solo para que la bomba repose bien
            Vector3 pureFloorPos = GetFloorPosition(transform.position);
            transform.position = new Vector3(pureFloorPos.x, pureFloorPos.y + bombRadiusOffset, pureFloorPos.z);
            
            StartCoroutine(BombExplosionRoutine());
        }
    }

    /// Encuentra únicamente la cota Y real del suelo (sin offsets de radio)
    private Vector3 GetFloorPosition(Vector3 pos)
    {
        Vector3 rayOrigin = new Vector3(pos.x, pos.y + 2.5f, pos.z);
        
        // Proyecta hacia abajo atravesando los colliders en el camino
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
        
        // Ordena los impactos de mayor a menor altura (de arriba hacia abajo)
        System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));

        foreach (var hit in hits)
        {
            // 1. Si impacta con una caja destructible, la ignora
            if (hit.collider.CompareTag("Destructible"))
            {
                continue;
            }

            // 2. Si impacta con la propia bomba, también la ignora
            if (hit.collider.GetComponentInParent<NetworkBomb>() != null)
            {
                continue;
            }

            // 3. El primer collider sólido válido es el suelo real en su cota exacta
            return new Vector3(pos.x, hit.point.y, pos.z);
        }

        // Fallback si no encuentra piso por debajo
        return pos;
    }

    private IEnumerator BombExplosionRoutine()
    {
        yield return new WaitForSeconds(bombTimer);

        // 1. Calcula la expansión usando las alturas puras del suelo
        List<Vector3> affectedCells = CalculateExplosionCells(transform.position);

        // 2. Notifica a todos los clientes para los VFX en la superficie exacta del piso
        SpawnVfxClientRpc(affectedCells.ToArray());

        // 3. Espera breve para asegurar el paquete
        yield return new WaitForSeconds(0.05f);

        // 4. Avisa a quien la plantó y destruye el objeto de red
        OnBombExploded?.Invoke();

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }

    private List<Vector3> CalculateExplosionCells(Vector3 center)
    {
        List<Vector3> cells = new List<Vector3>();
        
        // 1. Creamos la lista temporal para esta explosión
        HashSet<ulong> playersHitThisExplosion = new HashSet<ulong>();

        // 2. Casilla central proyectada al suelo real puro
        Vector3 groundCenter = GetFloorPosition(center);
        cells.Add(groundCenter);
        CheckAndDestroyCell(groundCenter, playersHitThisExplosion);

        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        foreach (Vector3 dir in directions)
        {
            for (int i = 1; i <= explosionRange; i++)
            {
                // Posición horizontal de la siguiente celda
                Vector3 horizontalCell = center + (dir * gridSize * i);
                
                // Proyecta la celda al piso real puro
                Vector3 targetCell = GetFloorPosition(horizontalCell);
                
                // 3. Verifica colisiones en la celda proyectada
                CheckCell(targetCell, playersHitThisExplosion, out bool hitIndestructible, out bool hitDestructible);

                if (hitIndestructible) break;
                cells.Add(targetCell);
                if (hitDestructible) break;
            }
        }
        return cells;
    }

    private void CheckCell(Vector3 cell, HashSet<ulong> playersHitThisExplosion, out bool hitIndestructible, out bool hitDestructible)
    {
        hitIndestructible = false;
        hitDestructible = false;
        
        // La caja se apoya desde el suelo (cell.y) elevándose para cubrir la altura
        Vector3 boxCenter = new Vector3(cell.x, cell.y + 1.0f, cell.z);
        Vector3 halfExtents = new Vector3(gridSize * 0.45f, 2.0f, gridSize * 0.45f);
        Collider[] hits = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.identity, explosionLayerMask, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (hit.GetComponentInParent<NetworkBomb>() != null) continue;

            if (hit.CompareTag("Indestructible"))
            {
                hitIndestructible = true;
                break;
            }

            if (hit.CompareTag("Destructible"))
            {
                hitDestructible = true;
                break;
            }
            
            PlayerStateManager player = hit.GetComponent<PlayerStateManager>();
            if (player == null)
            {
                player = hit.GetComponentInParent<PlayerStateManager>();
            }

            if (player != null)
            {
                ulong playerId = player.NetworkObjectId;
                if (!playersHitThisExplosion.Contains(playerId))
                {
                    playersHitThisExplosion.Add(playerId);
                    player.TakeDamageServerRpc();
                }
            }
        }
    }

    private void CheckAndDestroyCell(Vector3 cell, HashSet<ulong> playersHitThisExplosion)
    {
        CheckCell(cell, playersHitThisExplosion, out _, out _);
    }

    [ClientRpc]
    private void SpawnVfxClientRpc(Vector3[] firePositions)
    {
        if (firePositions == null || firePositions.Length == 0) return;

        // Elevación milimétrica para evitar Z-fighting directamente sobre la superficie del suelo
        float groundOffset = 0.02f;

        // Casilla central pegada al piso real
        if (explosionVfx != null)
        {
            Vector3 centerPos = new Vector3(firePositions[0].x, firePositions[0].y + groundOffset, firePositions[0].z);
            Instantiate(explosionVfx, centerPos, Quaternion.identity);
        }

        // Brazos de la explosión alineados exactamente al nivel del suelo de cada celda
        for (int i = 1; i < firePositions.Length; i++)
        {
            if (fireVfx != null)
            {
                Vector3 firePos = new Vector3(firePositions[i].x, firePositions[i].y + groundOffset, firePositions[i].z);
                Instantiate(fireVfx, firePos, Quaternion.identity);
            }
        }
    }
}