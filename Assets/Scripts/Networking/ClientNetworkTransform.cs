using UnityEngine;
using Unity.Netcode.Components;

// Hace que la autoridad de movimiento sea del dueño (owner), no del servidor.
// Asi cada jugador mueve su propio avatar localmente y se replica a los demas.
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}