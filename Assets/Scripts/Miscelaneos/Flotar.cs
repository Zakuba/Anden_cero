using UnityEngine;

public class Flotar : MonoBehaviour
{
    [SerializeField] private float altura = 0.2f;
    [SerializeField] private float velocidad = 2f;

    private Vector3 posicionInicial;

    private void Start()
    {
        posicionInicial = transform.position;
    }

    private void Update()
    {
        float movimiento = Mathf.Sin(Time.time * velocidad) * altura;

        transform.position = posicionInicial + Vector3.up * movimiento;
    }
}