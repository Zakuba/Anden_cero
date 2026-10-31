
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class EasterEggManager : NetworkBehaviour
{
    [Header("Destino")]
    [Tooltip("Tag de un objeto de la escena que marca el punto de destino")]
    [SerializeField] private string teleportPointTag = "TeleportPoint";

    [Header("Objetos hijos del prefab")]
    [SerializeField] private GameObject[] objectsToDeactivate;
    [SerializeField] private GameObject[] objectsToActivate;

    [Header("Cámaras")]
    [SerializeField] private Camera playerCamera;

    [SerializeField] private TypewriterText swapTypewriter;

    private readonly NetworkVariable<bool> swapped = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsSwapped => swapped.Value;
    public Camera PlayerCamera => playerCamera;

    private NetworkTransform netTransform;
    private Rigidbody rb;
    private CharacterController cc;
    private bool cameraSwitched;

    private void Awake()
    {
        netTransform = GetComponent<NetworkTransform>();
        rb = GetComponent<Rigidbody>();
        cc = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        swapped.OnValueChanged += OnSwappedChanged;

        ApplyVisuals(swapped.Value);

        if (playerCamera != null)
        {
            playerCamera.enabled = false;

            AudioListener al =
                playerCamera.GetComponent<AudioListener>();

            if (al != null)
                al.enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        swapped.OnValueChanged -= OnSwappedChanged;
    }

    // Lo llama el servidor cuando el jugador gana sin colocar bombas.
    public void TriggerFromServer()
    {
        if (!IsServer || swapped.Value)
            return;

        // Primero generamos la copia visual en todos los clientes.
        CreatePodiumVisualRpc();

        // Después activamos el estado del easter egg.
        swapped.Value = true;

        // Finalmente, trasladamos al jugador real.
        TriggerOwnerRpc();
    }

    // ---------- COPIA VISUAL DEL PODIO ----------

    [Rpc(SendTo.ClientsAndHost)]
    private void CreatePodiumVisualRpc()
    {
        PlayerCharacterVisual visual =
            GetComponent<PlayerCharacterVisual>();

        if (visual == null)
        {
            Debug.LogWarning(
                "[EasterEgg] No se encontró PlayerCharacterVisual."
            );
            return;
        }

        visual.CreatePodiumVisualCopy();
    }

    // ---------- TELETRANSPORTE DEL GANADOR ----------

    [Rpc(SendTo.Owner)]
    private void TriggerOwnerRpc()
    {
        GameObject point =
            GameObject.FindWithTag(teleportPointTag);

        if (point == null)
        {
            Debug.LogWarning(
                "[EasterEgg] No hay objeto con tag " + teleportPointTag
            );
            return;
        }

        Vector3 target = point.transform.position;

        if (netTransform == null || netTransform.CanCommitToTransform)
            DoTeleport(target);
        else
            TeleportServerRpc(target);

        if (!cameraSwitched)
        {
            cameraSwitched = true;

            if (MapCameraMover.Instance != null && playerCamera != null)
            {
                MapCameraMover.Instance.SwitchTo(
                    playerCamera,
                    ActivatePlayerCamera
                );
            }
            else
            {
                ActivatePlayerCamera();
            }
        }
    }

    // ---------- TELETRANSPORTE ----------

    private void DoTeleport(Vector3 position)
    {
        if (cc != null)
            cc.enabled = false;

        if (netTransform != null)
        {
            netTransform.Teleport(
                position,
                transform.rotation,
                transform.localScale
            );
        }
        else
        {
            transform.position = position;
        }

        if (cc != null)
            cc.enabled = true;

        if (rb != null && !rb.isKinematic)
        {
            rb.position = position;
            rb.linearVelocity = Vector3.zero;
        }
    }

    [ServerRpc]
    private void TeleportServerRpc(Vector3 position)
    {
        DoTeleport(position);
    }

    // ---------- CÁMARA ----------

    private void ActivatePlayerCamera()
    {
        if (playerCamera == null)
        {
            Debug.LogError(
                "PlayerTeleportSwap: 'Player Camera' no está asignada."
            );
            return;
        }

        playerCamera.gameObject.SetActive(true);
        playerCamera.enabled = true;

        AudioListener al =
            playerCamera.GetComponent<AudioListener>();

        if (al != null)
            al.enabled = true;

        if (swapTypewriter != null)
            swapTypewriter.Play();
    }

    // ---------- CAMBIO DE OBJETOS ----------

    private void OnSwappedChanged(bool oldValue, bool newValue)
    {
        ApplyVisuals(newValue);
    }

    private void ApplyVisuals(bool isSwapped)
    {
        if (!isSwapped)
            return;

        foreach (GameObject obj in objectsToDeactivate)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        foreach (GameObject obj in objectsToActivate)
        {
            if (obj != null)
                obj.SetActive(true);
        }
    }
}
