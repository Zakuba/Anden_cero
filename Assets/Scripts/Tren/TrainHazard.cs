using UnityEngine;
using Unity.Netcode;

public class TrainHazard : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Solo el servidor/Host autoriza la muerte para mantener la coherencia de red
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;

        // Intentar obtener el gestor de estados del jugador
        PlayerStateManager player = other.GetComponent<PlayerStateManager>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerStateManager>();
        }

        if (player != null && player.currentState.Value != PlayerState.Muerto)
        {
            Debug.Log($"[TrainHazard] ¡El tren arrolló a {player.gameObject.name}!");
            player.InstantKillServerRpc();
        }

        DroneBomberAI dronBom = other.GetComponent<DroneBomberAI>();
        if(dronBom == null)
        {
            dronBom = other.GetComponentInParent<DroneBomberAI>();
        }

        if(dronBom != null)
        {
            dronBom.TakeDamage();
        }

        DroneShooterAI dronShoot = other.GetComponent<DroneShooterAI>();
        if(dronShoot == null)
        {
            dronShoot = other.GetComponentInParent<DroneShooterAI>();
        }

        if(dronShoot != null)
        {
            dronShoot.TakeDamage();
        }
    }
}
