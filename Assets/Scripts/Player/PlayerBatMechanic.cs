using UnityEngine;
using Unity.Netcode;

public class PlayerBatMechanic : NetworkBehaviour
{
    [Header("Controles y Cuadrícula")]
    [SerializeField] private KeyCode batKey = KeyCode.E;
    [SerializeField] private float gridSize = 2.5f;

    [Header("Detección de Bombas")]
    [Tooltip("LayerMask exclusivo para detectar la bomba frente al jugador.")]
    [SerializeField] private LayerMask bombLayerMask;

    [Header("Tags de Obstáculos")]
    [SerializeField] private string indestructibleTag = "Indestructible";
    [SerializeField] private string destructibleTag = "Destructible";
    [SerializeField] private string playerTag = "Player";

    private PlayerStats playerStats;

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(batKey))
        {
            Vector3 cardinalDir = GetCardinalDirection(transform.forward);
            RequestBatBombServerRpc(cardinalDir);
        }
    }

    private Vector3 GetCardinalDirection(Vector3 forward)
    {
        forward.y = 0;
        forward.Normalize();
        if (Mathf.Abs(forward.x) > Mathf.Abs(forward.z))
            return forward.x > 0 ? Vector3.right : Vector3.left;
        else
            return forward.z > 0 ? Vector3.forward : Vector3.back;
    }

    [ServerRpc]
    private void RequestBatBombServerRpc(Vector3 direction)
    {
        Vector3 origin = transform.position + (Vector3.up * 0.5f);
        if (Physics.SphereCast(origin, 0.4f, direction, out RaycastHit hit, gridSize, bombLayerMask, QueryTriggerInteraction.Collide))
        {
            BombInteractable bomb = hit.collider.GetComponent<BombInteractable>();

            if (bomb != null && !bomb.isMoving.Value)
            {
                // Determina la cantidad de casillas según el power-up activo
                // Sin guante = 1 casilla. Con guante (multiplicador >= 1.5) = 2 casillas (o según el valor redondeado)
                int maxCellsToSlide = 1;
                if (playerStats != null && playerStats.BateoMultiplier > 1f)
                {
                    maxCellsToSlide = Mathf.RoundToInt(playerStats.BateoMultiplier); 
                    if (maxCellsToSlide < 2) maxCellsToSlide = 2; // Garantiza mínimo 2 casillas con el guante
                }

                // Proyecta casilla por casilla en línea recta hasta encontrar un obstáculo
                Vector3 currentTarget = bomb.transform.position;
                Vector3 finalDestination = bomb.transform.position;

                for (int i = 1; i <= maxCellsToSlide; i++)
                {
                    Vector3 nextCell = bomb.transform.position + (direction * gridSize * i);

                    if (!IsCellBlocked(nextCell))
                    {
                        finalDestination = nextCell; // Casilla válida para deslizarse
                    }
                    else
                    {
                        break; // Se frena en seco al topar con el primer obstáculo
                    }
                }

                // Si al menos pudo avanzar una casilla, inicia el deslizamiento
                if (finalDestination != bomb.transform.position)
                {
                    bomb.SlideTo(finalDestination);
                }
            }
        }
    }

    private bool IsCellBlocked(Vector3 cellCenter)
    {
        Vector3 boxCenter = new Vector3(cellCenter.x, 0.5f, cellCenter.z);
        Vector3 halfExtents = new Vector3(gridSize * 0.45f, 0.8f, gridSize * 0.45f);

        Collider[] colliders = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider col in colliders)
        {
            if (col.GetComponent<BombInteractable>() != null) continue;

            if (col.CompareTag(indestructibleTag) || col.CompareTag(destructibleTag))
            {
                return true;
            }

            if (col.CompareTag(playerTag) || col.GetComponent<CharacterController>() != null)
            {
                return true;
            }
        }

        return false;
    }
}