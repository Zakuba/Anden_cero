using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;


public class TutorialMenuController : MonoBehaviour
{
    [Header("Contenido")]
    [SerializeField] private List<TutorialSectionSO> sections = new List<TutorialSectionSO>();

    [Header("Lista de secciones (izquierda)")]
    [Tooltip("Content del ScrollRect. Debe empezar vacío.")]
    [SerializeField] private RectTransform listContainer;
    [SerializeField] private TutorialSectionButton sectionButtonPrefab;
    [Tooltip("Si queda vacío se busca en el listContainer.")]
    [SerializeField] private ToggleGroup toggleGroup;

    [Header("Detalle (derecha)")]
    [SerializeField] private VideoPlayer videoPlayer;
    [Tooltip("RenderTexture compartida por el VideoPlayer y el RawImage.")]
    [SerializeField] private RenderTexture videoRenderTexture;
    [SerializeField] private RawImage videoImage;
    [Tooltip("Opcional: fondo que se ve mientras carga el video o si la sección no tiene clip.")]
    [SerializeField] private GameObject videoPlaceholder;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Cierre")]
    [SerializeField] private bool closeWithEscape = true;
    [Tooltip("Se invoca con el botón Volver y con ESC. Configurar acá: apagar este panel y prender el menú principal.")]
    [SerializeField] private UnityEvent onCloseRequested;

    private readonly List<TutorialSectionSO> activeSections = new List<TutorialSectionSO>();
    private readonly List<TutorialSectionButton> buttons = new List<TutorialSectionButton>();
    private int currentIndex = -1;
    private bool listBuilt;

    // ---------------------------------------------------------------- Ciclo de vida

    private void Awake()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        if (toggleGroup == null) toggleGroup = listContainer.GetComponent<ToggleGroup>();
        if (toggleGroup != null) toggleGroup.allowSwitchOff = false;

        ConfigureVideoPlayer();
    }

    private void OnEnable()
    {
        videoPlayer.errorReceived += OnVideoError;

        BuildListIfNeeded();

        if (activeSections.Count > 0) Select(0);
        else ClearDetail();
    }

    private void OnDisable()
    {
        currentIndex = -1;

        if (videoPlayer != null)
        {
            videoPlayer.errorReceived -= OnVideoError;
            StopVideo();
        }
    }

    private void Update()
    {
        if (closeWithEscape && EscapePressed()) RequestClose();
    }

    // ---------------------------------------------------------------- API pública

    /// <summary>Para el botón Volver (OnClick) y para ESC.</summary>
    public void RequestClose()
    {
        onCloseRequested?.Invoke();
    }

    /// <summary>Muestra la sección indicada: actualiza título, descripción, botón resaltado y video.</summary>
    public void Select(int index)
    {
        if (index < 0 || index >= activeSections.Count) return;

        currentIndex = index;
        TutorialSectionSO section = activeSections[index];

        buttons[index].SetSelectedWithoutNotify(true);
        titleText.text = section.Title;
        descriptionText.text = section.Description;

        PlayClip(section.Clip);
    }

    // ---------------------------------------------------------------- Lista

    private void BuildListIfNeeded()
    {
        if (listBuilt) return;
        listBuilt = true;

        for (int i = listContainer.childCount - 1; i >= 0; i--)
            Destroy(listContainer.GetChild(i).gameObject);

        activeSections.Clear();
        buttons.Clear();

        foreach (TutorialSectionSO section in sections)
        {
            if (section == null)
            {
                Debug.LogWarning("[Tutorial] Hay una sección vacía en la lista; se ignora.", this);
                continue;
            }

            int index = activeSections.Count;
            activeSections.Add(section);

            TutorialSectionButton button = Instantiate(sectionButtonPrefab, listContainer);
            button.Setup(section.ButtonLabel, index, toggleGroup, OnSectionClicked);
            buttons.Add(button);
        }
    }

    private void OnSectionClicked(int index)
    {
        // Evita reiniciar el video si se vuelve a tocar la sección que ya está abierta.
        if (index == currentIndex) return;
        Select(index);
    }

    // ---------------------------------------------------------------- Video

    private void ConfigureVideoPlayer()
    {
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // clips sin audio

        videoImage.texture = videoRenderTexture;
    }

    private void PlayClip(VideoClip clip)
    {
        StopVideo();
        ShowVideo(false); // se oculta hasta que llegue el primer frame del clip nuevo

        if (clip == null) return;

        videoPlayer.clip = clip;
        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.Prepare();
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
        source.prepareCompleted -= OnVideoPrepared;

        source.sendFrameReadyEvents = true;
        source.frameReady += OnFirstFrameReady;
        source.Play();
    }

    private void OnFirstFrameReady(VideoPlayer source, long frameIdx)
    {
        source.frameReady -= OnFirstFrameReady;
        source.sendFrameReadyEvents = false;

        ShowVideo(true);
    }

    private void StopVideo()
    {
        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.frameReady -= OnFirstFrameReady;
        videoPlayer.sendFrameReadyEvents = false;
        videoPlayer.Stop();
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"[Tutorial] Error al reproducir el video: {message}", this);
        ShowVideo(false);
    }

    private void ShowVideo(bool visible)
    {
        videoImage.enabled = visible;
        if (videoPlaceholder != null) videoPlaceholder.SetActive(!visible);
    }

    private void ClearDetail()
    {
        titleText.text = string.Empty;
        descriptionText.text = string.Empty;
        ShowVideo(false);
    }

    // ---------------------------------------------------------------- Utilidades

    private bool ValidateReferences()
    {
        bool ok = true;
        ok &= Require(listContainer, nameof(listContainer));
        ok &= Require(sectionButtonPrefab, nameof(sectionButtonPrefab));
        ok &= Require(videoPlayer, nameof(videoPlayer));
        ok &= Require(videoRenderTexture, nameof(videoRenderTexture));
        ok &= Require(videoImage, nameof(videoImage));
        ok &= Require(titleText, nameof(titleText));
        ok &= Require(descriptionText, nameof(descriptionText));
        return ok;
    }

    private bool Require(Object reference, string fieldName)
    {
        if (reference != null) return true;
        Debug.LogError($"[Tutorial] Falta asignar '{fieldName}' en {name}.", this);
        return false;
    }

    // Compatible con el Input Manager clásico y con el nuevo Input System.
    private static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
