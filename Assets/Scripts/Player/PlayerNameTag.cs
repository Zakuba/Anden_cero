using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNameTag : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameLabel;
    [Tooltip("Oculta tu propio nombre cuando pasás a primera persona")]
    [SerializeField] private bool hideOwnTagAfterSwap = true;

    // Lo escribe el dueño y lo leen todos (también quien entra tarde)
    private readonly NetworkVariable<FixedString64Bytes> playerName = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private EasterEggManager swap;

    private void Awake()
    {
        swap = GetComponent<EasterEggManager>();
    }

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += OnNameChanged;

        if (IsOwner)
        {
            string saved = PersistenceManager.LoadData().nombreJugador;
            if (string.IsNullOrWhiteSpace(saved)) saved = $"Jugador {OwnerClientId}";
            playerName.Value = saved.Trim();
        }

        Refresh(playerName.Value);
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
    }

    private void OnNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        Refresh(newValue);
    }

    private void Refresh(FixedString64Bytes value)
    {
        if (nameLabel != null) nameLabel.text = value.ToString();
    }

    // El texto mira siempre a la cámara, aunque el jugador gire
    private void LateUpdate()
    {
        if (nameLabel == null) return;

        bool hide = hideOwnTagAfterSwap && IsOwner && swap != null && swap.IsSwapped;
        if (nameLabel.gameObject.activeSelf == hide) nameLabel.gameObject.SetActive(!hide);
        if (hide) return;

        Camera cam = Camera.main;
        if (cam != null) nameLabel.transform.rotation = cam.transform.rotation;
    }
}