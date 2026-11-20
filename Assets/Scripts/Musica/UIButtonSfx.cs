using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UiButtonSfx : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private SfxId clickSfx = SfxId.UiClick;
    [SerializeField] private bool playHover = true;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(clickSfx);
        });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (playHover && button.interactable && AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(SfxId.UiHover);
    }
}