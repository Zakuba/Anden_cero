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

    [Header("FX De despawn")]
    [SerializeField] private GameObject fxDespawnPoweUp;
    [SerializeField] private float duracionFX = 2f;

    [Header("Objeto PU")]
    [SerializeField] private GameObject ObjetoPU;

    [Header("Render del PowerUp")]
    [SerializeField] private GameObject renderPowerUp;

    private bool isCollected = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Aseguramos que el FX empiece apagado
        if (fxDespawnPoweUp != null)
        {
            fxDespawnPoweUp.SetActive(false);
        }

        StartCoroutine(LifetimeRoutine());
        StartCoroutine(PickupCheckRoutine());
    }

    private IEnumerator LifetimeRoutine()
    {
        // Esperar el tiempo de vida del PowerUp
        yield return new WaitForSeconds(lifetimeSeconds);

        if (!isCollected && NetworkObject.IsSpawned)
        {
            Vector3 posicionFX = transform.position;

            if (ObjetoPU != null)
            {
                posicionFX = ObjetoPU.transform.position;
            }

            // Ocultar el render del PowerUp
            if (renderPowerUp != null)
            {
                renderPowerUp.SetActive(false);
            }

            // Activar el FX en todos los clientes
            MostrarFXDespawnRpc(posicionFX);

            // Esperar a que termine el FX
            yield return new WaitForSeconds(duracionFX);

            // Despawnear el PowerUp
            if (NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    private void MostrarFXDespawnRpc(Vector3 posicion)
    {
        if (fxDespawnPoweUp == null)
            return;

        // Colocar el FX en la posición del PowerUp
        fxDespawnPoweUp.transform.position = posicion;

        // Activar el FX
        fxDespawnPoweUp.SetActive(true);
    }

    private IEnumerator PickupCheckRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(checkInterval);

        while (!isCollected)
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                pickupRadius,
                ~0,
                QueryTriggerInteraction.Collide
            );

            foreach (Collider hit in hits)
            {
                if (!hit.CompareTag("Player"))
                    continue;

                PlayerStats stats =
                    hit.GetComponentInParent<PlayerStats>();

                if (stats == null)
                    continue;

                if (stats.HasActivePowerUp)
                    continue;

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