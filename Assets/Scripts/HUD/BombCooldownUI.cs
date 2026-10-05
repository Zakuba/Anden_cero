using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BombCooldownUI : MonoBehaviour
{
    [Header("Referencias UI")]
    public Image bombIcon;
    public TextMeshProUGUI cooldownText;

    public bool IsCooldownActive { get; private set; }
    private float maxTimer;
    private float currentTimer;

    private void Start()
    {
        ResetUI(); // Nos aseguramos de que empiece brillante y listo
    }

private void Update()
    {
        if (IsCooldownActive)
        {
            currentTimer -= Time.deltaTime;

            if (currentTimer <= 0f)
            {
                // 1. Efecto visual de que ya terminó (se ilumina y desaparece el número)
                currentTimer = 0f;
                cooldownText.gameObject.SetActive(false);
                bombIcon.fillAmount = 1f;
                
                Color c = bombIcon.color;
                c.a = 1f;
                bombIcon.color = c;
                
                // IMPORTANTE: Ya no llamamos a ResetUI() aquí.
                // Dejamos IsCooldownActive = true temporalmente. 
                // Esto evita que el PlayerBombController reinicie el loop.
                // El estado real se liberará cuando el servidor confirme la explosión.
            }
            else
            {
                // 2. Continúa la cuenta regresiva normal
                cooldownText.text = currentTimer.ToString("F1");
                bombIcon.fillAmount = 1f - (currentTimer / maxTimer);
            }
        }
    }

    public void StartCooldown(float duration)
    {
        IsCooldownActive = true;
        maxTimer = duration;
        currentTimer = duration;

        // Oscurecemos el ícono reduciendo su canal Alfa (transparencia)
        Color c = bombIcon.color;
        c.a = 0.5f; // 30% de opacidad
        bombIcon.color = c;

        bombIcon.fillAmount = 0f; // Vaciamos el ícono para que empiece a llenarse
        cooldownText.gameObject.SetActive(true);
    }

    public void ResetUI()
    {
        IsCooldownActive = false;

        // Restauramos el color original
        Color c = bombIcon.color;
        c.a = 1f;
        bombIcon.color = c;

        bombIcon.fillAmount = 1f;
        cooldownText.gameObject.SetActive(false);
    }
}