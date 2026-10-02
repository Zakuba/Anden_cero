using UnityEngine;
using Unity.Netcode;

public class PlayerBatMechanic : NetworkBehaviour
{
    [Header("Controles y Cuadrícula")]
    [SerializeField] private KeyCode batKey = KeyCode.E;
    [SerializeField] private float gridSize = 2.5f;

    [Header("Detección de Bombas")]
    [SerializeField] private LayerMask bombLayerMask;

    [Header("Tags de Obstáculos")]
    [SerializeField] private string indestructibleTag = "Indestructible";
    [SerializeField] private string destructibleTag = "Destructible";
    [SerializeField] private string playerTag = "Player";

    private PlayerStats playerStats;
    private PlayerStateManager stateManager;

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        stateManager = GetComponent<PlayerStateManager>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Bloqueo si el jugador está aturdido o muerto
        if (stateManager != null && stateManager.currentState.Value != PlayerState.Vivo) return;

        if (Input.GetKeyDown(batKey))
        {
            TryBatLocal();
        }
    }

    private void TryBatLocal()
    {
        Vector3 cardinalDir = GetCardinalDirection(transform.forward);
        Vector3 origin = transform.position + (Vector3.up * 0.5f);

        // El cliente local detecta la bomba directamente frente a sus ojos
        if (Physics.SphereCast(origin, 0.45f, cardinalDir, out RaycastHit hit, gridSize * 1.1f, bombLayerMask, QueryTriggerInteraction.Collide))
        {
            BombInteractable bomb = hit.collider.GetComponent<BombInteractable>();

            if (bomb != null && !bomb.isMoving.Value)
            {
                // Enviar la referencia directa al servidor
                RequestBatBombServerRpc(new NetworkObjectReference(bomb.NetworkObject), cardinalDir);
            }
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
    private void RequestBatBombServerRpc(NetworkObjectReference bombRef, Vector3 direction)
    {
        // 1. Resolver el objeto en el servidor
        if (!bombRef.TryGet(out NetworkObject bombNetObj)) return;

        BombInteractable bomb = bombNetObj.GetComponent<BombInteractable>();
        if (bomb == null || bomb.isMoving.Value) return;

        // 2. Validación de proximidad de seguridad (evita exploits si el jugador estuviera lejos)
        if (Vector3.Distance(transform.position, bomb.transform.position) > gridSize * 2.2f) return;

        // 3. Determinar alcance según Power-Up
        int maxCellsToSlide = 1;
        if (playerStats != null && playerStats.BateoMultiplier > 1f)
        {
            maxCellsToSlide = Mathf.RoundToInt(playerStats.BateoMultiplier);
            if (maxCellsToSlide < 2) maxCellsToSlide = 2;
        }

        // 4. Calcular el trayecto casilla por casilla
        Vector3 finalDestination = bomb.transform.position;

        for (int i = 1; i <= maxCellsToSlide; i++)
        {
            Vector3 nextCell = bomb.transform.position + (direction * gridSize * i);

            if (!IsCellBlocked(nextCell))
            {
                finalDestination = nextCell;
            }
            else
            {
                break; // Se frena en seco ante el primer obstáculo
            }
        }

        if (finalDestination != bomb.transform.position)
        {
            bomb.SlideTo(finalDestination);
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