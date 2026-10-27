using UnityEngine;
using UnityEngine.EventSystems;

public class IconoEscritorio : MonoBehaviour, IPointerClickHandler
{
    public VentanaEscritorio ventana;

    public void OnPointerClick(PointerEventData e)
    {
        if (e.clickCount >= 2) ventana.Abrir(); // doble clic
    }
}