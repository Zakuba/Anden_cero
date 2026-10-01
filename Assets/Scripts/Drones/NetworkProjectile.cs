using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class NetworkProjectile : NetworkBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifeTime = 4f; // Autodestrucción si no choca con nada

    private void Start()
    {
        // Solo el servidor gestiona el tiempo de vida y el movimiento físico
        if (IsServer)
        {
            Invoke(nameof(DestroyProjectile), lifeTime);
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        // Movimiento constante hacia adelante
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // Ignorar colisiones con el propio dron que lo disparó u otros drones
        if (other.CompareTag("Drone")) return;

        // Si impacta a un jugador
        if (other.CompareTag("Player"))
        {
            NetworkObject playerNetObj = other.GetComponent<NetworkObject>();
            if (playerNetObj != null && playerNetObj.IsSpawned)
            {
                // Por ahora, esto destruye al jugador (lo saca de la partida)
                playerNetObj.Despawn(); 
            }
        }

        // Al chocar contra cualquier otra cosa (Pared, Caja, Jugador), se destruye la bala
        DestroyProjectile();
    }

    private void DestroyProjectile()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}