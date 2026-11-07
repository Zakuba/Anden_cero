using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Botón de la lista de secciones. Se usa un Toggle (dentro de un ToggleGroup) para que el
/// estado "seleccionado" y la exclusividad entre botones los maneje Unity.
/// Va en el prefab junto al componente Toggle.
/// </summary>
[RequireComponent(typeof(Toggle))]
public class TutorialSectionButton : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private Toggle toggle;
    private int index;
    private Action<int> onSelected;

    public void Setup(string text, int sectionIndex, ToggleGroup group, Action<int> selectedCallback)
    {
        toggle = GetComponent<Toggle>();
        index = sectionIndex;
        onSelected = selectedCallback;

        if (label != null) label.text = text;

        toggle.group = group;
        toggle.SetIsOnWithoutNotify(false);
        toggle.onValueChanged.RemoveListener(OnToggleChanged);
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    /// <summary>Marca el botón como seleccionado sin disparar el callback (el ToggleGroup apaga los demás).</summary>
    public void SetSelectedWithoutNotify(bool selected)
    {
        if (toggle != null) toggle.SetIsOnWithoutNotify(selected);
    }

    private void OnToggleChanged(bool isOn)
    {
        if (isOn) onSelected?.Invoke(index);
    }

    private void OnDestroy()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }
}
