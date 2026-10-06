using UnityEngine;
using Unity.Netcode;

public class FireHazard : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Solo el Host/Servidor autoriza y aplica el daño para evitar inconsistencias de red
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;

        // Intentar detectar si lo que entró es un jugador
        PlayerStateManager player = other.GetComponent<PlayerStateManager>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerStateManager>();
        }

        // Si el jugador está vivo, le aplica el daño reglamentario (aturdimiento / pérdida de vida / escudo)
        if (player != null && player.currentState.Value == PlayerState.Vivo)
        {
            player.TakeDamageServerRpc();
        }
    }
}
