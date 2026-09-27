using UnityEngine;

public class MenuPrincipal : MonoBehaviour
{
    public void SalirJuego()
    {
        Debug.Log("Cerrando el juego...");

        // Cierra la aplicación compilada
        Application.Quit();

        // Detiene el modo Play dentro del editor
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}