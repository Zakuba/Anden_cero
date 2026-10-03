using UnityEngine;

public class RotacionAspas : MonoBehaviour
{
    [Header("Configuración de Rotación")]
    [Tooltip("Velocidad a la que girarán las aspas. Usa valores negativos para girar al revés.")]
    public float velocidadGiro = 1000f;
    
    [Tooltip("El eje sobre el cual gira. Normalmente es Y (0,1,0) o Z (0,0,1) dependiendo del modelo.")]
    public Vector3 ejeDeRotacion = new Vector3(0, 1, 0); // Eje Y por defecto

    void Update()
    {
        // Rota el objeto en su eje local. Time.deltaTime asegura que gire suave sin importar los FPS.
        transform.Rotate(ejeDeRotacion * velocidadGiro * Time.deltaTime, Space.Self);
    }
}