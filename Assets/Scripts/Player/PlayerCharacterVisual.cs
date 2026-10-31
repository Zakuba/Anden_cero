
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

    private NetworkVariable<int> lobbyAnimationIndex =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private PlayerAnimationController animationController;

    // Copia visual que permanece en el podio.
    private GameObject podiumVisualCopy;

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
            int selectedIndex =
                PlayerSelectionData.selectedCharacterIndex;

            SeleccionarPersonajeServerRpc(selectedIndex);
        }

        ActualizarPersonaje(selectedCharacter.Value);
        AplicarAnimacionLobby();
    }

    public override void OnNetworkDespawn()
    {
        selectedCharacter.OnValueChanged -= OnCharacterChanged;
        lobbyAnimationIndex.OnValueChanged -= OnLobbyAnimationChanged;

        if (podiumVisualCopy != null)
            Destroy(podiumVisualCopy);

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

        if (personajeActivo == null ||
            !personajeActivo.activeInHierarchy)
            return;

        Animator animator =
            personajeActivo.GetComponentInChildren<Animator>(true);

        if (animator == null ||
            !animator.gameObject.activeInHierarchy)
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

    // ---------- COPIA VISUAL DEL PODIO ----------

    public void CreatePodiumVisualCopy()
    {
        // Evita duplicar el modelo si el método se llama más de una vez.
        if (podiumVisualCopy != null)
            return;

        if (characterModels == null ||
            characterModels.Length == 0)
        {
            Debug.LogWarning(
                "[PlayerCharacterVisual] No hay modelos para copiar."
            );
            return;
        }

        int selectedIndex = selectedCharacter.Value;

        if (selectedIndex < 0 ||
            selectedIndex >= characterModels.Length)
        {
            Debug.LogWarning(
                "[PlayerCharacterVisual] Índice de personaje inválido."
            );
            return;
        }

        GameObject selectedModel = characterModels[selectedIndex];

        if (selectedModel == null)
            return;

        // Todos los modelos deben estar dentro del mismo objeto padre.
        Transform modelsRoot = selectedModel.transform.parent;

        if (modelsRoot == null)
        {
            Debug.LogWarning(
                "[PlayerCharacterVisual] El modelo no tiene un contenedor padre."
            );
            return;
        }

        // Copiamos solamente el contenedor visual, no el Player completo.
        podiumVisualCopy = Instantiate(
            modelsRoot.gameObject,
            modelsRoot.position,
            modelsRoot.rotation
        );

        podiumVisualCopy.name =
            "PodiumVisual_" + OwnerClientId;

        // Conservamos la escala del contenedor original.
        podiumVisualCopy.transform.localScale =
            modelsRoot.lossyScale;

        // Activamos exclusivamente el personaje seleccionado.
        for (int i = 0; i < characterModels.Length; i++)
        {
            GameObject originalModel = characterModels[i];

            if (originalModel == null)
                continue;

            int siblingIndex =
                originalModel.transform.GetSiblingIndex();

            if (siblingIndex < 0 ||
                siblingIndex >= podiumVisualCopy.transform.childCount)
                continue;

            Transform copiedModel =
                podiumVisualCopy.transform.GetChild(siblingIndex);

            copiedModel.gameObject.SetActive(i == selectedIndex);
        }

        // Aplicamos a la copia la animación que tenía asignada el jugador.
        Transform copiedSelectedModel =
            podiumVisualCopy.transform.GetChild(
                selectedModel.transform.GetSiblingIndex()
            );

        Animator copiedAnimator =
            copiedSelectedModel.GetComponentInChildren<Animator>(true);

        if (copiedAnimator != null)
        {
            copiedAnimator.SetInteger(
                "LobbyAnimation",
                lobbyAnimationIndex.Value
            );
        }

        Debug.Log(
            $"[PlayerCharacterVisual] Copia visual del personaje {selectedIndex} creada en el podio."
        );
    }
}
