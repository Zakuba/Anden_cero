using UnityEngine;
using UnityEngine.UI;

public class DifficultySelector : MonoBehaviour
{
    [Header("Botones (uno por dificultad)")]
    [SerializeField] private Button easyButton;
    [SerializeField] private Button normalButton;
    [SerializeField] private Button hardButton;

    [Header("Aspecto del botón elegido")]
    [Tooltip("Color del gráfico del botón elegido. Con blanco no se tiñe.")]
    [SerializeField] private Color selectedColor = Color.white;
    [Tooltip("Color de los botones no elegidos.")]
    [SerializeField] private Color unselectedColor = new Color(1f, 1f, 1f, 0.6f);
    [Tooltip("Escala del botón elegido (1 = sin cambio).")]
    [SerializeField] private float selectedScale = 1.1f;

    private void Awake()
    {
        if (easyButton != null) easyButton.onClick.AddListener(OnEasyClicked);
        if (normalButton != null) normalButton.onClick.AddListener(OnNormalClicked);
        if (hardButton != null) hardButton.onClick.AddListener(OnHardClicked);
    }

    private void OnDestroy()
    {
        if (easyButton != null) easyButton.onClick.RemoveListener(OnEasyClicked);
        if (normalButton != null) normalButton.onClick.RemoveListener(OnNormalClicked);
        if (hardButton != null) hardButton.onClick.RemoveListener(OnHardClicked);
    }

    private void OnEnable()
    {
        // Lo que se ve marcado es lo que se va a usar: la elección anterior o Normal.
        DifficultyLevel current = MatchSettings.HasDifficulty
            ? MatchSettings.Difficulty
            : DifficultyLevel.Normal;

        Select(current);
    }

    private void OnEasyClicked() { Select(DifficultyLevel.Easy); }
    private void OnNormalClicked() { Select(DifficultyLevel.Normal); }
    private void OnHardClicked() { Select(DifficultyLevel.Hard); }

    private void Select(DifficultyLevel level)
    {
        MatchSettings.SetDifficulty(level);

        Paint(easyButton, level == DifficultyLevel.Easy);
        Paint(normalButton, level == DifficultyLevel.Normal);
        Paint(hardButton, level == DifficultyLevel.Hard);
    }

    private void Paint(Button button, bool selected)
    {
        if (button == null) return;

        if (button.targetGraphic != null)
            button.targetGraphic.color = selected ? selectedColor : unselectedColor;

        button.transform.localScale = Vector3.one * (selected ? selectedScale : 1f);
    }
}