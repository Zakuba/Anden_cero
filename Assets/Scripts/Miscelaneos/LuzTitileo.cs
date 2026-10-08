using UnityEngine;
using System.Collections;

public class LuzTitileo : MonoBehaviour
{
    [Header("Luz")]
    [SerializeField] private Light luz;

    [Header("Intervalo")]
    [SerializeField] private float intervaloMinimo = 0.05f;
    [SerializeField] private float intervaloMaximo = 0.4f;

    [Header("Intensidad")]
    [SerializeField] private float intensidadMinima = 0f;
    [SerializeField] private float intensidadMaxima = 1f;

    [Header("Probabilidad")]
    [Range(0f, 1f)]
    [SerializeField] private float probabilidadTitileo = 0.7f;

    private float intensidadOriginal;

    private void Start()
    {
        if (luz == null)
            luz = GetComponent<Light>();

        if (luz == null)
            return;

        intensidadOriginal = luz.intensity;

        StartCoroutine(Titilar());
    }

    private IEnumerator Titilar()
    {
        while (true)
        {
            float espera = Random.Range(intervaloMinimo, intervaloMaximo);
            yield return new WaitForSeconds(espera);

            if (Random.value <= probabilidadTitileo)
            {
                float intensidad = Random.Range(
                    intensidadMinima,
                    intensidadMaxima
                );

                luz.intensity = intensidad;
            }
            else
            {
                luz.intensity = intensidadOriginal;
            }
        }
    }
}