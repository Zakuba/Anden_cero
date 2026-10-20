
using UnityEngine;

public class OcultarObjetoPorCamara : MonoBehaviour
{
    [Header("Camara")]
    [SerializeField] private Camera camara;

    [Header("Tren generado")]
    [SerializeField] private Transform trenInstanciado;

    [Header("Zona de deteccion")]
    [SerializeField] private Collider zona;

    private void Awake()
    {
        if (zona == null)
            zona = GetComponent<Collider>();

        if (zona == null)
        {
            Debug.LogError("Falta asignar la zona de deteccion.", this);
            enabled = false;
            return;
        }
    }

    private void LateUpdate()
    {
        if (camara == null || trenInstanciado == null)
            return;

        bool camaraDentro =
            zona.bounds.Contains(camara.transform.position);

        MeshRenderer[] renderers =
            trenInstanciado.GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer renderer in renderers)
        {
            renderer.enabled = !camaraDentro;
        }
    }

    public void AsignarTren(GameObject tren)
    {
        trenInstanciado = tren != null ? tren.transform : null;
    }
}

