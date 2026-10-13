using UnityEngine;

public class PlayerLivesUI : MonoBehaviour
{
    [Header("Corazones intactos")]
    [Tooltip("Corazones normales en orden (1, 2, 3...)")]
    public GameObject[] heartIcons;

    [Header("Corazones rotos")]
    [Tooltip("Corazones rotos en el mismo orden que los normales")]
    public GameObject[] brokenHeartIcons;

    public void UpdateHearts(int currentLives)
    {
        for (int i = 0; i < heartIcons.Length; i++)
        {
            bool hasLife = i < currentLives;

            if (heartIcons[i] != null)
                heartIcons[i].SetActive(hasLife);

            if (i < brokenHeartIcons.Length && brokenHeartIcons[i] != null)
                brokenHeartIcons[i].SetActive(!hasLife);
        }
    }
}