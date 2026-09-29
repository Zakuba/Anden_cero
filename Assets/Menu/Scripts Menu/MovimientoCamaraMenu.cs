using UnityEngine;
using System.Collections;

public class MovimientoCamaraMenu : MonoBehaviour
{
    [Header("Cámara")]
    [SerializeField] private Transform camara;

    [Header("Movimiento")]
    [SerializeField] private float duracionMovimiento = 1.5f;

    private Coroutine movimientoActual;

    public void MoverACamara(Transform destino)
    {
        if (movimientoActual != null)
        {
            StopCoroutine(movimientoActual);
        }

        movimientoActual = StartCoroutine(Mover(destino));
    }

    private IEnumerator Mover(Transform destino)
    {
        Vector3 posicionInicial = camara.position;
        Quaternion rotacionInicial = camara.rotation;

        float tiempo = 0f;

        while (tiempo < duracionMovimiento)
        {
            tiempo += Time.deltaTime;

            float t = tiempo / duracionMovimiento;

            // Suaviza el movimiento
            t = Mathf.SmoothStep(0f, 1f, t);

            camara.position = Vector3.Lerp(
                posicionInicial,
                destino.position,
                t
            );

            camara.rotation = Quaternion.Slerp(
                rotacionInicial,
                destino.rotation,
                t
            );

            yield return null;
        }

        camara.position = destino.position;
        camara.rotation = destino.rotation;

        movimientoActual = null;
    }
}