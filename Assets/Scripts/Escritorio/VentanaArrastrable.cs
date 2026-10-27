using UnityEngine;
using UnityEngine.EventSystems;

public class VentanaArrastrable : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public RectTransform ventana;   // si lo dejás vacío, usa el padre
    RectTransform contenedor;
    Vector2 offset;

    void Awake()
    {
        if (ventana == null) ventana = transform.parent as RectTransform;
        contenedor = ventana.parent as RectTransform;
    }

    public void OnPointerDown(PointerEventData e)
    {
        ventana.SetAsLastSibling(); // la trae al frente
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                contenedor, e.position, e.pressEventCamera, out Vector2 local))
            offset = ventana.anchoredPosition - local;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                contenedor, e.position, e.pressEventCamera, out Vector2 local))
            return;

        Vector2 pos = local + offset;

        // Que no se salga del escritorio
        Vector2 mitad = contenedor.rect.size / 2.87f;
        pos.x = Mathf.Clamp(pos.x, -mitad.x, mitad.x);
        pos.y = Mathf.Clamp(pos.y, -mitad.y, mitad.y);

        ventana.anchoredPosition = pos;
    }
}