using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class TrainSpawner : NetworkBehaviour
{
    [Header("Tren")]
    [SerializeField] private GameObject trainPrefab;

    [Header("Puntos")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform endPoint;

    [Header("Configuración")]
    [SerializeField] private float spawnInterval = 30f;
    [SerializeField] private float trainSpeed = 10f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // REGLA CLAVE: Solo el Servidor/Host gestiona la aparición y el movimiento del tren
        if (IsServer)
        {
            StartCoroutine(TrainRoutine());
        }
    }

    private IEnumerator TrainRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            // 1. Instanciar en el servidor
            GameObject train = Instantiate(
                trainPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            // 2. Spawnear por red para que aparezca en todos los clientes
            NetworkObject netObj = train.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }

            // 3. Moverlo en el servidor (NetworkTransform replicará la posición a los clientes)
            yield return StartCoroutine(MoveTrain(train, netObj));
        }
    }

    private IEnumerator MoveTrain(GameObject train, NetworkObject netObj)
    {
        while (train != null)
        {
            train.transform.position = Vector3.MoveTowards(
                train.transform.position,
                endPoint.position,
                trainSpeed * Time.deltaTime
            );

            if (Vector3.Distance(train.transform.position, endPoint.position) < 0.1f)
            {
                // Despawn de red y destrucción autoritativa
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    Destroy(train);
                }
                yield break;
            }

            yield return null;
        }
    }
}