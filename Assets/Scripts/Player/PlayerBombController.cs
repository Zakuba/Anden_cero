
using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class PlayerBombController : NetworkBehaviour
{
    [Header("Configuración de Bomba")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float bombTimer = 3f;
    [SerializeField] private int explosionRange = 2;
    [SerializeField] private LayerMask explosionLayerMask;
    [SerializeField] private KeyCode plantKey = KeyCode.Space;
    [Tooltip("Radio o elevación para que la bomba descanse sobre el piso sin enterrarse.")]
    [SerializeField] private float bombRadiusOffset = 0.5f;

    [Header("Power-ups (Drops)")]
    [SerializeField] private GameObject[] powerUpPrefabs;
    [SerializeField] private float powerUpDropChance = 0.5f;
    [SerializeField] private float powerUpSpawnHeight = -1f;
    [SerializeField] private float powerUpHeightAboveGround = 1.8f;
    [SerializeField] private float dropChance = 0.75f;

    [Header("Efectos Visuales (VFX)")]
    [SerializeField] private GameObject explosionVfx;
    [SerializeField] private GameObject fireVfx;

    [Header("Destrucción Ragdoll")]
    [SerializeField] private GameObject destructionRagdoll;
    [SerializeField] private float ragdollLifetime = 4f;
    [SerializeField] private float ragdollForce = 3f;
    [SerializeField] private float ragdollUpForce = 2f;

    [Header("Desvanecimiento Ragdoll")]
    [SerializeField] private float ragdollFadeDuration = 1.5f;

    [Header("Animación de destrucción")]
    [SerializeField] private float destructionDuration = 0.5f;

    private int activeBombs = 0;
    private int maxBombs = 1;

    public int BombsPlacedThisMatch { get; private set; }

    private PlayerStats playerStats;
    private PlayerStateManager stateManager;
    private BombCooldownUI localBombUI;
    private EasterEggManager swap;

    // Control local para evitar generar varios ragdolls
    // por el mismo objeto destructible.
    private HashSet<EntityId> destroyedObjects = new HashSet<EntityId>();

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        stateManager = GetComponent<PlayerStateManager>();
        swap = GetComponent<EasterEggManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            localBombUI = FindObjectOfType<BombCooldownUI>(true);
        }
    }

    private int EffectiveMaxBombs =>
        maxBombs + (playerStats != null ? playerStats.MaxBombsBonus : 0);

    private int EffectiveExplosionRange =>
        explosionRange + (playerStats != null ? playerStats.ExplosionRangeBonus : 0);

    private void Update()
    {
        if (!IsOwner) return;

        if (stateManager != null &&
            stateManager.currentState.Value != PlayerState.Vivo)
            return;

        if (MatchManager.Instance != null &&
            !MatchManager.Instance.IsMatchRunning)
            return;

        if (localBombUI == null)
        {
            localBombUI = FindObjectOfType<BombCooldownUI>(true);

            if (localBombUI == null)
                return;
        }

        if (activeBombs >= EffectiveMaxBombs &&
            !localBombUI.IsCooldownActive)
        {
            localBombUI.StartCooldown(bombTimer);
        }
        else if (activeBombs < EffectiveMaxBombs &&
                 localBombUI.IsCooldownActive)
        {
            localBombUI.ResetUI();
        }

        bool canPlant = swap == null || !swap.IsSwapped;

        if (canPlant &&
            Input.GetKeyDown(plantKey) &&
            activeBombs < EffectiveMaxBombs)
        {
            Vector3 spawnPosition = GetGridCenter(transform.position);
            RequestPlantBombServerRpc(spawnPosition);
        }
    }

    private Vector3 GetGridCenter(Vector3 playerPos)
    {
        Vector3 rayOrigin = new Vector3(
            playerPos.x,
            playerPos.y + 0.5f,
            playerPos.z
        );

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            4f,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            string objName = hit.collider.gameObject.name.ToLower();
            string rootName = hit.collider.transform.root.gameObject.name.ToLower();

            if (objName.Contains("casil") ||
                objName.Contains("piso") ||
                objName.Contains("anden") ||
                objName.Contains("floor") ||
                rootName.Contains("casil") ||
                rootName.Contains("piso") ||
                rootName.Contains("anden"))
            {
                Vector3 tileCenter = hit.collider.bounds.center;
                float bombY = hit.point.y + bombRadiusOffset;

                return new Vector3(
                    tileCenter.x,
                    bombY,
                    tileCenter.z
                );
            }
        }

        float x = Mathf.Round(playerPos.x / gridSize) * gridSize;
        float z = Mathf.Round(playerPos.z / gridSize) * gridSize;
        float floorY = (playerPos.y - 1.0f) + bombRadiusOffset;

        return new Vector3(x, floorY, z);
    }

    [ServerRpc]
    private void RequestPlantBombServerRpc(Vector3 spawnPosition)
    {
        if (swap != null && swap.IsSwapped) return;

        if (MatchManager.Instance != null &&
            !MatchManager.Instance.IsMatchRunning)
            return;

        if (activeBombs >= EffectiveMaxBombs) return;

        activeBombs++;
        BombsPlacedThisMatch++;

        UpdateBombCountClientRpc(activeBombs);

        GameObject bombInstance = Instantiate(
            bombPrefab,
            spawnPosition,
            Quaternion.identity
        );

        NetworkObject bombNetObj =
            bombInstance.GetComponent<NetworkObject>();

        bombNetObj.Spawn();

        BombInteractable bombLogic =
            bombInstance.GetComponent<BombInteractable>();

        if (bombLogic != null)
        {
            bombLogic.SetInitialOwner(OwnerClientId);
        }

        StartCoroutine(BombExplosionRoutine(bombNetObj));
    }

    private IEnumerator BombExplosionRoutine(NetworkObject bombNetObj)
    {
        BombInteractable bombLogic =
            bombNetObj.GetComponent<BombInteractable>();

        float currentTimer = 0f;

        try
        {
            while (currentTimer < bombTimer)
            {
                if (bombNetObj == null || !bombNetObj.IsSpawned)
                {
                    Debug.LogWarning(
                        "[Bomba] La bomba fue destruida o despawneada antes de completar el temporizador."
                    );

                    yield break;
                }

                if (bombLogic == null || !bombLogic.isMoving.Value)
                {
                    currentTimer += Time.deltaTime;
                }

                yield return null;
            }

            if (bombNetObj != null && bombNetObj.IsSpawned)
            {
                Vector3 finalPos = bombNetObj.transform.position;

                ulong attackerId = bombLogic != null
                    ? bombLogic.currentOwnerClientId.Value
                    : OwnerClientId;

                List<Vector3> affectedCells =
                    CalculateExplosionCells(finalPos, attackerId);

                SpawnVfxClientRpc(affectedCells.ToArray());

                yield return new WaitForSeconds(0.05f);

                if (bombNetObj != null && bombNetObj.IsSpawned)
                {
                    bombNetObj.Despawn();
                }
            }
        }
        finally
        {
            activeBombs--;

            if (activeBombs < 0)
                activeBombs = 0;

            UpdateBombCountClientRpc(activeBombs);
        }
    }

    private List<Vector3> CalculateExplosionCells(
        Vector3 center,
        ulong attackerId)
    {
        List<Vector3> cells = new List<Vector3>();
        HashSet<ulong> playersHitThisExplosion = new HashSet<ulong>();

        Vector3 groundCenter = GetGroundPosition(center);

        cells.Add(groundCenter);

        CheckCell(
            groundCenter,
            playersHitThisExplosion,
            attackerId,
            out _,
            out _
        );

        Vector3[] directions =
        {
            Vector3.forward,
            Vector3.back,
            Vector3.right,
            Vector3.left
        };

        foreach (Vector3 dir in directions)
        {
            for (int i = 1; i <= EffectiveExplosionRange; i++)
            {
                Vector3 horizontalCell =
                    center + (dir * gridSize * i);

                Vector3 targetCell = GetGroundPosition(horizontalCell);

                CheckCell(
                    targetCell,
                    playersHitThisExplosion,
                    attackerId,
                    out bool hitIndestructible,
                    out bool hitDestructible
                );

                if (hitIndestructible)
                    break;

                cells.Add(targetCell);

                if (hitDestructible)
                    break;
            }
        }

        return cells;
    }

    private Vector3 GetGroundPosition(Vector3 pos)
    {
        Vector3 rayOrigin = new Vector3(
            pos.x,
            pos.y + 0.1f,
            pos.z
        );

        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            10f,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        System.Array.Sort(
            hits,
            (a, b) => b.point.y.CompareTo(a.point.y)
        );

        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("Destructible"))
                continue;

            if (hit.collider.GetComponentInParent<BombInteractable>() != null)
                continue;

            if (hit.collider.GetComponentInParent<PlayerStateManager>() != null ||
                hit.collider.GetComponent<CharacterController>() != null ||
                hit.collider.CompareTag("Player"))
                continue;

            return new Vector3(
                pos.x,
                hit.point.y,
                pos.z
            );
        }

        return pos;
    }


    // Centra la posición en la celda de la cuadrícula
private Vector3 SnapToGridCenter(Vector3 pos)
{
    float x = Mathf.Round(pos.x / gridSize) * gridSize;
    float z = Mathf.Round(pos.z / gridSize) * gridSize;
    return new Vector3(x, pos.y, z);
}

    private void CheckCell(
        Vector3 cell,
        HashSet<ulong> playersHitThisExplosion,
        ulong attackerId,
        out bool hitIndestructible,
        out bool hitDestructible)
    {
        hitIndestructible = false;
        hitDestructible = false;

        HashSet<GameObject> dronesHitThisCell =
            new HashSet<GameObject>();

        Vector3 boxCenter = new Vector3(
            cell.x,
            cell.y + 1.0f,
            cell.z
        );

        Vector3 halfExtents = new Vector3(
            gridSize * 0.45f,
            1.0f,
            gridSize * 0.45f
        );

        Collider[] hits = Physics.OverlapBox(
            boxCenter,
            halfExtents,
            Quaternion.identity,
            explosionLayerMask,
            QueryTriggerInteraction.Collide
        );

        foreach (Collider hit in hits)
        {
            if (hit.GetComponentInParent<BombInteractable>() != null)
                continue;

            // Daño a jugadores.
            PlayerStateManager player =
                hit.GetComponent<PlayerStateManager>();

            if (player == null)
                player = hit.GetComponentInParent<PlayerStateManager>();

            if (player != null)
            {
                ulong playerId = player.NetworkObjectId;

                if (!playersHitThisExplosion.Contains(playerId))
                {
                    playersHitThisExplosion.Add(playerId);
                    player.TakeDamageServerRpc(attackerId);
                }
            }

            // Obstáculos indestructibles.
            if (!hitIndestructible &&
                hit.CompareTag("Indestructible"))
            {
                hitIndestructible = true;
            }

            // Obstáculos destructibles.
            if (hit.CompareTag("Destructible"))
            {
                DestroyObjectAtPositionClientRpc(
                    hit.transform.position
                );

                if (!hitDestructible)
                {
                    hitDestructible = true;
                    TrySpawnPowerUpDrop(hit.transform.position);
                }
            }

            // Drones.
            if (hit.CompareTag("Drone"))
            {
                GameObject droneRoot = hit.transform.root.gameObject;

                if (dronesHitThisCell.Add(droneRoot))
                {
                    DroneBomberAI bombardero =
                        hit.GetComponentInParent<DroneBomberAI>();

                    if (bombardero != null)
                        bombardero.TakeDamage();

                    DroneShooterAI fusilero =
                        hit.GetComponentInParent<DroneShooterAI>();

                    if (fusilero != null)
                        fusilero.TakeDamage();

                    DroneHealerAI sanador =
                        hit.GetComponentInParent<DroneHealerAI>();

                    if (sanador != null)
                        sanador.TakeDamage();
                }

                hitDestructible = true;
            }
        }
    }

    [ClientRpc]
    private void SpawnVfxClientRpc(Vector3[] firePositions)
    {
        if (firePositions == null || firePositions.Length == 0)
            return;

        float groundOffset = 0.02f;

        if (explosionVfx != null)
        {
            Vector3 centerPos = new Vector3(
                firePositions[0].x,
                firePositions[0].y + groundOffset,
                firePositions[0].z
            );

            Instantiate(
                explosionVfx,
                centerPos,
                Quaternion.identity
            );
        }

        for (int i = 1; i < firePositions.Length; i++)
        {
            if (fireVfx != null)
            {
                Vector3 firePos = new Vector3(
                    firePositions[i].x,
                    firePositions[i].y + groundOffset,
                    firePositions[i].z
                );

                Instantiate(
                    fireVfx,
                    firePos,
                    Quaternion.identity
                );
            }
        }
    }

private void TrySpawnPowerUpDrop(Vector3 boxPosition)
{
    if (Random.value > powerUpDropChance)
        return;

    if (powerUpPrefabs == null || powerUpPrefabs.Length == 0)
        return;

    int index = Random.Range(0, powerUpPrefabs.Length);

    GameObject prefab = powerUpPrefabs[index];

    if (prefab == null)
        return;

    Vector3 cell = SnapToGridCenter(boxPosition);

    Vector3 ground = GetGroundPosition(
        new Vector3(cell.x, boxPosition.y, cell.z)
    );

    Vector3 spawnPos = new Vector3(
        cell.x,
        ground.y + powerUpHeightAboveGround,
        cell.z
    );

    GameObject dropInstance = Instantiate(
        prefab,
        spawnPos,
        Quaternion.identity
    );

    dropInstance.GetComponent<NetworkObject>().Spawn();
}

    /// <summary>
    /// Destruye localmente el objeto y genera su ragdoll visual.
    /// Cada cliente ejecuta esta operación en su propia escena.
    /// </summary>
    [ClientRpc]
    private void DestroyObjectAtPositionClientRpc(Vector3 pos)
    {
        Collider[] colliders = Physics.OverlapSphere(
            pos,
            0.5f,
            explosionLayerMask
        );

        foreach (Collider col in colliders)
        {
            if (!col.CompareTag("Destructible"))
                continue;

            GameObject destructible = col.gameObject;

            EntityId objectId = destructible.GetEntityId();

            if (!destroyedObjects.Add(objectId))
                continue;

            Vector3 destructionPosition = destructible.transform.position;
            Quaternion destructionRotation = destructible.transform.rotation;

            if (destructionRagdoll != null)
            {
                GameObject ragdoll = Instantiate(
                    destructionRagdoll,
                    destructionPosition,
                    destructionRotation
                );

                Rigidbody[] rigidbodies =
                    ragdoll.GetComponentsInChildren<Rigidbody>();

                foreach (Rigidbody rb in rigidbodies)
                {
                    Vector3 direction =
                        (rb.worldCenterOfMass - pos).normalized;

                    Vector3 force =
                        direction * ragdollForce +
                        Vector3.up * ragdollUpForce;

                    rb.AddForce(force, ForceMode.Impulse);
                }

                // Espera y luego desvanece las piedras.
                StartCoroutine(FadeAndDestroyRagdoll(ragdoll));
            }

            StartCoroutine(ShrinkAndDestroy(destructible));
        }
    }

    private IEnumerator FadeAndDestroyRagdoll(GameObject ragdoll)
    {
        if (ragdoll == null)
            yield break;

        // Tiempo durante el cual las piedras permanecen visibles.
        yield return new WaitForSeconds(ragdollLifetime);

        if (ragdoll == null)
            yield break;

        Renderer[] renderers =
            ragdoll.GetComponentsInChildren<Renderer>();

        List<Material> materials = new List<Material>();
        List<Color> originalColors = new List<Color>();

        foreach (Renderer renderer in renderers)
        {
            // Accedemos a las instancias de materiales del ragdoll.
            Material[] rendererMaterials = renderer.materials;

            foreach (Material material in rendererMaterials)
            {
                materials.Add(material);

                Color originalColor = Color.white;

                if (material.HasProperty("_BaseColor"))
                    originalColor = material.GetColor("_BaseColor");
                else if (material.HasProperty("_Color"))
                    originalColor = material.GetColor("_Color");

                originalColors.Add(originalColor);

                ConfigureMaterialForFade(material);
            }
        }

        // Si no hay materiales, simplemente destruimos el ragdoll.
        if (materials.Count == 0)
        {
            Destroy(ragdoll);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < ragdollFadeDuration)
        {
            if (ragdoll == null)
                yield break;

            elapsed += Time.deltaTime;

            float progress = ragdollFadeDuration > 0f
                ? Mathf.Clamp01(elapsed / ragdollFadeDuration)
                : 1f;

            float alpha = 1f - progress;

            for (int i = 0; i < materials.Count; i++)
            {
                Material material = materials[i];

                if (material == null)
                    continue;

                Color color = originalColors[i];
                color.a *= alpha;

                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                else if (material.HasProperty("_Color"))
                    material.SetColor("_Color", color);
            }

            yield return null;
        }

        Destroy(ragdoll);
    }

    private void ConfigureMaterialForFade(Material material)
    {
        if (material == null)
            return;

        // URP Lit.
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);

            material.SetInt(
                "_SrcBlend",
                (int)UnityEngine.Rendering.BlendMode.SrcAlpha
            );

            material.SetInt(
                "_DstBlend",
                (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
            );

            material.SetInt("_ZWrite", 0);

            material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            material.renderQueue = 3000;
        }

        // Standard Shader.
        else if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);

            material.SetInt(
                "_SrcBlend",
                (int)UnityEngine.Rendering.BlendMode.SrcAlpha
            );

            material.SetInt(
                "_DstBlend",
                (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
            );

            material.SetInt("_ZWrite", 0);

            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_ALPHABLEND_ON");

            material.renderQueue = 3000;
        }

        material.SetOverrideTag("RenderType", "Transparent");
    }

    private IEnumerator ShrinkAndDestroy(GameObject target)
    {
        if (target == null)
            yield break;

        Vector3 initialScale = target.transform.localScale;
        float elapsed = 0f;

        // Evita que el objeto siga interfiriendo con la explosión.
        Collider[] colliders = target.GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        while (elapsed < destructionDuration)
        {
            if (target == null)
                yield break;

            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / destructionDuration);

            // Ease In: empieza lento y acelera al desaparecer.
            float easedProgress = progress * progress;

            target.transform.localScale = Vector3.Lerp(
                initialScale,
                Vector3.zero,
                easedProgress
            );

            yield return null;
        }

        if (target != null)
            Destroy(target);
    }

    [ClientRpc]
    private void UpdateBombCountClientRpc(int count)
    {
        activeBombs = count;
    }
}
