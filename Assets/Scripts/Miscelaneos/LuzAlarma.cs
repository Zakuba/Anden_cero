using UnityEngine;

public class LuzAlarma : MonoBehaviour
{
    public float velocidadRotacion = 180f;

    void Update()
    {
        transform.Rotate(0f, velocidadRotacion * Time.deltaTime, 0f);
    }
}