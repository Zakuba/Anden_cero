using UnityEngine;

public class PlayerLivesUI : MonoBehaviour
{
    [Header("Corazones intactos")]
    [Tooltip("Corazones normales en orden (1, 2, 3...)")]
    public GameObject[] heartIcons;

    [Header("Corazones rotos")]
    [Tooltip("Corazones rotos en el mismo orden que los normales")]
    public GameObject[] brokenHeartIcons;

    private bool warnedMissingSlots;

    // Sin vidas máximas se muestran todos los corazones del HUD (comportamiento anterior).
    public void UpdateHearts(int currentLives)
    {
        UpdateHearts(currentLives, heartIcons.Length);
    }

    // Los espacios por encima de maxLives se ocultan (ni corazón ni corazón roto).
    public void UpdateHearts(int currentLives, int maxLives)
    {
        if (maxLives > heartIcons.Length && !warnedMissingSlots)
        {
            warnedMissingSlots = true;
            Debug.LogWarning("[PlayerLivesUI] El HUD tiene " + heartIcons.Length +
                             " corazones pero la vida máxima es " + maxLives +
                             ". Agrega los que falten a los arreglos.");
        }

        for (int i = 0; i < heartIcons.Length; i++)
        {
            bool slotInUse = i < maxLives;
            bool hasLife = i < currentLives;

            if (heartIcons[i] != null)
                heartIcons[i].SetActive(slotInUse && hasLife);

            if (i < brokenHeartIcons.Length && brokenHeartIcons[i] != null)
                brokenHeartIcons[i].SetActive(slotInUse && !hasLife);
        }
    }
}