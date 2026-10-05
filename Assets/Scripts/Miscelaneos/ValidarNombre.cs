using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using System.Collections;

public class ValidarNombre : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TMP_InputField inputNombre;
    [SerializeField] private Image imagenBoton;

    [Header("Acciones si el nombre es válido")]
    [SerializeField] private UnityEvent accionesValidas;

    [Header("Colores")]
    [SerializeField] private Color colorNormal = Color.white;
    [SerializeField] private Color colorError = Color.red;

    [Header("Destello")]
    [SerializeField] private float duracionDestello = 0.15f;

    private Coroutine destelloActual;

    public void ValidarYContinuar()
    {
        if (string.IsNullOrWhiteSpace(inputNombre.text))
        {
            if (destelloActual != null)
                StopCoroutine(destelloActual);

            destelloActual = StartCoroutine(DestelloRojo());
            return;
        }

        // Nombre válido → ejecutamos todas las acciones
        accionesValidas.Invoke();
    }

    private IEnumerator DestelloRojo()
    {
        imagenBoton.color = colorError;

        yield return new WaitForSeconds(duracionDestello);

        imagenBoton.color = colorNormal;

        destelloActual = null;
    }
}