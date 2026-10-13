
using System;
using TMPro;
using UnityEngine;

public class CCTVTimestamp : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TMP_Text timestampText;
    [SerializeField] private TMP_Text recText;

    [Header("Configuración")]
    [SerializeField] private string identificadorCamara = "stingCam";
    [SerializeField] private bool usarHoraReal = false;
    [SerializeField] private bool mostrarREC = true;
    [SerializeField] private float velocidadParpadeo = 1f;

    private float temporizador;
    private bool recVisible = true;

    private void Update()
    {
        // Actualiza el reloj una vez por segundo.
        temporizador += Time.unscaledDeltaTime;

        if (temporizador >= 1f)
        {
            temporizador = 0f;
            ActualizarHora();
        }

        // Parpadeo del indicador REC.
        if (recText != null && mostrarREC)
        {
            recText.gameObject.SetActive(true);

            float fase = Mathf.Repeat(
                Time.unscaledTime * velocidadParpadeo, 1f
            );

            recVisible = fase < 0.5f;
            recText.alpha = recVisible ? 1f : 0.15f;
        }
        else if (recText != null)
        {
            recText.gameObject.SetActive(false);
        }
    }

    private void ActualizarHora()
    {
        if (timestampText == null)
            return;

        DateTime hora = usarHoraReal
            ? DateTime.Now
            : DateTime.Today.AddSeconds(Time.timeSinceLevelLoad);

        timestampText.text =
            $"{identificadorCamara}  {hora:dd-MM-yyyy HH:mm:ss}";
    }
}
