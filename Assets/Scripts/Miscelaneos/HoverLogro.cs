using UnityEngine;
using System.Collections;

public class HoverLogro : MonoBehaviour
{
    [Header("Objetos")]
    [SerializeField] private Transform objetoReal;
    [SerializeField] private Transform objetoFinal;

    [Header("Animación")]
    [SerializeField] private float duracion = 0.3f;

    private Vector3 posicionInicial;
    private Quaternion rotacionInicial;

    private Coroutine animacionActual;

    private void Start()
    {
        // Guardamos la posición y rotación originales
        posicionInicial = objetoReal.position;
        rotacionInicial = objetoReal.rotation;
    }

    public void MouseEntra()
    {
        AnimarHacia(
            objetoFinal.position,
            objetoFinal.rotation
        );
    }

    public void MouseSale()
    {
        AnimarHacia(
            posicionInicial,
            rotacionInicial
        );
    }

    private void AnimarHacia(Vector3 posicionDestino, Quaternion rotacionDestino)
    {
        if (animacionActual != null)
            StopCoroutine(animacionActual);

        animacionActual = StartCoroutine(
            MoverObjeto(posicionDestino, rotacionDestino)
        );
    }

    private IEnumerator MoverObjeto(
        Vector3 posicionDestino,
        Quaternion rotacionDestino
    )
    {
        Vector3 posicionComienzo = objetoReal.position;
        Quaternion rotacionComienzo = objetoReal.rotation;

        float tiempo = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;

            float t = Mathf.Clamp01(tiempo / duracion);

            // Movimiento suave
            t = Mathf.SmoothStep(0f, 1f, t);

            objetoReal.position = Vector3.Lerp(
                posicionComienzo,
                posicionDestino,
                t
            );

            objetoReal.rotation = Quaternion.Slerp(
                rotacionComienzo,
                rotacionDestino,
                t
            );

            yield return null;
        }

        // Aseguramos que termine exactamente en el destino
        objetoReal.position = posicionDestino;
        objetoReal.rotation = rotacionDestino;

        animacionActual = null;
    }
}   