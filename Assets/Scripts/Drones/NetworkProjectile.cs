using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class NetworkProjectile : NetworkBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifeTime = 4f;

    // Reemplazamos Start() por OnNetworkSpawn() que es seguro en multijugador
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Invoke(nameof(DestroyProjectile), lifeTime);
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // 1. Ignorar colisiones con drones
        if (other.CompareTag("Drone")) return;
        
        // 2. Ignorar si choca contra otro trigger (por ejemplo, zonas de luz u otros radares)
        if (other.isTrigger) return; 

        // 3. Impacto a jugador
        if (other.CompareTag("Player"))
        {
            NetworkObject playerNetObj = other.GetComponent<NetworkObject>();
            if (playerNetObj != null && playerNetObj.IsSpawned)
            {
                //playerNetObj.Despawn(); 
            }
        }

        // 4. Se destruye al chocar contra geometría sólida
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