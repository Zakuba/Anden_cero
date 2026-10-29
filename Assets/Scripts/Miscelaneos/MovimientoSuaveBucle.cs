
using UnityEngine;

public class MovimientoSuaveBucle : MonoBehaviour
{
    [Header("Puntos de movimiento")]
    [SerializeField] private Transform puntoA;
    [SerializeField] private Transform puntoB;

    [Header("Configuración")]
    [Min(0.1f)]
    [SerializeField] private float duracionTrayecto = 3f;

    [SerializeField] private bool iniciarEnA = true;
    [SerializeField] private bool usarPosicionLocal = false;

    [Header("Movimiento")]
    [SerializeField] private bool movimientoAutomatico = true;

    private float tiempo;
    private bool avanzando = true;

    private void Start()
    {
        if (puntoA == null || puntoB == null)
        {
            Debug.LogError(
                "[MovimientoSuaveBucle] Asigná los puntos A y B.",
                this
            );

            enabled = false;
            return;
        }

        transform.position = puntoA.position;
        transform.localPosition = usarPosicionLocal
            ? puntoA.localPosition
            : transform.localPosition;

        if (!iniciarEnA)
        {
            if (usarPosicionLocal)
                transform.localPosition = puntoB.localPosition;
            else
                transform.position = puntoB.position;

            avanzando = false;
        }
    }

    private void Update()
    {
        if (!movimientoAutomatico)
            return;

        tiempo += Time.deltaTime / duracionTrayecto;

        // Curva SmoothStep: acelera al principio y frena al final.
        float t = Mathf.Clamp01(tiempo);
        float suavizado = t * t * (3f - 2f * t);

        Vector3 destino;
        Vector3 origen;

        if (avanzando)
        {
            origen = usarPosicionLocal
                ? puntoA.localPosition
                : puntoA.position;

            destino = usarPosicionLocal
                ? puntoB.localPosition
                : puntoB.position;
        }
        else
        {
            origen = usarPosicionLocal
                ? puntoB.localPosition
                : puntoB.position;

            destino = usarPosicionLocal
                ? puntoA.localPosition
                : puntoA.position;
        }

        if (usarPosicionLocal)
            transform.localPosition =
                Vector3.Lerp(origen, destino, suavizado);
        else
            transform.position =
                Vector3.Lerp(origen, destino, suavizado);

        if (tiempo >= 1f)
        {
            tiempo = 0f;
            avanzando = !avanzando;
        }
    }
}
