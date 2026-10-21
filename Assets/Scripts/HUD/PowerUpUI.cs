using System.Collections; // NUEVO
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PowerUpUI : MonoBehaviour
{
    public static PowerUpUI Instance { get; private set; }

    [Header("Referencias Visuales")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image effectIcon;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Íconos de Efectos")]
    [SerializeField] private Sprite expansorSprite;
    [SerializeField] private Sprite bombaExtraSprite;
    [SerializeField] private Sprite botasSprite;
    [SerializeField] private Sprite guanteSprite;
    [SerializeField] private Sprite escudoSprite;

    // --- NUEVO ---
    [Header("Animación de recogida")]
    [SerializeField] private Canvas canvas;                 // El canvas que contiene effectIcon
    [SerializeField] private string flySortingLayer = "Default";
    [SerializeField] private int flySortingOrder = 1000;
    [SerializeField] private float popDuration = 0.2f;      // Saltito inicial
    [SerializeField] private float popHeight = 0.6f;        // Cuánto sube en pantalla (unidades de mundo)
    [SerializeField] private float flyDuration = 0.55f;     // Viaje hacia la UI
    [SerializeField] private float startScale = 1f;
    [SerializeField] private float endScale = 0.35f;

    private float currentTimer;
    private bool isTimerActive;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        DeactivateEffect();
    }

    private void Update()
    {
        if (isTimerActive)
        {
            currentTimer -= Time.deltaTime;

            if (currentTimer > 0)
                timerText.text = Mathf.CeilToInt(currentTimer).ToString() + "s";
            else
                timerText.text = "0s";
        }
    }

    public void ActivateEffect(PowerUpType type, float duration, Vector3 worldPos)
    {
        effectIcon.sprite = GetSpriteForType(type);
        currentTimer = duration;
        isTimerActive = true;
        panelRoot.SetActive(true);
        PlayPickupFlight(type, worldPos);
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

    // =====================  NUEVO: ANIMACIÓN DE VIAJE  =====================

    /// <summary>
    /// Lanza un sprite que "viaja" desde worldStartPos hasta el ícono de la UI.
    /// Es puramente visual: no toca la lógica del power-up.
    /// </summary>
    public void PlayPickupFlight(PowerUpType type, Vector3 worldStartPos)
    {
        Sprite sprite = GetSpriteForType(type);
        if (sprite == null || Camera.main == null) return;

        StartCoroutine(FlyRoutine(sprite, worldStartPos));
        Debug.Log("Se inicio la animacion");
    }

    private IEnumerator FlyRoutine(Sprite sprite, Vector3 startPos)
    {
        Camera cam = Camera.main;

        Debug.Log("Iniciando animacion aca");

        // Objeto temporal con el sprite del power-up
        GameObject go = new GameObject("PowerUpFlight");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = flySortingLayer;
        sr.sortingOrder = flySortingOrder;

        Transform tr = go.transform;
        tr.position = startPos;
        tr.rotation = cam.transform.rotation; // Siempre mirando a la cámara (sirve para 2D y 3D top-down)
        tr.localScale = Vector3.zero;

        // ---- Fase 1: "pop" (aparece con rebote y sube un poco hacia arriba de la pantalla) ----
        Vector3 popPos = startPos + cam.transform.up * popHeight;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / popDuration;
            float k = Mathf.Clamp01(t);
            tr.position = Vector3.Lerp(startPos, popPos, EaseOutCubic(k));
            tr.localScale = Vector3.one * startScale * EaseOutBack(k);
            tr.rotation = cam.transform.rotation;
            yield return null;
        }

        // ---- Fase 2: viaje hacia el ícono de la UI ----
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / flyDuration;
            float k = Mathf.Clamp01(t);
            float e = k * k; // ease-in: arranca lento y acelera

            // Recalculamos el destino cada frame porque la cámara se mueve
            float depth = Vector3.Dot(popPos - cam.transform.position, cam.transform.forward);
            Vector3 target = GetIconWorldPosition(cam, depth);

            tr.position = Vector3.Lerp(popPos, target, e);
            tr.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, e);
            tr.rotation = cam.transform.rotation;
            yield return null;
        }

        Destroy(go);
        StartCoroutine(PunchIcon());
        Debug.Log("Se termino la animacion");
    }

    // Convierte la posición del ícono de la UI a una posición en el mundo, a cierta profundidad de la cámara
    private Vector3 GetIconWorldPosition(Camera cam, float depth)
    {
        RectTransform rt = effectIcon.rectTransform;
        Vector3 screenPos;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            screenPos = rt.position; // En Overlay, position ya está en píxeles de pantalla
        }
        else
        {
            Camera uiCam = canvas.worldCamera != null ? canvas.worldCamera : cam;
            screenPos = RectTransformUtility.WorldToScreenPoint(uiCam, rt.position);
        }

        screenPos.z = depth;
        return cam.ScreenToWorldPoint(screenPos);
    }

    // Pequeño "golpecito" del ícono al recibir el power-up
    private IEnumerator PunchIcon()
    {
        Transform icon = effectIcon.transform;
        float duration = 0.18f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float s = 1f + 0.3f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            icon.localScale = Vector3.one * s;
            yield return null;
        }
        icon.localScale = Vector3.one;
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}