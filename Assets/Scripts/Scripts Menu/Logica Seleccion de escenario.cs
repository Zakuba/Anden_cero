using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class SelectorEscenario : MonoBehaviour
{
    [Header("Elementos de UI")]
    [SerializeField] private Button botonIzquierda;
    [SerializeField] private Button botonDerecha;
    [SerializeField] private TextMeshProUGUI textoEscenario;

    [Header("Lista de Escenarios")]
    [SerializeField] private string[] escenarios = new string[]
    {
        "Andén Central",
        "Túneles y Vías",
        "Sala de Control"
    };

    [Header("Fondos de los Escenarios")]
    [SerializeField] private GameObject[] fondosEscenarios;

    [Header("Estática")]
    [SerializeField] private GameObject estatica;

    [SerializeField] private float duracionEstatica = 0.35f;

    [Header("Índice Actual")]
    [SerializeField] private int indiceActual = 0;
    private Coroutine cambioActual;
    private void Start()
    {
        if (botonIzquierda != null)
            botonIzquierda.onClick.AddListener(AnteriorEscenario);

        if (botonDerecha != null)
            botonDerecha.onClick.AddListener(SiguienteEscenario);

        ActualizarTexto();
        ActualizarFondos();
    }

    public void SiguienteEscenario()
    {
        if (escenarios.Length == 0) return;

        indiceActual = (indiceActual + 1) % escenarios.Length;

        CambiarEscenarioConEstatica();
    }

    public void AnteriorEscenario()
    {
        if (escenarios.Length == 0) return;

        indiceActual--;

        if (indiceActual < 0)
        {
            indiceActual = escenarios.Length - 1;
        }

        CambiarEscenarioConEstatica();
    }
    private void CambiarEscenarioConEstatica()
    {
        if (cambioActual != null)
            StopCoroutine(cambioActual);

        cambioActual = StartCoroutine(TransicionEscenario());
    }

    private IEnumerator TransicionEscenario()
    {
        // Mostrar estática
        if (estatica != null)
            estatica.SetActive(true);

        // Esperar la mitad de la transición
        yield return new WaitForSeconds(duracionEstatica / 2f);

        // Cambiar fondo
        ActualizarFondos();

        // Actualizar texto
        ActualizarTexto();

        // Esperar la otra mitad
        yield return new WaitForSeconds(duracionEstatica / 2f);

        // Ocultar estática
        if (estatica != null)
            estatica.SetActive(false);

        cambioActual = null;
    }

    private void ActualizarFondos()
    {
        for (int i = 0; i < fondosEscenarios.Length; i++)
        {
            fondosEscenarios[i].SetActive(i == indiceActual);
        }
    }

    private void ActualizarTexto()
    {
        if (textoEscenario != null && escenarios.Length > 0)
        {
            textoEscenario.text = escenarios[indiceActual];
        }
    }

    // Métodos públicos para consultar desde el Game Manager o Netcode
    public int ObtenerIndiceEscenario()
    {
        return indiceActual;
    }

    public string ObtenerNombreEscenario()
    {
        return escenarios[indiceActual];
    }
}