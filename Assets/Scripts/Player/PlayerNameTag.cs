using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNameTag : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameLabel;
    [Tooltip("Oculta tu propio nombre cuando pasás a primera persona")]
    [SerializeField] private bool hideOwnTagAfterSwap = true;
    [Header("Colores de equipo (solo modo 2 vs 2)")]
    [SerializeField] private Color colorAlfa = new Color(0.25f, 0.6f, 1f);   // azul
    [SerializeField] private Color colorBeta = new Color(1f, 0.55f, 0.15f);  // naranja

private Color colorOriginal = Color.white;
private int ultimoEquipoAplicado = -2;

    // Lo escribe el dueño y lo leen todos (también quien entra tarde)
    private readonly NetworkVariable<FixedString64Bytes> playerName = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private EasterEggManager swap;

    private void Awake()
{
    swap = GetComponent<EasterEggManager>();

    if (nameLabel != null)
        colorOriginal = nameLabel.color;
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

    // En el modo 2 vs 2 el nombre se pinta del color de su equipo; en los demás mapas queda como siempre.
private void ActualizarColorEquipo()
{
    if (nameLabel == null) return;

    int equipo = -1;
    MatchManager mm = MatchManager.Instance;

    if (mm != null && mm.ModoEquipos)
        equipo = mm.GetEquipo(OwnerClientId);

    if (equipo == ultimoEquipoAplicado) return;
    ultimoEquipoAplicado = equipo;

    nameLabel.color =
        equipo == 0 ? colorAlfa :
        equipo == 1 ? colorBeta :
        colorOriginal;
}

    // El texto mira siempre a la cámara, aunque el jugador gire
    private void LateUpdate()
    {
        if (nameLabel == null) return;

        ActualizarColorEquipo();

        bool hide = hideOwnTagAfterSwap && IsOwner && swap != null && swap.IsSwapped;
        if (nameLabel.gameObject.activeSelf == hide) nameLabel.gameObject.SetActive(!hide);
        if (hide) return;

        Camera cam = Camera.main;
        if (cam != null) nameLabel.transform.rotation = cam.transform.rotation;
    }
}