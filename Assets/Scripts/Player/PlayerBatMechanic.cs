
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

        if (MatchManager.Instance != null && !MatchManager.Instance.IsMatchRunning) return;
        if (stateManager != null && stateManager.currentState.Value != PlayerState.Vivo) return;

        if (Input.GetKeyDown(batKey))
        {
            TryBatLocal();
        }
    }

private void TryBatLocal()
    {
        Vector3 inputDir = GetInputOrFacingDirection();
        Vector3 searchCenter = transform.position - (Vector3.up * 0.5f);
        
        Collider[] hits = Physics.OverlapSphere(searchCenter, gridSize * 1.25f, bombLayerMask, QueryTriggerInteraction.Collide);
        Debug.Log($"[Bat-Local] Tecla pulsada. Colliders detectados en bombLayerMask: {hits.Length}");

        BombInteractable targetBomb = null;
        float bestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            BombInteractable bomb = hit.GetComponentInParent<BombInteractable>();
            if (bomb == null)
            {
                Debug.LogWarning($"[Bat-Local] Objeto tocado ({hit.name}) no tiene BombInteractable en padres.");
                continue;
            }

            if (bomb.isMoving.Value)
            {
                Debug.LogWarning("[Bat-Local] La bomba ya se está moviendo.");
                continue;
            }

            float verticalDiff = Mathf.Abs(bomb.transform.position.y - (transform.position.y - 1f));
            if (verticalDiff > 2.5f)
            {
                Debug.LogWarning($"[Bat-Local] Descartada por desnivel excesivo: {verticalDiff:F2}m");
                continue;
            }

            Vector3 diff = bomb.transform.position - transform.position;
            diff.y = 0;
            float dist = diff.magnitude;

            if (dist < 0.35f)
            {
                Debug.LogWarning($"[Bat-Local] Descartada por estar encima de la bomba: {dist:F2}m");
                continue;
            }

            if (dist < bestDistance)
            {
                bestDistance = dist;
                targetBomb = bomb;
            }
        }

        if (targetBomb != null)
        {
            Debug.Log($"[Bat-Local] Bomba válida encontrada ({targetBomb.name}). Enviando RPC al servidor...");
            RequestBatBombServerRpc(new NetworkObjectReference(targetBomb.NetworkObject), inputDir);
        }
        else
        {
            Debug.LogError("[Bat-Local] No se seleccionó ninguna bomba válida.");
        }
    }

    private Vector3 GetInputOrFacingDirection()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 moveInput = new Vector3(h, 0f, v);

        if (moveInput.sqrMagnitude > 0.01f)
        {
            return GetCardinalDirection(moveInput);
        }

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
    if (MatchManager.Instance != null && !MatchManager.Instance.IsMatchRunning) return;

    if (!bombRef.TryGet(out NetworkObject bombNetObj)) return;

    BombInteractable bomb = bombNetObj.GetComponent<BombInteractable>();
    if (bomb == null || bomb.isMoving.Value) return;

    Vector3 diff = bomb.transform.position - transform.position;
    diff.y = 0;
    if (diff.magnitude > gridSize * 1.8f || diff.magnitude < 0.25f) return;

    int maxCellsToSlide = 1;
    if (playerStats != null && playerStats.BateoMultiplier > 1f)
    {
        maxCellsToSlide = Mathf.RoundToInt(playerStats.BateoMultiplier);
        if (maxCellsToSlide < 2) maxCellsToSlide = 2;
    }

    Vector3 currentBombPos = bomb.transform.position;
    Vector3 finalDestination = currentBombPos;

    for (int i = 1; i <= maxCellsToSlide; i++)
    {
        Vector3 nextCell = currentBombPos + (direction * gridSize * i);

        // 1. Validar que la casilla destino esté libre
        // 2. Validar que en el trayecto recto NO haya un muro cruzándose en el camino
        if (!IsCellBlocked(nextCell) && !IsPathBlocked(currentBombPos, direction, gridSize * i))
        {
            finalDestination = nextCell;
        }
        else
        {
            Debug.LogWarning($"[Bat-Server] Movimiento bloqueado hacia {nextCell}");
            break;
        }
    }

    if (finalDestination != currentBombPos)
    {
        // 1. Deslizar hacia la casilla libre calculada
        bomb.SlideTo(finalDestination);

        // 2. Transferir la autoría de la bomba al jugador que la acaba de batear
        bomb.SetNewHitter(OwnerClientId);

        // 3. Notificar el logro de bateo exitoso
        NotifySuccessfulBatClientRpc();
    }
}

/// Lanza un rayo a la altura del cuerpo de la bomba para asegurar que ninguna pared intermedia sea atravesada.
private bool IsPathBlocked(Vector3 startPos, Vector3 dir, float distance)
{
    Vector3 origin = startPos + Vector3.up * 0.5f;
    RaycastHit[] hits = Physics.RaycastAll(origin, dir, distance, ~0, QueryTriggerInteraction.Ignore);

    foreach (var hit in hits)
    {
        if (hit.collider.GetComponentInParent<BombInteractable>() != null) continue;
        if (hit.collider.isTrigger) continue;

        if (hit.collider.CompareTag(indestructibleTag) || hit.collider.CompareTag(destructibleTag))
        {
            Debug.LogWarning($"[Bat-Block] Trayecto bloqueado por muro: {hit.collider.name}");
            return true;
        }
    }

    return false;
}

    [ClientRpc]
    private void NotifySuccessfulBatClientRpc()
    {
        if (IsOwner && AchievementManager.Instance != null)
        {
            AchievementManager.Instance.AddBatStat();
        }
    }

private bool IsCellBlocked(Vector3 cellCenter)
{
    // Aumentamos la altura de la caja a 1.2m para cubrir paredes completas y desniveles
    Vector3 boxCenter = new Vector3(cellCenter.x, cellCenter.y + 0.6f, cellCenter.z);
    Vector3 halfExtents = new Vector3(gridSize * 0.45f, 0.8f, gridSize * 0.45f);

    Collider[] colliders = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);

    foreach (Collider col in colliders)
    {
        if (col.GetComponentInParent<BombInteractable>() != null) continue;
        if (col.isTrigger) continue;

        // 1. Bloqueo inmediato por Tags de obstáculo
        if (col.CompareTag(indestructibleTag) || col.CompareTag(destructibleTag) || col.CompareTag(playerTag))
        {
            Debug.LogWarning($"[Bat-Block] Casilla bloqueada por Tag: {col.name}");
            return true;
        }

        // 2. Bloqueo de jugadores vivos
        if (col.GetComponent<CharacterController>() != null || col.GetComponentInParent<CharacterController>() != null)
        {
            return true;
        }
    }

    return false;
}
}