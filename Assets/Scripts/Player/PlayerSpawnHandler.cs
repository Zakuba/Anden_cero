using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerSpawnHandler : NetworkBehaviour
{
    // El servidor lo llama; se ejecuta solo en el dueño del avatar
    [Rpc(SendTo.Owner)]
    public void TeleportToSpawnRpc(Vector3 position, Quaternion rotation)
    {
        var controller = GetComponent<CharacterController>();
        var netTransform = GetComponent<NetworkTransform>();

        // El CharacterController pisa la posición si está activo, así que se apaga un instante
        bool wasEnabled = controller.enabled;
        controller.enabled = false;

        if (netTransform != null)
            netTransform.Teleport(position, rotation, transform.localScale);
        else
            transform.SetPositionAndRotation(position, rotation);

        controller.enabled = wasEnabled;
    }
}