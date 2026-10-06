using UnityEngine;
using Unity.Netcode;

public class PlayerBatMechanic : NetworkBehaviour
{
    [Header("Controles y Cuadrícula")]
    [SerializeField] private KeyCode batKey = KeyCode.E;
    [SerializeField] private float gridSize = 2.5f;

    [Header("Detección de Bombas")]
    [Tooltip("LayerMask exclusivo para detectar la bomba.")]
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
        // 1. Determinar dirección: Si mantiene una tecla de movimiento, batear en esa dirección;
        // de lo contrario, batear hacia el frente del personaje.
        Vector3 inputDir = GetInputOrFacingDirection();

        // 2. Buscar bombas en un radio de hasta 1 casilla a la redonda
        Vector3 playerCenter = transform.position + (Vector3.up * 0.5f);
        Collider[] hits = Physics.OverlapSphere(playerCenter, gridSize * 1.05f, bombLayerMask, QueryTriggerInteraction.Collide);

        BombInteractable targetBomb = null;
        float bestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            BombInteractable bomb = hit.GetComponent<BombInteractable>();
            if (bomb == null || bomb.isMoving.Value) continue;

            // Distancia horizontal plana
            Vector3 diff = bomb.transform.position - transform.position;
            diff.y = 0;
            float dist = diff.magnitude;

            // Condición mínima: Si el jugador está parado justo encima o dentro del centro geométrico, no permite batear
            if (dist < 0.35f) continue;

            // Selecciona la bomba más cercana dentro del rango de 1 celda
            if (dist < bestDistance)
            {
                bestDistance = dist;
                targetBomb = bomb;
            }
        }

        // 3. Si encontró una bomba válida, enviar la solicitud con la dirección al servidor
        if (targetBomb != null)
        {
            RequestBatBombServerRpc(new NetworkObjectReference(targetBomb.NetworkObject), inputDir);
        }
    }

    private Vector3 GetInputOrFacingDirection()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 moveInput = new Vector3(h, 0f, v);

        // Si hay input direccional activo, batear hacia donde apunta el input
        if (moveInput.sqrMagnitude > 0.01f)
        {
            return GetCardinalDirection(moveInput);
        }

        // Si no hay input de dirección, batear hacia el frente del avatar
        return GetCardinalDirection(transform.forward);
    }

    private Vector3 GetCardinalDirection(Vector3 dir)
    {
        dir.y = 0;
        dir.Normalize();
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
            return dir.x > 0 ? Vector3.right : Vector3.left;
        else
            return dir.z > 0 ? Vector3.forward : Vector3.back;
    }

    [ServerRpc]
    private void RequestBatBombServerRpc(NetworkObjectReference bombRef, Vector3 direction)
    {
        if (!bombRef.TryGet(out NetworkObject bombNetObj)) return;

        BombInteractable bomb = bombNetObj.GetComponent<BombInteractable>();
        if (bomb == null || bomb.isMoving.Value) return;

        // Validación de seguridad en el servidor: como máximo una casilla y media de tolerancia de red
        Vector3 diff = bomb.transform.position - transform.position;
        diff.y = 0;
        if (diff.magnitude > gridSize * 1.6f || diff.magnitude < 0.25f) return;

        // Determinar alcance según Power-Up
        int maxCellsToSlide = 1;
        if (playerStats != null && playerStats.BateoMultiplier > 1f)
        {
            maxCellsToSlide = Mathf.RoundToInt(playerStats.BateoMultiplier);
            if (maxCellsToSlide < 2) maxCellsToSlide = 2;
        }

        // Calcular trayecto casilla por casilla
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

            // Bloqueo explícito por Tags existentes
            if (col.CompareTag(indestructibleTag) || col.CompareTag(destructibleTag) || col.CompareTag(playerTag))
            {
                return true;
            }

            // Bloqueo de jugadores vivos
            if (col.GetComponent<CharacterController>() != null)
            {
                return true;
            }

            // Bloqueo de muros Untagged sólidos
            if (col.CompareTag("Untagged") && !col.isTrigger)
            {
                return true;
            }
        }

        return false;
    }
}