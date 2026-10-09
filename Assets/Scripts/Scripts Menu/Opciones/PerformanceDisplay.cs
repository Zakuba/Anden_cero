using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class PerformanceDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private float updateInterval = 0.3f; // Se actualiza 3 veces por segundo

    private float timer;
    private int frameCount;
    private float currentFps;

    private void Update()
    {
        // Contar frames para el cálculo de FPS
        frameCount++;
        timer += Time.unscaledDeltaTime;

        if (timer >= updateInterval)
        {
            currentFps = frameCount / timer;
            frameCount = 0;
            timer = 0f;

            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        bool showFPS = SettingsManager.Instance == null || SettingsManager.Instance.currentSettings.showFPS;
        bool showPing = SettingsManager.Instance == null || SettingsManager.Instance.currentSettings.showPing;

        // Si ambos están apagados, ocultamos el texto
        if (!showFPS && !showPing)
        {
            statsText.text = "";
            return;
        }

        string result = "";

        // 1. Formato FPS
        if (showFPS)
        {
            result += $"FPS: {Mathf.RoundToInt(currentFps)}  ";
        }

        // 2. Formato Ping (Netcode for GameObjects)
        if (showPing)
        {
            result += GetPingString();
        }

        statsText.text = result;
    }

    private string GetPingString()
    {
        // Si no está corriendo el NetworkManager o no estamos conectados
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            return "Ping: --";
        }

        // Si somos el Host (servidor local), el ping siempre es 0
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            return "Ping: 0 ms (Host)";
        }

        // Si somos Cliente conectado: obtenemos el RTT hacia el Servidor
        if (NetworkManager.Singleton.IsClient)
        {
            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport != null)
            {
                ulong rtt = transport.GetCurrentRtt(NetworkManager.ServerClientId);
                return $"Ping: {rtt} ms";
            }
        }

        return "Ping: --";
    }
}