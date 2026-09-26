using UnityEngine;
using System.Collections;

public class LuzBomba : MonoBehaviour
{
    [Header("Luz")]
    [SerializeField] private Light luz;

    [Header("Parpadeo")]
    [SerializeField] private float intervalo = 0.5f;

    private void Start()
    {
        if (luz == null)
            luz = GetComponent<Light>();

        StartCoroutine(Parpadeo());
    }

    private IEnumerator Parpadeo()
    {
        while (true)
        {
            // Encender
            luz.enabled = true;

            yield return new WaitForSeconds(intervalo);

            // Apagar
            luz.enabled = false;

            yield return new WaitForSeconds(intervalo);
        }
    }
}