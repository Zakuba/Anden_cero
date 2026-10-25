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
            // SI ESTAMOS EN EL MINIJUEGO: aplicar daño directo sin el filtro de MatchManager
            if (MiniJuegoManager.Instance != null)
            {
                MiniJuegoManager.Instance.ApplyMinijuegoDamageFromExternal(player);
            }
            else
            {
                // MODO COMPETITIVO / NORMAL: daño estándar
                player.TakeDamageServerRpc();
            }
        }

        // 2. Intentar detectar si lo que entró es el Dron Bombardero
        DroneBomberAI bomber = other.GetComponent<DroneBomberAI>();
        if (bomber == null)
        {
            bomber = other.GetComponentInParent<DroneBomberAI>();
        }

        if (bomber != null)
        {
            bomber.TakeDamage();
            return;
        }

        // 3. Intentar detectar si lo que entró es el Dron Fusilero
        DroneShooterAI shooter = other.GetComponent<DroneShooterAI>();
        if (shooter == null)
        {
            shooter = other.GetComponentInParent<DroneShooterAI>();
        }

        if (shooter != null)
        {
            shooter.TakeDamage();
            return;
        }

        DroneHealerAI healer = other.GetComponent<DroneHealerAI>();
        if (healer == null)
        {
            healer = other.GetComponentInParent<DroneHealerAI>();
        }

        if (healer != null)
        {
            healer.TakeDamage();
            return;
        }
    }
}
