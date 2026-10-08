using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PerimeterCollapse : NetworkBehaviour
{
    [Header("Zona jugable (coordenadas de grilla = posición / gridSize)")]
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private int minX = -7;
    [SerializeField] private int maxX = 7;
    [SerializeField] private int minZ = -6;
    [SerializeField] private int maxZ = 6;
    [SerializeField] private float tileHeight = -1.95f;

    [Header("Tiempos (segundos)")]
    [SerializeField] private float startDelay = 60f;
    [SerializeField] private float ringInterval = 30f;
    [SerializeField] private float warningTime = 5f;
    [SerializeField] private int maxCollapsedRings = 3;

    [Header("Daño")]
    [SerializeField] private float damageInterval = 2.5f;
    [SerializeField] private float firstDamageDelay = 0.75f;

    [Header("Visual")]
    [SerializeField] private Material warningMaterial;
    [SerializeField] private Material collapsedMaterial;
    [SerializeField] private float blinkInterval = 0.3f;

    private readonly NetworkVariable<int> collapsedRings = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> warningRing = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Servidor
    private float elapsed;
    private float checkTimer;
    private readonly Dictionary<ulong, float> nextDamageTime = new Dictionary<ulong, float>();

    // Visual (todas las instancias)
    private readonly Dictionary<int, List<GameObject>> ringTiles = new Dictionary<int, List<GameObject>>();
    private Transform tilesRoot;

    private int CappedRings
    {
        get
        {
            int possible = (Mathf.Min(maxX - minX, maxZ - minZ) + 1) / 2;
            return Mathf.Clamp(Mathf.Min(maxCollapsedRings, possible - 1), 0, possible);
        }
    }

    private int RingOf(int x, int z)
    {
        return Mathf.Min(Mathf.Min(x - minX, maxX - x), Mathf.Min(z - minZ, maxZ - z));
    }

    // ---------------- Ciclo de vida ----------------
    public override void OnNetworkSpawn()
    {
        BuildTiles();
        collapsedRings.OnValueChanged += (_, __) => RefreshVisuals();
        warningRing.OnValueChanged += (_, __) => RefreshVisuals();
        RefreshVisuals();
    }

    public override void OnNetworkDespawn()
    {
        if (tilesRoot != null) Destroy(tilesRoot.gameObject);
    }

    private void Update()
    {
        UpdateBlink();

        if (!IsServer) return;
        if (MatchManager.Instance == null || !MatchManager.Instance.IsMatchRunning) return;

        elapsed += Time.deltaTime;
        UpdateSchedule();

        checkTimer += Time.deltaTime;
        if (checkTimer >= 0.1f)
        {
            checkTimer = 0f;
            ApplyDamage();
        }
    }

    // ---------------- Servidor: cronograma y daño ----------------
    private void UpdateSchedule()
    {
        int next = collapsedRings.Value;
        if (next >= CappedRings)
        {
            warningRing.Value = -1;
            return;
        }

        float collapseAt = startDelay + next * ringInterval;
        if (elapsed >= collapseAt)
        {
            collapsedRings.Value = next + 1;
            warningRing.Value = -1;
        }
        else if (elapsed >= collapseAt - warningTime)
        {
            warningRing.Value = next;
        }
    }

    [SerializeField] private float touchRadius = 0.6f; // "tamaño" del jugador para detectar el borde

    private void ApplyDamage()
    {
        if (collapsedRings.Value <= 0) return;

        foreach (var player in FindObjectsByType<PlayerStateManager>(FindObjectsSortMode.None))
        {
            ulong id = player.NetworkObjectId;

            if (player.currentState.Value != PlayerState.Vivo || !IsInCollapsedCell(player.transform.position))
            {
                nextDamageTime.Remove(id);
                continue;
            }

            // Daño inmediato al tocar la zona, y luego cada damageInterval
            if (!nextDamageTime.TryGetValue(id, out float due) || Time.time >= due)
            {
                nextDamageTime[id] = Time.time + damageInterval;
                player.TakeDamageServerRpc();
            }
        }
    }

    private bool IsInCollapsedCell(Vector3 p)
    {
        // Revisa el centro y 4 puntos alrededor, así el borde del cuerpo también cuenta
        return InCollapsed(p.x, p.z)
            || InCollapsed(p.x + touchRadius, p.z)
            || InCollapsed(p.x - touchRadius, p.z)
            || InCollapsed(p.x, p.z + touchRadius)
            || InCollapsed(p.x, p.z - touchRadius);
    }

    private bool InCollapsed(float worldX, float worldZ)
    {
        int x = Mathf.RoundToInt(worldX / gridSize);
        int z = Mathf.RoundToInt(worldZ / gridSize);
        return RingOf(x, z) < collapsedRings.Value;
    }

    // ---------------- Visual ----------------
    private void BuildTiles()
    {
        tilesRoot = new GameObject("PerimeterTiles").transform;
        int cap = CappedRings;

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                int ring = RingOf(x, z);
                if (ring >= cap) continue;

                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(tile.GetComponent<Collider>()); // solo visual, no bloquea nada
                tile.transform.SetParent(tilesRoot);
                tile.transform.position = new Vector3(x * gridSize, tileHeight, z * gridSize);
                tile.transform.localScale = new Vector3(gridSize * 0.98f, 0.1f, gridSize * 0.98f);
                tile.SetActive(false);

                if (!ringTiles.ContainsKey(ring)) ringTiles[ring] = new List<GameObject>();
                ringTiles[ring].Add(tile);
            }
        }
    }

    private void SetRingMaterial(int ring, Material mat)
    {
        if (!ringTiles.TryGetValue(ring, out var tiles) || mat == null) return;
        foreach (var t in tiles)
            if (t != null) t.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private void SetRingActive(int ring, bool active)
    {
        if (!ringTiles.TryGetValue(ring, out var tiles)) return;
        foreach (var t in tiles)
            if (t != null) t.SetActive(active);
    }

    private void RefreshVisuals()
    {
        foreach (var kv in ringTiles)
        {
            int ring = kv.Key;
            if (ring < collapsedRings.Value)
            {
                SetRingMaterial(ring, collapsedMaterial);
                SetRingActive(ring, true);
            }
            else if (ring == warningRing.Value)
            {
                SetRingMaterial(ring, warningMaterial);
            }
            else
            {
                SetRingActive(ring, false);
            }
        }
    }

    private void UpdateBlink()
    {
        int w = warningRing.Value;
        if (w < 0) return;
        bool on = Mathf.FloorToInt(Time.time / blinkInterval) % 2 == 0;
        SetRingActive(w, on);
    }

    // Dibuja la zona y los anillos en la Scene para ajustar los valores
    private void OnDrawGizmosSelected()
    {
        int cap = CappedRings;
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                int ring = RingOf(x, z);
                Gizmos.color = ring < cap ? new Color(1f, 0.3f, 0.1f, 0.6f) : new Color(0.2f, 1f, 0.3f, 0.35f);
                Gizmos.DrawWireCube(new Vector3(x * gridSize, tileHeight, z * gridSize),
                                    new Vector3(gridSize * 0.95f, 0.1f, gridSize * 0.95f));
            }
        }
    }
}