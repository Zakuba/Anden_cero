using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public enum ShooterState
{
    Patrol,
    Attack
}

[RequireComponent(typeof(NavMeshAgent))]
public class DroneShooterAI : NetworkBehaviour
{
    [Header("Configuración General")]
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float heightOffset = -1f;

    [Header("Movimiento")]
    [SerializeField] private float patrolSpeed = 3.5f;
    [SerializeField] private float attackSpeed = 1.5f; // Más lento al disparar
    
    [Header("Combate y Visión")]
    [SerializeField] private bool Damage = true;
    [SerializeField] private float sightRange = 12f;
    [SerializeField] private LayerMask obstacleMask; // Capas que bloquean la visión (Paredes, Cajas)
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private float eyeHeight = 1f; // <-- NUEVO: Altura de los "ojos"

    [SerializeField] private float scanInterval = 0.15f;
    private float scanTimer;

    private NavMeshAgent agent;
    private ShooterState currentState = ShooterState.Patrol;
    private Transform targetPlayer;
    private float fireTimer;
    private PerimeterCollapse perimeterCollapse;
    private Vector3 currentDirection = Vector3.forward;
    private float speedMultiplier = 1f; // lo fija WaveManager según la dificultad

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
        
        transform.position = GetGridCenter(transform.position);
        ApplySpeed();
        SetNextGridDestination();
    }

    private void Update()
    {
        if (!IsServer) return;

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            CheckLineOfSight();
        }

        switch (currentState)
        {
            case ShooterState.Patrol:
                HandleMovement();
                break;

            case ShooterState.Attack:
                HandleMovement(); // Sigue moviéndose en grilla
                HandleShooting();
                RotateTowardsTarget();
                break;
        }
    }

    private void CheckLineOfSight()
    {
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        float sightRangeSqr = sightRange * sightRange;

        Transform potentialTarget = null;
        float closestSqr = Mathf.Infinity;

        IReadOnlyList<PlayerStateManager> players = PlayerTargetCache.Players;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerStateManager player = players[i];
            if (player == null || player.currentState.Value == PlayerState.Muerto) continue;

            Vector3 toPlayer = (player.transform.position + Vector3.up * eyeHeight) - eyePosition;
            float sqr = toPlayer.sqrMagnitude;
            if (sqr > sightRangeSqr || sqr >= closestSqr) continue;

            float dist = Mathf.Sqrt(sqr);
            if (dist < 0.001f || !Physics.Raycast(eyePosition, toPlayer / dist, dist, obstacleMask))
            {
                closestSqr = sqr;
                potentialTarget = player.transform;
            }
        }

        // Transiciones
        if (potentialTarget != null)
        {
            if (currentState == ShooterState.Patrol)
            {
                currentState = ShooterState.Attack;
                ApplySpeed();
            }
            targetPlayer = potentialTarget;
        }
        else if (currentState == ShooterState.Attack)
        {
            currentState = ShooterState.Patrol;
            ApplySpeed();
            targetPlayer = null;
        }
    }

    // --- NUEVO: Esto dibuja la esfera de visión en la pestaña Scene del Editor ---
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 eyePosition = transform.position + Vector3.up * (eyeHeight > 0 ? eyeHeight : 1f);
        Gizmos.DrawWireSphere(eyePosition, sightRange);
    }

    private void HandleMovement()
    {
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

        // Busca celdas contiguas transitables y seguras
        foreach (Vector3 dir in possibleDirections)
        {
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

        if (validDestinations.Count > 0)
        {
            if (currentState == ShooterState.Attack && targetPlayer != null)
            {
                // MODO ATAQUE Y SEGURO: Elige la celda segura que lo acerque más al jugador
                chosenCell = validDestinations[0];
                float minDistance = Vector3.Distance(chosenCell, targetPlayer.position);

                foreach (var cell in validDestinations)
                {
                    float dist = Vector3.Distance(cell, targetPlayer.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        chosenCell = cell;
                    }
                }
            }
            else
            {
                // MODO PATRULLA Y SEGURO: Aleatorio entre las seguras
                chosenCell = validDestinations[Random.Range(0, validDestinations.Count)];
            }
        }
        else if (fallbackDestinations.Count > 0)
        {
            // Si todas las celdas están en peligro, usa fallback hacia el centro del mapa (Vector3.zero)
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

        if (currentState == ShooterState.Patrol)
        {
            transform.rotation = Quaternion.LookRotation(currentDirection);
        }
    }

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

    private void HandleShooting()
    {
        if (targetPlayer == null) return;

        // Calculamos la dirección exacta hacia el jugador en el plano horizontal
        Vector3 dirToPlayer = (targetPlayer.position - transform.position).normalized;
        dirToPlayer.y = 0;

        // Calculamos cuántos grados de diferencia hay entre la mira del dron y el jugador
        float angle = Vector3.Angle(transform.forward, dirToPlayer);

        // Si la diferencia es mayor a 5 grados, el dron cancela el disparo y sigue girando
        if (angle > 10f)
        {
            return; 
        }

        // Solo si lo tiene en la mira, avanza el temporizador y dispara
        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            Shoot();
            fireTimer = fireRate;
        }
    }

    private void Shoot()
    {
        // Instanciar la bala y dirigirla hacia donde está mirando el dron
        GameObject proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        NetworkObject projNetObj = proj.GetComponent<NetworkObject>();
        projNetObj.Spawn(); // Sincroniza la bala en la red
    }

    private void RotateTowardsTarget()
    {
        if (targetPlayer == null) return;

        // Rota suavemente hacia el jugador solo en el eje Y
        Vector3 dir = (targetPlayer.position - transform.position).normalized;
        dir.y = 0; 
        if (dir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }

    private readonly CellCenterResolver cellResolver = new CellCenterResolver();

    private Vector3 GetGridCenter(Vector3 pos)
    {
        return cellResolver.Resolve(pos, gridSize, heightOffset);
    }

    // Lo llama WaveManager al spawnear el dron (solo servidor). Los drones colocados a mano
    // (Battle Royale) nunca lo reciben y conservan los valores del Inspector.
    public void ApplyDifficulty(DroneDifficultyParams p)
    {
        if (!IsServer) return;

        speedMultiplier = Mathf.Max(0.1f, p.SpeedMultiplier);
        fireRate = Mathf.Max(0.1f, p.ShooterFireRate);
        sightRange = Mathf.Max(1f, p.ShooterSightRange);
        scanInterval = Mathf.Max(0.02f, p.ShooterScanInterval);

        ApplySpeed();
    }

    private void ApplySpeed()
    {
        if (agent == null || !agent.enabled) return;
        float baseSpeed = currentState == ShooterState.Attack ? attackSpeed : patrolSpeed;
        agent.speed = baseSpeed * speedMultiplier;
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