
using UnityEngine;
using Unity.Netcode;

public class Teletransportador : NetworkBehaviour
{
    [Header("Configuración")]
    [SerializeField] private Transform puntoDestino;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
            return;

        NetworkObject jugador = other.GetComponentInParent<NetworkObject>();

        if (jugador == null || !jugador.IsPlayerObject)
            return;

        if (puntoDestino == null)
        {
            Debug.LogWarning("No se asignó un punto de destino.");
            return;
        }

        TeletransportarJugador(jugador);
    }

    private void TeletransportarJugador(NetworkObject jugador)
    {
        CharacterController controller =
            jugador.GetComponent<CharacterController>();

        if (controller != null)
            controller.enabled = false;

        jugador.transform.SetPositionAndRotation(
            puntoDestino.position,
            puntoDestino.rotation
        );

        if (controller != null)
            controller.enabled = true;
    }
}
