using UnityEngine;

public class BasicExplosionVFX : MonoBehaviour
{
    [Tooltip("Tiempo en segundos antes de que el efecto de fuego desaparezca.")]
    public float destructionDelay = 1.5f;

    private void Start()
    {
        // Apenas este objeto es instanciado en la escena, inicia su cuenta regresiva para destruirse
        Destroy(gameObject, destructionDelay);
    }
}