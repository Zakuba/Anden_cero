using UnityEngine;
using System.Collections;

public class TrainSpawnerConCam : MonoBehaviour
{
    [Header("Tren")]
    [SerializeField] private GameObject trainPrefab;

    [Header("Puntos")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform endPoint;

    [Header("Configuración del tren")]
    [SerializeField] private float spawnInterval = 30f;
    [SerializeField] private float trainSpeed = 10f;

    [Header("Vibración de cámara")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float shakeDuration = 3f;
    [SerializeField] private float shakeAmount = 0.05f;

    [Header("Parpadeo de luz")]
    [SerializeField] private Light trainLight;
    [SerializeField] private float minLightIntensity = 0f;
    [SerializeField] private float maxLightIntensity = 1f;
    [SerializeField] private float lightFlickerSpeed = 15f;

    private float originalLightIntensity;

    private void Start()
    {
        if (trainLight != null)
        {
            originalLightIntensity = trainLight.intensity;
        }

        StartCoroutine(TrainRoutine());
    }

    private IEnumerator TrainRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            GameObject train = Instantiate(
                trainPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            // Iniciamos el shake independientemente del movimiento del tren
            if (cameraTransform != null || trainLight != null)
            {
                StartCoroutine(ShakeCameraAndFlickerLight());
            }

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

            if (Vector3.Distance(
                train.transform.position,
                endPoint.position
            ) < 0.1f)
            {
                Destroy(train);
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator ShakeCameraAndFlickerLight()
    {
        float elapsed = 0f;

        // Guardamos la posición LOCAL de la cámara.
        // Como la cámara está dentro del CameraRig,
        // normalmente será (0,0,0).
        Vector3 posicionBase = Vector3.zero;
        Quaternion rotacionBase = Quaternion.identity;

        if (cameraTransform != null)
        {
            posicionBase = cameraTransform.localPosition;
            rotacionBase = cameraTransform.localRotation;
        }

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            if (cameraTransform != null)
            {
                float x = Random.Range(-1f, 1f) * shakeAmount;
                float y = Random.Range(-1f, 1f) * shakeAmount;

                cameraTransform.localPosition =
                    posicionBase + new Vector3(x, y, 0f);

                float rotation =
                    Random.Range(-1f, 1f) * shakeAmount * 10f;

                cameraTransform.localRotation =
                    rotacionBase *
                    Quaternion.Euler(0f, 0f, rotation);
            }

            if (trainLight != null)
            {
                trainLight.intensity = Random.Range(
                    minLightIntensity,
                    maxLightIntensity
                );
            }

            yield return null;
        }

        // Restauramos únicamente el movimiento LOCAL del shake.
        if (cameraTransform != null)
        {
            cameraTransform.localPosition = posicionBase;
            cameraTransform.localRotation = rotacionBase;
        }

        if (trainLight != null)
        {
            trainLight.intensity = originalLightIntensity;
        }
    }
}