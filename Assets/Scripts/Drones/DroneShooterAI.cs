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
    [SerializeField] private float sightRange = 12f;
    [SerializeField] private LayerMask obstacleMask; // Capas que bloquean la visión (Paredes, Cajas)
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private float eyeHeight = 1f; // <-- NUEVO: Altura de los "ojos"

    private NavMeshAgent agent;
    private ShooterState currentState = ShooterState.Patrol;
    private Transform targetPlayer;
    private float fireTimer;
    private Vector3 currentDirection = Vector3.forward;

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
        
        transform.position = GetGridCenter(transform.position);
        agent.speed = patrolSpeed;
        SetNextGridDestination();
    }

    private void Update()
    {
        if (!IsServer) return;

        CheckLineOfSight();

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
        // Elevamos el punto desde donde mira el dron
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight; 
        
        Collider[] hits = Physics.OverlapSphere(eyePosition, sightRange);
        Transform potentialTarget = null;
        float closestDistance = Mathf.Infinity;
        
    foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                // Elevamos también el punto hacia donde mira (para no apuntarle a los pies)
                Vector3 targetCenter = hit.transform.position + Vector3.up * eyeHeight;
                
                float dist = Vector3.Distance(eyePosition, targetCenter);
                Vector3 dirToPlayer = (targetCenter - eyePosition).normalized;

                // Dibuja una línea amarilla en la vista "Scene" para que veas el rayo en tiempo real
                Debug.DrawRay(eyePosition, dirToPlayer * dist, Color.yellow); 

                // Lanza el rayo. Si no choca con nada de la obstacleMask, lo vio
                if (!Physics.Raycast(eyePosition, dirToPlayer, dist, obstacleMask))
                {
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        potentialTarget = hit.transform;
                    }
                }
            }
        }

        // Transiciones
        if (potentialTarget != null)
        {
            if (currentState == ShooterState.Patrol)
            {
                currentState = ShooterState.Attack;
                agent.speed = attackSpeed;
            }
            targetPlayer = potentialTarget;
        }
        else
        {
            if (currentState == ShooterState.Attack)
            {
                currentState = ShooterState.Patrol;
                agent.speed = patrolSpeed;
                targetPlayer = null;
            }
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

        // Busca todas las celdas contiguas transitables
        foreach (Vector3 dir in possibleDirections)
        {
            Vector3 nextCell = currentGridPos + (dir * gridSize);
            if (IsCellWalkable(nextCell))
            {
                validDestinations.Add(nextCell);
            }
        }

        if (validDestinations.Count > 0)
        {
            Vector3 chosenCell;

            if (currentState == ShooterState.Attack && targetPlayer != null)
            {
                // MODO ATAQUE: Elige la celda contigua que lo acerque más al jugador
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
                // MODO PATRULLA: Aleatorio, intentando no volver por donde vino si es posible
                chosenCell = validDestinations[Random.Range(0, validDestinations.Count)];
            }

            currentDirection = (chosenCell - currentGridPos).normalized;
            agent.SetDestination(chosenCell);

            // Si patrulla, mira hacia donde camina. En ataque, la rotación la maneja RotateTowardsTarget
            if (currentState == ShooterState.Patrol)
            {
                transform.rotation = Quaternion.LookRotation(currentDirection);
            }
        }
    }

    private bool IsCellWalkable(Vector3 targetPosition)
    {
        NavMeshPath path = new NavMeshPath();
        agent.CalculatePath(targetPosition, path);
        return path.status == NavMeshPathStatus.PathComplete;
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

    private Vector3 GetGridCenter(Vector3 pos)
    {
        float x = Mathf.Round(pos.x / gridSize) * gridSize;
        float z = Mathf.Round(pos.z / gridSize) * gridSize;
        return new Vector3(x, heightOffset, z);
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