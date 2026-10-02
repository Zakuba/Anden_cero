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
        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition; // Anclaje matemático perfecto
            isMoving.Value = false;
        }
    }
}