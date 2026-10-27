using UnityEngine;
using UnityEngine.EventSystems;

public class CursorPantalla : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Texture2D cursorPantalla;
    public Vector2 hotspot = Vector2.zero;   // punto "activo" del cursor (0,0 = esquina superior izquierda)
    public CursorMode modo = CursorMode.Auto;

    bool dentro;

    public void OnPointerEnter(PointerEventData e)
    {
        dentro = true;
        Cursor.SetCursor(cursorPantalla, hotspot, modo);
    }

    public void OnPointerExit(PointerEventData e)
    {
        Restaurar();
    }

    // Si el escritorio se desactiva con el mouse adentro, no queda el cursor cambiado
    void OnDisable()
    {
        if (dentro) Restaurar();
    }

    void Restaurar()
    {
        dentro = false;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}