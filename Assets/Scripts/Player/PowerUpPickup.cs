using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class PowerUpPickup : NetworkBehaviour
{
    [Header("Configuración del Drop")]
    [SerializeField] private PowerUpType powerUpType;
    [SerializeField] private float lifetimeSeconds = 15f;
    [SerializeField] private float pickupRadius = 0.5f;
    [SerializeField] private float checkInterval = 0.1f;

    private bool isCollected = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        StartCoroutine(LifetimeRoutine());
        StartCoroutine(PickupCheckRoutine());
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetimeSeconds);

        if (!isCollected && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }

    private IEnumerator PickupCheckRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(checkInterval);

        while (!isCollected)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, pickupRadius, ~0, QueryTriggerInteraction.Collide);

            foreach (Collider hit in hits)
            {
                if (!hit.CompareTag("Player")) continue;

                PlayerStats stats = hit.GetComponentInParent<PlayerStats>();
                if (stats == null) continue;

                // Si ya tiene un power-up activo, lo dejamos tirado para otro jugador
                if (stats.HasActivePowerUp) continue;

                isCollected = true;
                stats.RequestPickupPowerUpServerRpc(powerUpType);

                if (NetworkObject.IsSpawned)
                {
                    NetworkObject.Despawn();
                }
                yield break;
            }

            yield return wait;
        }
    }
}