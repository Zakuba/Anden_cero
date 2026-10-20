using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class DroneHealerAI : NetworkBehaviour
{
    private NavMeshAgent agent;

    [Header("Configuración del Dron Sanador")]
    [SerializeField] private GameObject healthPickupPrefab; // El prefab del Corazón con HealthPickup.cs
    [SerializeField] private float dropInterval = 10f;      // Tiempo entre cada corazón dejado en el suelo
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float spawnHeight = -1f;       // Altura de apoyo en el suelo
    [Tooltip("Cuánto se eleva el corazón sobre el piso real para no quedar enterrado.")]
    [SerializeField] private float heartHeightOffset = 0.5f;

    private float dropTimer;
    private Vector3 currentDirection = Vector3.forward;
    private PerimeterCollapse perimeterCollapse;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            agent.enabled = false;
            return;
        }
        
        // Buscamos la referencia al sistema de perímetro
        perimeterCollapse = Object.FindFirstObjectByType<PerimeterCollapse>();

        // Alineamos al dron a la cuadrícula al nacer
        transform.position = GetGridCenter(transform.position);
        SetNextGridDestination();
        dropTimer = dropInterval;
    }

    private void Update()
    {
        if (!IsServer) return;

        HandleMovement();
        HandleHeartDropping();
    }

    private void HandleMovement()
    {
        // Si el dron está cerca del centro de la celda destino, elige la siguiente de forma aleatoria
        if (!agent.pathPending && agent.remainingDistance < 0.05f)
        {
            SetNextGridDestination();
        }
    }

    private void SetNextGridDestination()
    {
        Vector3 currentGridPos = GetGridCenter(transform.position);
        List<Vector3> possibleDirections = new List<Vector3> { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        List<Vector3> validDestinations = new List<Vector3>();
        List<Vector3> fallbackDestinations = new List<Vector3>();

        // 1. Evaluar dirección recta
        Vector3 straightCell = currentGridPos + (currentDirection * gridSize);
        if (IsCellWalkable(straightCell))
        {
            if (!IsCellInDanger(straightCell))
            {
                validDestinations.Add(straightCell);
                validDestinations.Add(straightCell);
                validDestinations.Add(straightCell);
                validDestinations.Add(straightCell);
            }
            else
            {
                fallbackDestinations.Add(straightCell);
            }
        }

        // 2. Evaluar giros
        foreach (Vector3 dir in possibleDirections)
        {
            if (dir == currentDirection) continue;
            if (dir == -currentDirection && validDestinations.Count > 0 && fallbackDestinations.Count == 0) continue;

            Vector3 nextCell = currentGridPos + (dir * gridSize);
            if (IsCellWalkable(nextCell))
            {
                if (!IsCellInDanger(nextCell))
                {
                    validDestinations.Add(nextCell);
                }
                else
                {
                    fallbackDestinations.Add(nextCell);
                }
            }
        }

        Vector3 chosenCell;

        // 3. Elegir destino seguro o fallback hacia el centro
        if (validDestinations.Count > 0)
        {
            chosenCell = validDestinations[Random.Range(0, validDestinations.Count)];
        }
        else if (fallbackDestinations.Count > 0)
        {
            chosenCell = fallbackDestinations[0];
            float minDistanceToCenter = Vector3.Distance(chosenCell, new Vector3(0, chosenCell.y, 0));

            foreach (var cell in fallbackDestinations)
            {
                float dist = Vector3.Distance(cell, new Vector3(0, cell.y, 0));
                if (dist < minDistanceToCenter)
                {
                    minDistanceToCenter = dist;
                    chosenCell = cell;
                }
            }
        }
        else
        {
            return;
        }

        currentDirection = (chosenCell - currentGridPos).normalized;
        agent.SetDestination(chosenCell);
        transform.rotation = Quaternion.LookRotation(currentDirection);
    }

    private bool IsCellInDanger(Vector3 targetPosition)
    {
        if (perimeterCollapse == null) return false;
        return perimeterCollapse.IsPositionInDangerOrWarning(targetPosition);
    }

    private bool IsCellWalkable(Vector3 targetPosition)
    {
        NavMeshPath path = new NavMeshPath();
        agent.CalculatePath(targetPosition, path);
        return path.status == NavMeshPathStatus.PathComplete;
    }

    private void HandleHeartDropping()
    {
        dropTimer -= Time.deltaTime;
        if (dropTimer <= 0f)
        {
            DropHeart();
            dropTimer = dropInterval;
        }
    }

    private void DropHeart()
    {
        if (healthPickupPrefab == null) return;

        // El corazón se apoya sobre el piso real, a la altura configurada (no sobre un Y fijo)
        Vector3 spawnPosition = GetGridCenter(transform.position);
        if (TryGetFloorY(spawnPosition, out float floorY))
            spawnPosition.y = floorY + heartHeightOffset;
        
        // Instancia y spawnea el corazón en la red para que todos los clientes lo vean
        GameObject heartInstance = Instantiate(healthPickupPrefab, spawnPosition, Quaternion.identity);
        NetworkObject heartNetObj = heartInstance.GetComponent<NetworkObject>();
        
        if (heartNetObj != null)
        {
            heartNetObj.Spawn();
        }
    }

    // Cota Y real del piso bajo la celda. Ignora cajas, jugadores, drones, bombas y otros corazones.
    private bool TryGetFloorY(Vector3 pos, out float floorY)
    {
        floorY = pos.y;

        Vector3 rayOrigin = new Vector3(pos.x, pos.y + 2.5f, pos.z);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);

        // De mayor a menor altura
        System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));

        foreach (RaycastHit hit in hits)
        {
            Collider col = hit.collider;

            if (col.CompareTag("Destructible")) continue;
            if (col.GetComponentInParent<PlayerStateManager>() != null) continue;
            if (col.GetComponentInParent<NavMeshAgent>() != null) continue; // drones
            if (col.GetComponentInParent<NetworkBomb>() != null) continue;
            if (col.GetComponentInParent<HealthPickup>() != null) continue;

            floorY = hit.point.y;
            return true;
        }

        return false; // sin piso debajo: se conserva la altura original
    }

    private readonly CellCenterResolver cellResolver = new CellCenterResolver();

    private Vector3 GetGridCenter(Vector3 pos)
    {
        return cellResolver.Resolve(pos, gridSize, spawnHeight);
    }

    // Permite que el dron aliado también pueda ser destruido si le cae una bomba por accidente
    public void TakeDamage()
    {
        if (!IsServer) return;
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}