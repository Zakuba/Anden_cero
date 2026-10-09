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

    // Animación del lobby sincronizada por red.
    private NetworkVariable<int> lobbyAnimationIndex =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private PlayerAnimationController animationController;

    private void Awake()
    {
        animationController = GetComponent<PlayerAnimationController>();

        if (animationController == null)
        {
            Debug.LogError(
                "[PlayerCharacterVisual] No se encontró PlayerAnimationController."
            );
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        selectedCharacter.OnValueChanged += OnCharacterChanged;
        lobbyAnimationIndex.OnValueChanged += OnLobbyAnimationChanged;

        if (IsOwner)
        {
            int selectedIndex = PlayerSelectionData.selectedCharacterIndex;
            SeleccionarPersonajeServerRpc(selectedIndex);
        }

        ActualizarPersonaje(selectedCharacter.Value);
        AplicarAnimacionLobby();
    }

    public override void OnNetworkDespawn()
    {
        selectedCharacter.OnValueChanged -= OnCharacterChanged;
        lobbyAnimationIndex.OnValueChanged -= OnLobbyAnimationChanged;

        base.OnNetworkDespawn();
    }

    private void OnCharacterChanged(int previousValue, int newValue)
    {
        ActualizarPersonaje(newValue);
        AplicarAnimacionLobby();
    }

    private void OnLobbyAnimationChanged(int previousValue, int newValue)
    {
        AplicarAnimacionLobby();
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
                "[PlayerCharacterVisual] No hay modelos configurados."
            );
            return;
        }

        if (index < 0 || index >= characterModels.Length)
            index = 0;

        for (int i = 0; i < characterModels.Length; i++)
        {
            if (characterModels[i] != null)
                characterModels[i].SetActive(i == index);
        }

        GameObject personajeActivo = characterModels[index];

        if (personajeActivo == null)
        {
            Debug.LogError(
                $"[PlayerCharacterVisual] El personaje {index} es NULL."
            );
            return;
        }

        Animator animator =
            personajeActivo.GetComponentInChildren<Animator>(true);

        if (animator == null)
        {
            Debug.LogError(
                $"[PlayerCharacterVisual] El personaje {index} no tiene Animator."
            );
            return;
        }

        if (animationController != null)
            animationController.SetAnimator(animator);
    }

    public int GetSelectedCharacter()
    {
        return selectedCharacter.Value;
    }

    // Este método lo llama MatchManager en el servidor.
    public void SetLobbyAnimation(int animationIndex)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "[PlayerCharacterVisual] Solo el servidor puede asignar la animación."
            );
            return;
        }

        lobbyAnimationIndex.Value = animationIndex;

        // Aplicación inmediata en el servidor.
        AplicarAnimacionLobby();
    }

    public void ResetLobbyAnimation()
    {
        SetLobbyAnimation(0);
    }

    private void AplicarAnimacionLobby()
    {
        if (characterModels == null)
            return;

        int index = selectedCharacter.Value;

        if (index < 0 || index >= characterModels.Length)
            return;

        GameObject personajeActivo = characterModels[index];

        if (personajeActivo == null || !personajeActivo.activeInHierarchy)
            return;

        Animator animator =
            personajeActivo.GetComponentInChildren<Animator>(true);

        if (animator == null)
            return;

        if (!animator.gameObject.activeInHierarchy)
            return;

        if (animator.runtimeAnimatorController == null)
            return;

        if (animator.isInitialized)
        {
            animator.SetInteger(
                "LobbyAnimation",
                lobbyAnimationIndex.Value
            );
        }
        else
        {
            animator.Rebind();
            animator.SetInteger(
                "LobbyAnimation",
                lobbyAnimationIndex.Value
            );
        }
    }
}