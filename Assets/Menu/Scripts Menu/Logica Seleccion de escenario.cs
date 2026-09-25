using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Índice Actual")]
    [SerializeField] private int indiceActual = 0;

    private void Start()
    {
        if (botonIzquierda != null)
            botonIzquierda.onClick.AddListener(AnteriorEscenario);

        if (botonDerecha != null)
            botonDerecha.onClick.AddListener(SiguienteEscenario);

        ActualizarTexto();
    }

    public void SiguienteEscenario()
    {
        if (escenarios.Length == 0) return;
        
        // Avanza y vuelve a 0 si llega al final (cíclico)
        indiceActual = (indiceActual + 1) % escenarios.Length;
        ActualizarTexto();
    }

    public void AnteriorEscenario()
    {
        if (escenarios.Length == 0) return;

        // Retrocede y vuelve al último elemento si baja de 0
        indiceActual--;
        if (indiceActual < 0)
        {
            indiceActual = escenarios.Length - 1;
        }
        ActualizarTexto();
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