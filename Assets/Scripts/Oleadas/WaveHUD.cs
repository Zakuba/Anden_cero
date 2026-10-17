using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// HUD de oleadas. Solo lee las NetworkVariable de WaveManager, así que se ve
/// igual en el host y en los clientes. Si la escena no tiene WaveManager
/// (por ejemplo Battle Royale), el panel raíz queda oculto.
/// </summary>
public class WaveHUD : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("Contenedor de todo el HUD de oleadas. Se oculta si no hay WaveManager. " +
             "IMPORTANTE: este script debe estar en OTRO objeto (por ejemplo el Canvas), " +
             "no en el mismo que 'root', porque desactivar root lo apagaría a él también.")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text waveText;     // "Oleada 3"
    [SerializeField] private TMP_Text enemiesText;  // "Enemigos: 7"
    [SerializeField] private TMP_Text bannerText;   // "Oleada 4 en 6 s" / "¡Oleada 4!"

    [Header("Opciones")]
    [Tooltip("Marcado: muestra vivos + en cola (lo que falta para terminar la oleada). " +
             "Desmarcado: solo los que están vivos en el mapa en este momento.")]
    [SerializeField] private bool includeQueuedEnemies = true;
    [SerializeField] private float combatBannerSeconds = 2f;

    private int lastWave = -1;
    private int lastEnemies = -1;
    private int lastCountdown = -1;
    private WavePhase lastPhase = WavePhase.Idle;
    private float combatBannerUntil;

    private void Awake()
    {
        SetRootActive(false);
    }

    private void Update()
    {
        WaveManager wm = WaveManager.Instance;

        if (wm == null || !wm.IsSpawned)
        {
            SetRootActive(false);
            return;
        }

        WavePhase phase = wm.Phase;

        // En el lobby (antes de iniciar) y al terminar la partida no mostramos el HUD.
        bool visible = phase == WavePhase.Rest || phase == WavePhase.Combat;
        SetRootActive(visible);
        if (!visible) return;

        UpdateWave(wm.CurrentWave);
        UpdateEnemies(includeQueuedEnemies ? wm.EnemiesRemaining : wm.EnemiesAlive);
        UpdateBanner(wm, phase);

        lastPhase = phase;
    }

    private void UpdateWave(int wave)
    {
        if (wave == lastWave) return;
        lastWave = wave;

        if (waveText != null)
            waveText.text = "Oleada " + wave;
    }

    private void UpdateEnemies(int count)
    {
        if (count == lastEnemies) return;
        lastEnemies = count;

        if (enemiesText != null)
            enemiesText.text = "Enemigos: " + count;
    }

    private void UpdateBanner(WaveManager wm, WavePhase phase)
    {
        if (bannerText == null) return;

        if (phase == WavePhase.Rest)
        {
            double remaining = wm.PhaseEndServerTime - NetworkManager.Singleton.ServerTime.Time;
            int seconds = Mathf.Max(0, Mathf.CeilToInt((float)remaining));

            if (seconds != lastCountdown || lastPhase != WavePhase.Rest)
            {
                lastCountdown = seconds;
                bannerText.text = "Oleada " + wm.CurrentWave + " en " + seconds + " s";
            }

            bannerText.gameObject.SetActive(true);
            return;
        }

        // Combat: aviso corto al arrancar la oleada y después se oculta.
        if (lastPhase != WavePhase.Combat)
        {
            lastCountdown = -1;
            combatBannerUntil = Time.time + combatBannerSeconds;
            bannerText.text = "¡Oleada " + wm.CurrentWave + "!";
        }

        bannerText.gameObject.SetActive(Time.time < combatBannerUntil);
    }

    private void SetRootActive(bool active)
    {
        if (root != null && root.activeSelf != active)
            root.SetActive(active);
    }
}
