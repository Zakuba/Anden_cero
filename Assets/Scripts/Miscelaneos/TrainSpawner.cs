using UnityEngine;
using System.Collections;

public class TrainSpawner : MonoBehaviour
{
    [Header("Tren")]
    [SerializeField] private GameObject trainPrefab;

    [Header("Puntos")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform endPoint;

    [Header("Configuración")]
    [SerializeField] private float spawnInterval = 30f;
    [SerializeField] private float trainSpeed = 10f;

    private void Start()
    {
        StartCoroutine(TrainRoutine());
    }

    private IEnumerator TrainRoutine()
    {
        while (true)
        {
            // Esperar antes de generar el tren
            yield return new WaitForSeconds(spawnInterval);

            // Crear el tren
            GameObject train = Instantiate(
                trainPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            // Moverlo hasta el punto final
            yield return StartCoroutine(MoveTrain(train));
        }
    }

    private IEnumerator MoveTrain(GameObject train)
    {
        while (train != null)
        {
            train.transform.position = Vector3.MoveTowards(
                train.transform.position,
                endPoint.position,
                trainSpeed * Time.deltaTime
            );

            // Llegó al destino
            if (Vector3.Distance(train.transform.position, endPoint.position) < 0.1f)
            {
                Destroy(train);
                yield break;
            }

            yield return null;
        }
    }
}