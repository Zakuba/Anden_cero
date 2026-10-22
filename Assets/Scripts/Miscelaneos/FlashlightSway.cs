using UnityEngine;

public class FlashlightLag : MonoBehaviour
{
    [Header("Referencia")]
    [SerializeField] private Light flashlight;

    [Header("Retraso al girar la cámara")]
    [Tooltip("Menor = la luz tarda más en alcanzar a la cámara")]
    [SerializeField] private float followSpeed = 5f;
    [Tooltip("Máximo desfase entre la luz y la cámara, en grados. 0 = sin retraso")]
    [SerializeField] private float maxLagAngle = 20f;

    [Header("Parpadeo")]
    [SerializeField] private bool flicker = true;
    [Tooltip("Variación de intensidad: 0.05 = ±5%")]
    [SerializeField] private float flickerAmount = 0.05f;
    [SerializeField] private float flickerSpeed = 10f;

    private Transform lightT;
    private Transform parent;
    private Quaternion baseLocalRot;
    private Quaternion currentRot; // rotación propia de la luz, en el mundo
    private float baseIntensity;
    private bool snap = true;

    private void Awake()
    {
        if (flashlight == null)
        {
            Debug.LogError("FlashlightLag: falta asignar la luz en el Inspector.");
            enabled = false;
            return;
        }

        lightT = flashlight.transform;
        parent = lightT.parent;
        baseLocalRot = lightT.localRotation;
        baseIntensity = flashlight.intensity;
    }

    private void OnEnable()
    {
        snap = true; // evita que la luz "barra" desde otra orientación al activarse
    }

    private void LateUpdate()
    {
        if (parent == null) return;

        // Rotación que tendría la luz pegada a la cámara
        Quaternion desired = parent.rotation * baseLocalRot;

        if (snap)
        {
            currentRot = desired;
            snap = false;
        }
        else
        {
            // Se parte de la rotación del frame anterior (guardada), no de lightT.rotation,
            // porque esa ya se movió junto con la cámara al ser su hija
            float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            Quaternion smoothed = Quaternion.Slerp(currentRot, desired, k);

            // Limita cuánto se puede atrasar respecto a la cámara
            currentRot = Quaternion.RotateTowards(desired, smoothed, maxLagAngle);
        }

        lightT.rotation = currentRot;

        if (flicker)
        {
            float n = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) - 0.5f;
            flashlight.intensity = baseIntensity * (1f + n * 2f * flickerAmount);
        }
    }
}