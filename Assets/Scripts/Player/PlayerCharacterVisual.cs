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

        // Mostrar inicialmente el valor actual
        ActualizarPersonaje(selectedCharacter.Value);
    }

    public override void OnNetworkDespawn()
    {
        selectedCharacter.OnValueChanged -= OnCharacterChanged;
    }

    private void OnCharacterChanged(int previousValue, int newValue)
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
                "[PlayerCharacterVisual] No hay modelos configurados."
            );

            return;
        }

        if (index < 0 || index >= characterModels.Length)
        {
            index = 0;
        }

        for (int i = 0; i < characterModels.Length; i++)
        {
            if (characterModels[i] != null)
            {
                characterModels[i].SetActive(i == index);
            }
        }
    }

    public int GetSelectedCharacter()
    {
        return selectedCharacter.Value;
    }
}