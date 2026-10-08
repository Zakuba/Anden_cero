using UnityEngine;
using Unity.Netcode;

public class PlayerCharacterVisual : NetworkBehaviour
{
    [Header("Modelos de personajes")]
    [SerializeField] private GameObject[] characterModels;

    private NetworkVariable<int> selectedCharacter =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private PlayerAnimationController animationController;

    private void Awake()
    {
        animationController =
            GetComponent<PlayerAnimationController>();

        if (animationController == null)
        {
            Debug.LogError(
                "[PlayerCharacterVisual] " +
                "No se encontró PlayerAnimationController en el Player."
            );
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        selectedCharacter.OnValueChanged += OnCharacterChanged;

        // Si este es MI Player, envío al servidor
        // el personaje que elegí en el Lobby.
        if (IsOwner)
        {
            int selectedIndex =
                PlayerSelectionData.selectedCharacterIndex;

            SeleccionarPersonajeServerRpc(selectedIndex);
        }

        // Mostrar inicialmente el personaje actual.
        ActualizarPersonaje(selectedCharacter.Value);
    }

    public override void OnNetworkDespawn()
    {
        selectedCharacter.OnValueChanged -= OnCharacterChanged;
    }

    private void OnCharacterChanged(
        int previousValue,
        int newValue)
    {
        ActualizarPersonaje(newValue);
    }

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Owner
    )]
    private void SeleccionarPersonajeServerRpc(int index)
    {
        if (characterModels == null ||
            index < 0 ||
            index >= characterModels.Length)
        {
            Debug.LogWarning(
                $"[PlayerCharacterVisual] Índice inválido: {index}"
            );

            return;
        }

        selectedCharacter.Value = index;
    }

    private void ActualizarPersonaje(int index)
    {
        if (characterModels == null ||
            characterModels.Length == 0)
        {
            Debug.LogError(
                "[PlayerCharacterVisual] " +
                "No hay modelos configurados."
            );

            return;
        }

        if (index < 0 || index >= characterModels.Length)
        {
            index = 0;
        }

        // Desactivar todos los personajes
        // y activar solamente el seleccionado.
        for (int i = 0; i < characterModels.Length; i++)
        {
            if (characterModels[i] != null)
            {
                characterModels[i].SetActive(i == index);
            }
        }

        // Obtener el personaje que acabamos de activar.
        GameObject personajeActivo =
            characterModels[index];

        if (personajeActivo == null)
        {
            Debug.LogError(
                $"[PlayerCharacterVisual] " +
                $"El personaje {index} es NULL."
            );

            return;
        }

        // Buscar el Animator dentro del personaje.
        Animator animator =
            personajeActivo.GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError(
                $"[PlayerCharacterVisual] " +
                $"El personaje {index} no tiene Animator."
            );

            return;
        }

        // Pasarle el Animator al controlador
        // que está en el Player.
        if (animationController != null)
        {
            animationController.SetAnimator(animator);
        }
    }

    public int GetSelectedCharacter()
    {
        return selectedCharacter.Value;
    }

    public void SetLobbyAnimation(int animationIndex)
    {
        Animator animatorActivo = null;

        Animator[] animators = GetComponentsInChildren<Animator>(true);

        foreach (Animator animator in animators)
        {
            if (animator.gameObject.activeInHierarchy)
            {
                animatorActivo = animator;
                break;
            }
        }

        if (animatorActivo == null)
            return;

        animatorActivo.SetInteger("LobbyAnimation", animationIndex);
    }

    public void ResetLobbyAnimation()
    {
        SetLobbyAnimation(0);
    }
}
