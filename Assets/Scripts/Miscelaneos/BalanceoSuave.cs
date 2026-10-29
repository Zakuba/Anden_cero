
using UnityEngine;

public class BalanceoSuave : MonoBehaviour
{
    [Header("Configuración del balanceo")]
    [SerializeField] private float anguloMaximo = 15f;
    [SerializeField] private float velocidad = 1f;

    [Header("Ejes de rotación local")]
    [SerializeField] private bool balancearX = false;
    [SerializeField] private bool balancearY = false;
    [SerializeField] private bool balancearZ = true;

    private Quaternion rotacionInicial;

    private void Start()
    {
        rotacionInicial = transform.localRotation;
    }

    private void Update()
    {
        float movimiento = Mathf.Sin(Time.time * velocidad) * anguloMaximo;

        Vector3 rotacion = Vector3.zero;

        if (balancearX)
            rotacion.x = movimiento;

        if (balancearY)
            rotacion.y = movimiento;

        if (balancearZ)
            rotacion.z = movimiento;

        transform.localRotation = rotacionInicial * Quaternion.Euler(rotacion);
    }
}
