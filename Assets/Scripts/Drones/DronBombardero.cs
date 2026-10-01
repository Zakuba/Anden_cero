using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class DroneBomberAI : NetworkBehaviour
{
    private NavMeshAgent agent;

    [Header("Configuración de Bombas")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private float dropInterval = 4f;
    [SerializeField] private float gridSize = 2.5f;
    [SerializeField] private float bombSpawnHeight = -1f;
    
    private float bombTimer;
    private Vector3 currentDirection = Vector3.forward;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        
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

        // 1. Prioridad a seguir en línea recta
        Vector3 straightCell = currentGridPos + (currentDirection * gridSize);
        if (IsCellWalkable(straightCell))
        {
            // Agregamos múltiples veces la opción recta para aumentar su probabilidad (80% prob)
            validDestinations.Add(straightCell);
            validDestinations.Add(straightCell);
            validDestinations.Add(straightCell);
            validDestinations.Add(straightCell);
        }

        // 2. Evaluar giros (lados)
        foreach (Vector3 dir in possibleDirections)
        {
            if (dir == currentDirection) continue; // Ya evaluado
            if (dir == -currentDirection && validDestinations.Count > 0) continue; // No volver por donde vino salvo que sea un callejón sin salida

            Vector3 nextCell = currentGridPos + (dir * gridSize);
            if (IsCellWalkable(nextCell))
            {
                validDestinations.Add(nextCell);
            }
        }

        // 3. Elegir destino
        if (validDestinations.Count > 0)
        {
            Vector3 chosenCell = validDestinations[Random.Range(0, validDestinations.Count)];
            currentDirection = (chosenCell - currentGridPos).normalized;
            agent.SetDestination(chosenCell);
            
            // Opcional: Forzar la rotación para que mire ortogonalmente
            transform.rotation = Quaternion.LookRotation(currentDirection);
        }
    }

    // Utiliza el NavMesh para confirmar que no hay paredes entre la celda actual y la adyacente
    private bool IsCellWalkable(Vector3 targetPosition)
    {
        NavMeshPath path = new NavMeshPath();
        agent.CalculatePath(targetPosition, path);
        return path.status == NavMeshPathStatus.PathComplete;
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
        Vector3 spawnPosition = GetGridCenter(transform.position);
        GameObject bombInstance = Instantiate(bombPrefab, spawnPosition, Quaternion.identity);
        NetworkObject bombNetObj = bombInstance.GetComponent<NetworkObject>();
        bombNetObj.Spawn();
    }

    private Vector3 GetGridCenter(Vector3 pos)
    {
        float x = Mathf.Round(pos.x / gridSize) * gridSize;
        float z = Mathf.Round(pos.z / gridSize) * gridSize;
        return new Vector3(x, bombSpawnHeight, z);
    }

    public void TakeDamage()
    {
        if (!IsServer) return;
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}