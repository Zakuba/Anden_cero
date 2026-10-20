using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class HealthPickup : NetworkBehaviour
{
    [Header("Tiempo de vida")]
    [SerializeField] private float lifetimeSeconds = 10f;

    [Header("FX de despawn (opcional)")]
    [SerializeField] private GameObject fxDespawn;
    [SerializeField] private float duracionFX = 1f;

    [Header("Render del corazón (opcional)")]
    [Tooltip("Se oculta en todos los peers cuando se agota el tiempo, antes de despawnear.")]
    [SerializeField] private GameObject renderCorazon;

    // Evita curar dos veces si varios colliders del mismo jugador tocan el corazón en el mismo paso de física.
    // También se usa para bloquear la recogida una vez que el corazón empezó a desaparecer por tiempo.
    private bool consumed;

    public override void OnNetworkSpawn()
    {
        // El FX arranca apagado en todos los peers
        if (fxDespawn != null)
        {
            fxDespawn.SetActive(false);
        }

        // El tiempo de vida lo controla solo el servidor
        if (!IsServer) return;

        StartCoroutine(LifetimeRoutine());
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetimeSeconds);

        // Si ya lo recogieron (o ya se despawneó), no hay nada que hacer
        if (consumed || !NetworkObject.IsSpawned)
            yield break;

        // Desde acá ya no se puede recoger, aunque el FX siga en pantalla
        consumed = true;

        bool hayFX = fxDespawn != null || renderCorazon != null;

        if (hayFX)
        {
            MostrarFXDespawnRpc();
            yield return new WaitForSeconds(duracionFX);
        }

        if (NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void MostrarFXDespawnRpc()
    {
        if (renderCorazon != null)
        {
            renderCorazon.SetActive(false);
        }

        if (fxDespawn != null)
        {
            fxDespawn.SetActive(true);
        }
    }

    // OnTriggerStay (y no Enter): si el jugador está parado sobre el corazón con la vida al máximo
    // y después recibe daño, lo recoge sin tener que salir y volver a entrar.
    private void OnTriggerStay(Collider other)
    {
        if (consumed) return;

        // 1. Autoridad absoluta del Servidor
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            return;

        // 2. Detectar si lo que tocó el corazón es un jugador
        PlayerStateManager player = other.GetComponent<PlayerStateManager>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerStateManager>();
        }

        // 3. Si es un jugador válido, intentamos curarlo
        if (player != null)
        {
            bool wasHealed = player.TryHeal();

            // 4. SÓLO si la curación fue exitosa (el jugador tenía espacio para un corazón), despawneamos el objeto
            if (wasHealed)
            {
                consumed = true;

                if (NetworkObject != null && NetworkObject.IsSpawned)
                {
                    NetworkObject.Despawn(true);
                }
            }
            // Si wasHealed es false (vida al máximo), no hacemos nada y el corazón sigue en el suelo.
        }
    }
}