using UnityEngine;
using TMPro;
using System.Collections;

public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance;

    [Header("UI Elements")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TMP_Text achievementTitleText;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }

        if (notificationPanel != null)
        {
            notificationPanel.SetActive(false);
        }
    }

    // Ahora recibe título y descripción
    public void ShowAchievement(string title, string description)
    {
        if (notificationPanel == null || achievementTitleText == null) return;

        StopAllCoroutines();
        StartCoroutine(NotificationRoutine(title, description));
    }

    private IEnumerator NotificationRoutine(string title, string description)
    {
        // Une el título y la descripción entre paréntesis
        achievementTitleText.text = $"¡Logro Desbloqueado!\n{title} ({description})";
        notificationPanel.SetActive(true);
        
        yield return new WaitForSeconds(4f);
        
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(false);
        }
    }
}