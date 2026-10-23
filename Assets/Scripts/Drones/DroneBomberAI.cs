using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class DroneBomberAI : NetworkBehaviour
{
    private NavMeshAgent agent;
    private float baseSpeed; // velocidad del prefab, antes del escalado por oleada

    [Header("Configuración de Bombas")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private float dropInterval = 4f;
    [SerializeField] private float gridSize = 2.5f;
    [SerializeField] private float bombSpawnHeight = -1f;
    [Tooltip("Por regla de diseño las bombas de los drones no rompen cajas.")]
    [SerializeField] private bool bombsDestroyCrates = false;
    
    private float bombTimer;
    private PerimeterCollapse perimeterCollapse;
    private Vector3 currentDirection = Vector3.forward;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        baseSpeed = agent.speed;
        
        // Evitamos rotaciones suaves para un movimiento más robótico/lineal (opcional)
        // agent.updateRotation = false; 
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            agent.enabled = false;
            return;
        }
        // Buscamos la referencia al sistema de perímetro de la escena
        perimeterCollapse = Object.FindFirstObjectByType<PerimeterCollapse>();

        // Alineamos al dron a la cuadrícula al nacer
        transform.position = GetGridCenter(transform.position);
        SetNextGridDestination();
        bombTimer = dropInterval;
    }

    private void Update()
    {
        if (!IsServer) return;

        HandleMovement();
        HandleBombDropping();
    }

    private void HandleMovement()
    {
        // Si el dron está muy cerca del centro de la celda destino, elige la siguiente
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
        List<Vector3> fallbackDestinations = new List<Vector3>(); // Por si todas las opciones son peligroso

        // 1. Evaluar dirección recta (con preferencia si es segura)
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

        // 2. Evaluar giros (lados)
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

        // 3. Elegir destino seguro, o usar el fallback si no hay más remedio
        if (validDestinations.Count > 0)
        {
            chosenCell = validDestinations[Random.Range(0, validDestinations.Count)];
        }
        else if (fallbackDestinations.Count > 0)
        {
            // Si todo alrededor es peligro, elegimos la celda del fallback que esté más cerca del centro (Vector3.zero)
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
            return; // Si no hay absolutamente ninguna celda a donde moverse, se queda quieto
        }

        currentDirection = (chosenCell - currentGridPos).normalized;
        agent.SetDestination(chosenCell);
        transform.rotation = Quaternion.LookRotation(currentDirection);
    }

    // Método auxiliar para consultar al PerimeterCollapse
    private bool IsCellInDanger(Vector3 targetPosition)
    {
        if (perimeterCollapse == null) return false;
        return perimeterCollapse.IsPositionInDangerOrWarning(targetPosition);
    }

    private static NavMeshPath sharedPath;

    private bool IsCellWalkable(Vector3 targetPosition)
    {
        if (sharedPath == null) sharedPath = new NavMeshPath();
        agent.CalculatePath(targetPosition, sharedPath);
        return sharedPath.status == NavMeshPathStatus.PathComplete;
    }

    private void HandleBombDropping()
    {
        bombTimer -= Time.deltaTime;
        if (bombTimer <= 0f)
        {
            DropBomb();
            bombTimer = dropInterval;
        }
    }

    private void DropBomb()
    {
        bool matchRunning = MatchManager.Instance == null || MatchManager.Instance.IsMatchRunning;
        if (!matchRunning) return;

        Vector3 spawnPosition = GetGridCenter(transform.position);
        GameObject bombInstance = Instantiate(bombPrefab, spawnPosition, Quaternion.identity);
        NetworkObject bombNetObj = bombInstance.GetComponent<NetworkObject>();

        // Se configura antes del Spawn; la explosión se calcula en el servidor.
        if (bombInstance.TryGetComponent(out NetworkBomb bomb))
            bomb.destroysCrates = bombsDestroyCrates;

        bombNetObj.Spawn();
    }

    // Lo llama WaveManager al spawnear el dron (solo servidor). 1 = velocidad base.
    public void SetSpeedMultiplier(float multiplier)
    {
        if (!IsServer || agent == null || !agent.enabled) return;
        agent.speed = baseSpeed * Mathf.Max(0.1f, multiplier);
    }

    public void TakeDamage()
    {
        if (!IsServer) return;
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
    private readonly CellCenterResolver cellResolver = new CellCenterResolver();

    private Vector3 GetGridCenter(Vector3 pos)
    {
        return cellResolver.Resolve(pos, gridSize, bombSpawnHeight);
    }
}