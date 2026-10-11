using UnityEngine;
using Unity.Netcode;

public class HealthPickup : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
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
                if (NetworkObject != null && NetworkObject.IsSpawned)
                {
                    NetworkObject.Despawn(true);
                }
            }
            // Si wasHealed es false (vida al máximo), no hacemos nada y el corazón sigue en el suelo.
        }
    }
}