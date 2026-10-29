
using UnityEngine;

public class CamaraSeguridad : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float anguloMaximo = 45f;
    [SerializeField] private float velocidad = 1f;

    [Header("Eje de giro local")]
    [SerializeField] private Vector3 ejeGiro = Vector3.up;

    [Header("Movimiento")]
    [SerializeField] private bool iniciarEnExtremoOpuesto = false;

    private Quaternion rotacionInicial;
    private float desfase;

    private void Start()
    {
        rotacionInicial = transform.localRotation;

        if (iniciarEnExtremoOpuesto)
            desfase = Mathf.PI;
    }

    private void Update()
    {
        float angulo = Mathf.Sin(Time.time * velocidad + desfase)
                       * anguloMaximo;

        transform.localRotation =
            rotacionInicial *
            Quaternion.AngleAxis(angulo, ejeGiro.normalized);
    }
}
