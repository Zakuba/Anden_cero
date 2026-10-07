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
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        notificationPanel.SetActive(false);
    }

    public void ShowAchievement(string title)
    {
        StopAllCoroutines();
        StartCoroutine(NotificationRoutine(title));
    }

    private IEnumerator NotificationRoutine(string title)
    {
        achievementTitleText.text = "¡Logro Desbloqueado!\n" + title;
        notificationPanel.SetActive(true);
        
        yield return new WaitForSeconds(4f);
        
        notificationPanel.SetActive(false);
    }
}