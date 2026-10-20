
using UnityEngine;
using UnityEngine.UI;

public class SintyHelper : MonoBehaviour
{
    [Header("Probabilidad")]
    [Range(1, 100)]
    [SerializeField] private int probabilidad = 20;

    [Header("Easter Egg")]
    [SerializeField] private GameObject objetoEasterEgg;

    [Header("Boton invisible")]
    [SerializeField] private GameObject botonInvisible;

    private void Start()
    {
        // Ambos comienzan desactivados.
        if (objetoEasterEgg != null)
            objetoEasterEgg.SetActive(false);

        if (botonInvisible != null)
            botonInvisible.SetActive(false);

        IntentarGenerarEasterEgg();
    }

    private void IntentarGenerarEasterEgg()
    {
        // 20% de probabilidad.
        if (Random.Range(1, 101) > probabilidad)
            return;

        // Activar el objeto de terror y el boton.
        if (objetoEasterEgg != null)
            objetoEasterEgg.SetActive(true);

        if (botonInvisible != null)
            botonInvisible.SetActive(true);

        Debug.Log("...");
    }
}
