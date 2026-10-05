using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class PowerUpUI : MonoBehaviour
{
    [Header("Referencias Visuales")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image effectIcon;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Íconos de Efectos")]
    // Asigna los sprites correspondientes a cada power-up en el Inspector
    [SerializeField] private Sprite expansorSprite;
    [SerializeField] private Sprite bombaExtraSprite;
    [SerializeField] private Sprite botasSprite;
    [SerializeField] private Sprite guanteSprite;
    [SerializeField] private Sprite escudoSprite;

    private float currentTimer;
    private bool isTimerActive;

    private void Update()
    {
        if (isTimerActive)
        {
            currentTimer -= Time.deltaTime;

            if (currentTimer > 0)
            {
                // Muestra el tiempo restante sin decimales (estilo Minecraft)
                timerText.text = Mathf.CeilToInt(currentTimer).ToString() + "s";
            }
            else
            {
                // Red de seguridad visual: si llega a 0 localmente, oculta el texto 
                // hasta que el servidor confirme el apagado oficial.
                timerText.text = "0s";
            }
        }
    }

    public void ActivateEffect(PowerUpType type, float duration)
    {
        // 1. Asigna el ícono correcto según el tipo
        effectIcon.sprite = GetSpriteForType(type);

        // 2. Configura y arranca el reloj
        currentTimer = duration;
        isTimerActive = true;

        // 3. Enciende el panel
        panelRoot.SetActive(true);
    }

    public void DeactivateEffect()
    {
        isTimerActive = false;
        panelRoot.SetActive(false);
    }

    private Sprite GetSpriteForType(PowerUpType type)
    {
        return type switch
        {
            PowerUpType.Expansor => expansorSprite,
            PowerUpType.BombaExtra => bombaExtraSprite,
            PowerUpType.Botas => botasSprite,
            PowerUpType.Guante => guanteSprite,
            PowerUpType.Escudo => escudoSprite,
            _ => null
        };
    }
}