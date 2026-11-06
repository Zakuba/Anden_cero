using UnityEngine;
using Unity.Netcode;

public class BombInteractable : NetworkBehaviour
{
    [Header("Configuración de Desplazamiento")]
    [Tooltip("Velocidad a la que se desliza la bomba hacia la siguiente casilla.")]
    [SerializeField] private float slideSpeed = 12f;

    public NetworkVariable<bool> isMoving = new NetworkVariable<bool>(false);
    public bool IsPaused => isMoving.Value; 

    // Rastrear el dueño actual de la bomba (plantador o último bateador)
    public NetworkVariable<ulong> currentOwnerClientId = new NetworkVariable<ulong>(
        999999, // ID por defecto no válido
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private Vector3 targetPosition;

    public void SetInitialOwner(ulong ownerId)
    {
        if (!IsServer) return;
        currentOwnerClientId.Value = ownerId;
    }

    public void SetNewHitter(ulong hitterClientId)
    {
        if (!IsServer) return;
        currentOwnerClientId.Value = hitterClientId;
    }
    
    public void SlideTo(Vector3 destination)
    {
        if (!IsServer) return;
        targetPosition = destination;
        isMoving.Value = true;
    }

    /// <summary>
    /// Comprueba si la bomba está apoyada en el suelo antes de permitir el bateo.
    /// </summary>
    public bool IsGrounded()
    {
        // El centro de la bomba está a +0.5m del suelo. Lanzamos un rayo hacia abajo de 0.65m.
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.65f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.gameObject != gameObject && !hit.collider.isTrigger)
            {
                return true;
            }
        }
        return false;
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
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.gameObject != gameObject)
            {
                transform.position = new Vector3(transform.position.x, hit.point.y + 0.5f, transform.position.z);
            }
        }
    }
}