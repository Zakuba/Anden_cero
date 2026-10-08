using UnityEngine;
using System.Collections;

public class ExplosionBomba : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float duracion = 3f;

    [Header("Objeto que titila")]
    [SerializeField] private GameObject objetoTitileo;

    [Header("Velocidad del titileo")]
    [SerializeField] private float intervaloInicial = 0.5f;
    [SerializeField] private float intervaloFinal = 0.05f;

    [Header("Colores")]
    [SerializeField] private Color colorNormal = Color.white;
    [SerializeField] private Color colorRojo = Color.red;

    private Renderer objetoRenderer;

    private void Start()
    {
        if (objetoTitileo != null)
        {
            objetoRenderer = objetoTitileo.GetComponent<Renderer>();

            if (objetoRenderer != null)
            {
                objetoRenderer.material.color = colorNormal;
            }
        }

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

            if (objetoRenderer != null)
                objetoRenderer.material.color = colorRojo;

            yield return new WaitForSeconds(intervalo);

            if (objetoRenderer != null)
                objetoRenderer.material.color = colorNormal;

            yield return new WaitForSeconds(intervalo);

            tiempoTranscurrido += intervalo * 2f;
        }

        // Simular la desaparición de la bomba
        if (objetoTitileo != null)
        {
            objetoTitileo.SetActive(false);
        }

        // Destruir la bomba
        Destroy(gameObject);
    }
}