using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SelectorOpciones : MonoBehaviour
{
    [System.Serializable]
    public class BotonOpcion
    {
        public Button boton;            // Solo pides el botón
        public TextMeshProUGUI texto;   // Y el texto hijo
    }

    [Header("Botones del Grupo")]
    [SerializeField] private BotonOpcion[] opciones;

    [Header("Índice Inicial Seleccionado")]
    [SerializeField] private int indiceSeleccionado = 0;

    // Colores exactos en Hexadecimal
    private Color colorCyan;
    private Color colorMagenta;

    private void Awake()
    {
        ColorUtility.TryParseHtmlString("#12E2D5", out colorCyan);
        ColorUtility.TryParseHtmlString("#E61C7E", out colorMagenta);
    }

    private void Start()
    {
        for (int i = 0; i < opciones.Length; i++)
        {
            int index = i; // Copia local para evitar problemas de captura en el closure
            opciones[i].boton.onClick.AddListener(() => Seleccionar(index));
        }

        ActualizarVisuales();
    }

    public void Seleccionar(int index)
    {
        indiceSeleccionado = index;
        ActualizarVisuales();
    }

    private void ActualizarVisuales()
    {
        for (int i = 0; i < opciones.Length; i++)
        {
            bool estaSeleccionado = (i == indiceSeleccionado);
            Color colorFinal = estaSeleccionado ? colorCyan : colorMagenta;

            if (opciones[i].boton.image != null)
                opciones[i].boton.image.color = colorFinal;

            if (opciones[i].texto != null)
                opciones[i].texto.color = colorFinal;
        }
    }

    // Método público para consultar qué opción está activa (0, 1 o 2)
    public int ObtenerOpcionSeleccionada()
    {
        return indiceSeleccionado;
    }
}