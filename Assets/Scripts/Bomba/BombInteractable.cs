/*
using UnityEngine;
using Unity.Netcode;

public class BombInteractable : NetworkBehaviour
{
    [Header("Configuración de Desplazamiento")]
    [Tooltip("Velocidad a la que se desliza la bomba hacia la siguiente casilla.")]
    [SerializeField] private float slideSpeed = 12f;

    // Sincroniza el estado para que todos sepan que está en movimiento
    public NetworkVariable<bool> isMoving = new NetworkVariable<bool>(false);
    
    public bool IsPaused => isMoving.Value; 

    private Vector3 targetPosition;

    public void SlideTo(Vector3 destination)
    {
        if (!IsServer) return; // Solo el servidor autoriza el movimiento
        targetPosition = destination;
        isMoving.Value = true;
    }

    private void Update()
    {
        // El movimiento lo calcula el servidor. El componente NetworkTransform suavizará la vista en los clientes.
        if (!IsServer || !isMoving.Value) return;

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, slideSpeed * Time.deltaTime);
        // Al llegar exactamente al centro de la casilla destino
        // Aumentar la tolerancia de 0.01f a 0.05f para evitar problemas de precisión flotante
        if (Vector3.Distance(transform.position, targetPosition) <= 0.05f)
        {
            transform.position = targetPosition;// Anclaje matemático perfecto
            isMoving.Value = false;
        }
    }
}
*/

using UnityEngine;
using Unity.Netcode;

public class BombInteractable : NetworkBehaviour
{
    [Header("Configuración de Desplazamiento")]
    [Tooltip("Velocidad a la que se desliza la bomba hacia la siguiente casilla.")]
    [SerializeField] private float slideSpeed = 12f;

    public NetworkVariable<bool> isMoving = new NetworkVariable<bool>(false);
    public bool IsPaused => isMoving.Value; 

    private Vector3 targetPosition;

    public void SlideTo(Vector3 destination)
    {
        if (!IsServer) return;
        targetPosition = destination;
        isMoving.Value = true;
    }

    private void Update()
    {
        if (!IsServer || !isMoving.Value) return;

        // Desplazamiento horizontal hacia la casilla destino
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, slideSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) <= 0.05f)
        {
            transform.position = targetPosition;

            // Si fue bateada al vacío o a un escalón más bajo, cae al suelo inferior
            SnapToFloorBelow();

            isMoving.Value = false;
        }
    }

private void SnapToFloorBelow()
{
    // Lanza el rayo hacia abajo partiendo desde el centro de la bomba
    Vector3 origin = transform.position + Vector3.up * 0.1f;
    if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
    {
        if (hit.collider.gameObject != gameObject)
        {
            // hit.point.y es la superficie del suelo; sumamos 0.5f para que la bomba no se entierre al caer
            transform.position = new Vector3(transform.position.x, hit.point.y + 0.5f, transform.position.z);
        }
    }
}
}