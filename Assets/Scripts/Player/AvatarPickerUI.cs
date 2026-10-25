using SFB;
using UnityEngine;
using UnityEngine.UI;

public class AvatarPickerUI : MonoBehaviour
{
    [SerializeField] Button elegirButton;
    [SerializeField] RawImage preview;

    Texture2D previewTex;   // la textura que creamos nosotros (para liberarla)

    void Start()
    {
        elegirButton.onClick.AddListener(Elegir);
        MostrarGuardada();                       // al abrir el menú, mostrar la ya guardada
    }

    void Elegir()
    {
        var filtros = new[] { new ExtensionFilter("Imágenes", "png", "jpg", "jpeg") };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Elegí tu imagen", "", filtros, false);

        if (paths == null || paths.Length == 0 || string.IsNullOrEmpty(paths[0])) return;   // canceló

        if (AvatarStorage.SetFromFile(paths[0]))
            MostrarGuardada();
        else
            Debug.LogWarning("No se pudo leer esa imagen.");
    }

    void MostrarGuardada()
    {
        byte[] png = AvatarStorage.LoadPng();
        if (png == null || png.Length == 0) return;

        if (previewTex != null) Destroy(previewTex);
        previewTex = new Texture2D(2, 2);
        previewTex.LoadImage(png);
        preview.texture = previewTex;
    }

    void OnDestroy()
    {
        if (previewTex != null) Destroy(previewTex);
    }
}