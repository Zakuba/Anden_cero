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

        Vector3 posicionFinal = destino.position;
        Quaternion rotacionFinal = destino.rotation;

        float tiempo = 0f;

        while (tiempo < duracionMovimiento)
        {
            tiempo += Time.deltaTime;

            float t = Mathf.Clamp01(tiempo / duracionMovimiento);
            t = Mathf.SmoothStep(0f, 1f, t);

            camara.position = Vector3.Lerp(
                posicionInicial,
                posicionFinal,
                t
            );

            camara.rotation = Quaternion.Slerp(
                rotacionInicial,
                rotacionFinal,
                t
            );

            yield return null;
        }

        camara.position = posicionFinal;
        camara.rotation = rotacionFinal;

        movimientoActual = null;
    }
}