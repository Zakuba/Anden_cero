using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Datos de una sección del tutorial: texto del botón, título, descripción y video en loop.
/// Para agregar una sección nueva: Create > Anden Cero > Tutorial > Seccion
/// y sumarla a la lista del TutorialMenuController.
/// </summary>
[CreateAssetMenu(fileName = "TutorialSection_", menuName = "Anden Cero/Tutorial/Seccion")]
public class TutorialSectionSO : ScriptableObject
{
    [Tooltip("Texto del botón en la lista de la izquierda. Si está vacío se usa el título.")]
    [SerializeField] private string buttonLabel;

    [SerializeField] private string title;

    [TextArea(3, 8)]
    [Tooltip("Admite rich text de TextMeshPro (<b>, <color>, <sprite>). Conviene mantenerlo en 3-4 líneas.")]
    [SerializeField] private string description;

    [Tooltip("Clip que se reproduce en loop, sin audio. Si está vacío se muestra el placeholder.")]
    [SerializeField] private VideoClip clip;

    public string ButtonLabel => string.IsNullOrWhiteSpace(buttonLabel) ? title : buttonLabel;
    public string Title => title;
    public string Description => description;
    public VideoClip Clip => clip;
}
