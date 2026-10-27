using UnityEngine;

public class VentanaEscritorio : MonoBehaviour
{
    public void Abrir()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Cerrar()
    {
        gameObject.SetActive(false);
    }
}