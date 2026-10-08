using UnityEngine;
using System.Collections;

public class BasicExplosionVFX : MonoBehaviour
{
    [Header("Duración")]
    [Tooltip("Tiempo total que permanece el efecto.")]
    [SerializeField] private float destructionDelay = 1.5f;

    [Header("Animación de desaparición")]
    [Tooltip("Cuánto dura la animación antes de desaparecer.")]
    [SerializeField] private float desaparicionDuracion = 0.2f;

    [Tooltip("Distancia que baja el fuego durante la desaparición.")]
    [SerializeField] private float distanciaBajada = 0.5f;

    [Tooltip("Escala final del fuego.")]
    [SerializeField] private float escalaFinal = 0.2f;

    private Vector3 posicionInicial;
    private Vector3 escalaInicial;

    private void Start()
    {
        posicionInicial = transform.position;
        escalaInicial = transform.localScale;

        StartCoroutine(Desaparecer());
    }

    private IEnumerator Desaparecer()
    {
        float tiempoAntesDeDesaparecer =
            destructionDelay - desaparicionDuracion;

        if (tiempoAntesDeDesaparecer > 0f)
        {
            yield return new WaitForSeconds(tiempoAntesDeDesaparecer);
        }

        float tiempo = 0f;

        while (tiempo < desaparicionDuracion)
        {
            tiempo += Time.deltaTime;

            float progreso = Mathf.Clamp01(
                tiempo / desaparicionDuracion
            );

            // Hace que empiece lento y termine rápido
            float curva = progreso * progreso;

            // Baja
            transform.position = Vector3.Lerp(
                posicionInicial,
                posicionInicial + Vector3.down * distanciaBajada,
                curva
            );

            // Reduce el tamaño
            transform.localScale = Vector3.Lerp(
                escalaInicial,
                escalaInicial * escalaFinal,
                curva
            );

            yield return null;
        }

        Destroy(gameObject);
    }
}