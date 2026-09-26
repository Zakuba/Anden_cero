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
    [SerializeField] private float shakeSpeed = 20f;

    [Header("Parpadeo de luz")]
    [SerializeField] private Light trainLight;
    [SerializeField] private float minLightIntensity = 0f;
    [SerializeField] private float maxLightIntensity = 1f;
    [SerializeField] private float lightFlickerSpeed = 15f;

    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private float originalLightIntensity;

    private void Start()
    {
        if (cameraTransform != null)
        {
            originalCameraPosition = cameraTransform.localPosition;
            originalCameraRotation = cameraTransform.localRotation;
        }

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
            // Esperar antes de spawnear el siguiente tren
            yield return new WaitForSeconds(spawnInterval);

            // Spawnear el tren
            GameObject train = Instantiate(
                trainPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            // Empezar cámara + luz al mismo tiempo
            if (cameraTransform != null || trainLight != null)
            {
                StartCoroutine(ShakeCameraAndFlickerLight());
            }

            // Mover el tren
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

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            // =========================
            // CÁMARA
            // =========================

            if (cameraTransform != null)
            {
                float x = Random.Range(-1f, 1f) * shakeAmount;
                float y = Random.Range(-1f, 1f) * shakeAmount;

                cameraTransform.localPosition =
                    originalCameraPosition + new Vector3(x, y, 0f);

                float rotation =
                    Random.Range(-1f, 1f) * shakeAmount * 10f;

                cameraTransform.localRotation =
                    originalCameraRotation *
                    Quaternion.Euler(0f, 0f, rotation);
            }

            // =========================
            // LUZ
            // =========================

            if (trainLight != null)
            {
                trainLight.intensity = Random.Range(
                    minLightIntensity,
                    maxLightIntensity
                );
            }

            yield return new WaitForSeconds(1f / lightFlickerSpeed);
        }

        // Restaurar cámara
        if (cameraTransform != null)
        {
            cameraTransform.localPosition = originalCameraPosition;
            cameraTransform.localRotation = originalCameraRotation;
        }

        // Restaurar intensidad original
        if (trainLight != null)
        {
            trainLight.intensity = originalLightIntensity;
        }
    }
}