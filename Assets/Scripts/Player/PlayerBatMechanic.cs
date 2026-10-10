/*
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
         if (MatchManager.Instance != null && !MatchManager.Instance.IsMatchRunning) return;

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

            // Avisa únicamente al dueño del avatar que el bateo fue exitoso
            NotifySuccessfulBatClientRpc();
        }

    }

    [ClientRpc]
    private void NotifySuccessfulBatClientRpc()
    {
        // Solo el jugador dueño de este avatar incrementa su persistencia local
        if (IsOwner && AchievementManager.Instance != null)
        {
            AchievementManager.Instance.AddBatStat();
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

*/
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
        if (MatchManager.Instance != null && !MatchManager.Instance.IsMatchRunning)
        {
            Debug.LogWarning("[Bat-Server] MatchManager no está corriendo.");
            return;
        }

        if (!bombRef.TryGet(out NetworkObject bombNetObj))
        {
            Debug.LogWarning("[Bat-Server] No se pudo resolver la referencia de red de la bomba.");
            return;
        }

        BombInteractable bomb = bombNetObj.GetComponent<BombInteractable>();
        if (bomb == null || bomb.isMoving.Value) return;

        Vector3 diff = bomb.transform.position - transform.position;
        diff.y = 0;
        if (diff.magnitude > gridSize * 1.8f || diff.magnitude < 0.25f)
        {
            Debug.LogWarning($"[Bat-Server] Distancia de seguridad inválida: {diff.magnitude:F2}m (Límite: {gridSize * 1.8f}m)");
            return;
        }

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

            if (!IsCellBlocked(nextCell))
            {
                finalDestination = nextCell;
            }
            else
            {
                Debug.LogWarning($"[Bat-Server] Casilla {nextCell} bloqueada por obstáculo.");
                break;
            }
        }

        if (finalDestination != currentBombPos)
        {
            Debug.Log($"[Bat-Server] Éxito: Deslizando bomba hacia {finalDestination}");
            bomb.SlideTo(finalDestination);
            NotifySuccessfulBatClientRpc();
        }
        else
        {
            Debug.LogWarning("[Bat-Server] La bomba no se movió: Casilla destino bloqueada.");
        }
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
        Vector3 boxCenter = new Vector3(cellCenter.x, cellCenter.y + 0.7f, cellCenter.z);
        Vector3 halfExtents = new Vector3(gridSize * 0.42f, 0.45f, gridSize * 0.42f);

        Collider[] colliders = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider col in colliders)
        {
            // 1. Ignorar la propia bomba
            if (col.GetComponentInParent<BombInteractable>() != null) continue;

            // 2. Ignorar triggers decorativos
            if (col.isTrigger) continue;

            // 3. Ignorar suelo, andenes, casillas y volúmenes de límites de cámara
            string colName = col.gameObject.name.ToLower();
            string rootName = col.transform.root.gameObject.name.ToLower();
            
            if (colName.Contains("casilla") || colName.Contains("piso") || colName.Contains("floor") 
                || colName.Contains("suelo") || colName.Contains("anden") || colName.Contains("via")
                || colName.Contains("camara") || colName.Contains("camera") || colName.Contains("limite")
                || rootName.Contains("casilla") || rootName.Contains("grid") || rootName.Contains("piso")
                || rootName.Contains("camara") || rootName.Contains("camera"))
            {
                continue;
            }

            // 4. Bloqueo explícito por Tags de juego
            if (col.CompareTag(indestructibleTag) || col.CompareTag(destructibleTag) || col.CompareTag(playerTag))
            {
                Debug.LogWarning($"[Bat-Block] Bloqueado por Tag: {col.name} ({col.tag})");
                return true;
            }

            // 5. Bloqueo de avatares de jugadores vivos
            if (col.GetComponent<CharacterController>() != null || col.GetComponentInParent<CharacterController>() != null)
            {
                Debug.LogWarning($"[Bat-Block] Bloqueado por Jugador: {col.name}");
                return true;
            }

            // 6. Muros y obstáculos sólidos Untagged
            if (col.CompareTag("Untagged") && !col.isTrigger)
            {
                if (col.bounds.max.y <= cellCenter.y - 0.2f)
                {
                    continue; // Es una plataforma/piso debajo de la bomba, ignorar
                }

                Debug.LogWarning($"[Bat-Block] Bloqueado por Muro Untagged: {col.name}");
                return true;
            }
        }

        return false;
    }
}