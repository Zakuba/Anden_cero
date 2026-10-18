using Unity.Netcode;
using UnityEngine;

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
