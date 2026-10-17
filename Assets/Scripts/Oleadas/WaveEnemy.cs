using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Va en cada prefab de dron (junto a NetworkObject). Los drones se destruyen
/// con NetworkObject.Despawn desde TakeDamage(); este componente le avisa al
/// WaveManager (solo en el servidor) para que descuente al enemigo.
/// </summary>
[DisallowMultipleComponent]
public class WaveEnemy : NetworkBehaviour
{
    [Tooltip("Desmarcar en el Dron Sanador: es aliado y no cuenta para completar la oleada.")]
    [SerializeField] private bool countsForWave = true;

    public bool CountsForWave => countsForWave;

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        if (WaveManager.Instance != null)
            WaveManager.Instance.NotifyEnemyDespawned(this);
    }
}
