using UnityEngine;

public class PlayerLivesUI : MonoBehaviour
{
    [Header("Iconos de Corazones")]
    [Tooltip("Arrastra aquí los 3 objetos de imagen de los corazones en orden (1, 2, 3)")]
    public GameObject[] heartIcons;

    public void UpdateHearts(int currentLives)
    {
        for (int i = 0; i < heartIcons.Length; i++)
        {
            // Si el índice del corazón es menor a las vidas actuales, se activa. Si es mayor o igual, se desactiva.
            if (heartIcons[i] != null)
            {
                heartIcons[i].SetActive(i < currentLives);
            }
        }
    }
}