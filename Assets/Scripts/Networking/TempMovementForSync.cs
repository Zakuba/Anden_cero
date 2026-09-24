using Unity.Netcode;
using UnityEngine;

// Movimiento simple, SOLO para validar la sincronizacion de HU-01.3.
// El movimiento real en cuadricula es responsabilidad de HU-02.1.
public class TempMovementForSync : NetworkBehaviour
{
    [SerializeField] private float speed = 3f;

    private void Update()
    {
        if (!IsOwner) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 move = new Vector3(h, 0f, v) * speed * Time.deltaTime;
        transform.position += move;
    }
}