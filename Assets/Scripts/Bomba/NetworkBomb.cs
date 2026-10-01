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

    [Header("VFX")]
    public GameObject explosionVfx;
    public GameObject fireVfx;

    // Evento para avisarle al jugador que la bomba explotó (y recupere su carga)
    public Action OnBombExploded; 

    public override void OnNetworkSpawn()
    {
        // Solo el servidor controla el tiempo y la explosión
        if (IsServer)
        {
            StartCoroutine(BombExplosionRoutine());
        }
    }

    private IEnumerator BombExplosionRoutine()
    {
        yield return new WaitForSeconds(bombTimer);

        // 1. Calcula la expansión
        List<Vector3> affectedCells = CalculateExplosionCells(transform.position);

        // 2. Notifica a todos los clientes para los VFX
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
        List<Vector3> cells = new List<Vector3> { center };
        CheckAndDestroyCell(center);

        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        foreach (Vector3 dir in directions)
        {
            for (int i = 1; i <= explosionRange; i++)
            {
                Vector3 targetCell = center + (dir * gridSize * i);
                CheckCell(targetCell, out bool hitIndestructible, out bool hitDestructible);

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
                break;
            }
        }
    }

    private void CheckAndDestroyCell(Vector3 cell)
    {
        CheckCell(cell, out _, out _);
    }

    [ClientRpc]
    private void DestroyObjectAtPositionClientRpc(Vector3 pos)
    {
        Collider[] colliders = Physics.OverlapSphere(pos, 0.5f, explosionLayerMask);
        foreach (var col in colliders)
        {
            if (col.CompareTag("Destructible")) Destroy(col.gameObject);
        }
    }

    [ClientRpc]
    private void SpawnVfxClientRpc(Vector3[] firePositions)
    {
        if (firePositions == null || firePositions.Length == 0) return;

        if (explosionVfx != null)
        {
            Instantiate(explosionVfx, new Vector3(firePositions[0].x, -1.99f, firePositions[0].z), Quaternion.identity);
        }

        for (int i = 1; i < firePositions.Length; i++)
        {
            if (fireVfx != null)
            {
                Instantiate(fireVfx, new Vector3(firePositions[i].x, -1.99f, firePositions[i].z), Quaternion.identity);
            }
        }
    }
}
