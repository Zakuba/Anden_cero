using UnityEngine;

public class CameraHeadBob : MonoBehaviour
{
    [Header("Balanceo")]
    [Tooltip("Metros que sube y baja la cámara en cada paso")]
    [SerializeField] private float verticalAmount = 0.04f;
    [Tooltip("Metros que se mueve a los costados (un ciclo cada dos pasos)")]
    [SerializeField] private float horizontalAmount = 0.025f;
    [Tooltip("Metros que avanza el jugador por cada paso. Menor = pasos más rápidos")]
    [SerializeField] private float stepLength = 0.9f;

    [Header("Respuesta")]
    [Tooltip("Velocidad (m/s) a la que el balanceo llega al máximo. Con la velocidad lenta del swap, usá un valor bajo")]
    [SerializeField] private float speedForFullBob = 2.5f;
    [Tooltip("Qué tan rápido vuelve la cámara a su lugar al frenar")]
    [SerializeField] private float returnSpeed = 10f;

    private Camera cam;
    private CharacterController controller;
    private Vector3 basePos;
    private float phase;
    private Vector3 currentOffset;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        controller = GetComponentInParent<CharacterController>();
        basePos = transform.localPosition;
    }

    private void LateUpdate()
    {
        // Solo actúa cuando esta es la cámara activa. Así no interfiere con el viaje de la cámara del mapa,
        // que usa la posición de esta cámara como destino.
        if (cam == null || !cam.enabled || controller == null) return;

        Vector3 v = controller.velocity;
        v.y = 0f;
        float speed = v.magnitude;
        bool moving = controller.isGrounded && speed > 0.1f;

        Vector3 targetOffset = Vector3.zero;

        if (moving)
        {
            // La fase avanza según la distancia recorrida: más rápido = pasos más rápidos
            phase += (speed / Mathf.Max(0.01f, stepLength)) * Mathf.PI * Time.deltaTime;

            float strength = Mathf.Clamp01(speed / Mathf.Max(0.01f, speedForFullBob));
            targetOffset = new Vector3(
                Mathf.Cos(phase) * horizontalAmount * strength,
                Mathf.Sin(phase * 2f) * verticalAmount * strength,
                0f);
        }

        // Suavizado independiente del framerate (también hace la vuelta al reposo)
        float k = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
        currentOffset = Vector3.Lerp(currentOffset, targetOffset, k);

        transform.localPosition = basePos + currentOffset;
    }
}