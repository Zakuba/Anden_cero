using UnityEngine;
using System.Collections;

public class TitileoRojo : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float duracion = 15f;

    [Header("Velocidad del titileo")]
    [SerializeField] private float intervaloInicial = 0.5f;
    [SerializeField] private float intervaloFinal = 0.05f;

    [Header("Colores")]
    [SerializeField] private Color colorNormal = Color.white;
    [SerializeField] private Color colorRojo = Color.red;

    [Header("FX Final")]
    [SerializeField] private GameObject fxFinal;
    [SerializeField] private float duracionFX = 2f;

    private Renderer objetoRenderer;

    private void Start()
    {
        objetoRenderer = GetComponent<Renderer>();

        if (objetoRenderer == null)
        {
            Debug.LogWarning(
                "TitileoRojo: No se encontró un Renderer en " + gameObject.name
            );

            return;
        }

        objetoRenderer.material.color = colorNormal;

        StartCoroutine(Titileo());
    }

    private IEnumerator Titileo()
    {
        float tiempoTranscurrido = 0f;

        while (tiempoTranscurrido < duracion)
        {
            float progreso = tiempoTranscurrido / duracion;

            float intervalo = Mathf.Lerp(
                intervaloInicial,
                intervaloFinal,
                progreso
            );

            // Rojo
            objetoRenderer.material.color = colorRojo;

            yield return new WaitForSeconds(intervalo);

            // Normal
            objetoRenderer.material.color = colorNormal;

            yield return new WaitForSeconds(intervalo);

            tiempoTranscurrido += intervalo * 2f;
        }

        // Dejar el objeto rojo al terminar
        objetoRenderer.material.color = colorRojo;

        // Crear el FX
        GenerarFX();
    }

    private void GenerarFX()
    {
        if (fxFinal == null)
            return;

        GameObject fx = Instantiate(
            fxFinal,
            transform.position,
            Quaternion.identity
        );

        Destroy(fx, duracionFX);
    }
}