using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class EasterEggManager : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode teleportKey = KeyCode.T;

    [Header("Destino")]
    [Tooltip("Tag de un objeto de la escena que marca el punto de destino")]
    [SerializeField] private string teleportPointTag = "TeleportPoint";

    [Header("Objetos hijos del prefab")]
    [SerializeField] private GameObject[] objectsToDeactivate;
    [SerializeField] private GameObject[] objectsToActivate;

    [Header("Cámaras")]
    [SerializeField] private Camera playerCamera; // la cámara hija del prefab

    [SerializeField] private TypewriterText swapTypewriter;

    // Estado sincronizado: lo escribe el servidor, lo leen todos (incluso quien entra tarde)
    private readonly NetworkVariable<bool> swapped = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

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
        ApplyVisuals(swapped.Value); // para quien se conecta con el estado ya cambiado

        // La cámara del player arranca apagada en todos los clientes
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
            AudioListener al = playerCamera.GetComponent<AudioListener>();
            if (al != null) al.enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        swapped.OnValueChanged -= OnSwappedChanged;
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(teleportKey))
        {
            GameObject point = GameObject.FindWithTag(teleportPointTag);
            if (point == null)
            {
                Debug.LogWarning($"No hay ningún objeto con el tag '{teleportPointTag}' en la escena.");
                return;
            }

            Vector3 target = point.transform.position;

            // Cliente con autoridad (ClientNetworkTransform): teletransporta él mismo.
            // NetworkTransform server-authoritative: se lo pide al servidor.
            if (netTransform == null || netTransform.CanCommitToTransform)
                DoTeleport(target);
            else
                TeleportServerRpc(target);

            // El cambio de cámara ocurre una sola vez
            if (!cameraSwitched)
            {
                cameraSwitched = true;
                if (MapCameraMover.Instance != null && playerCamera != null)
                    MapCameraMover.Instance.SwitchTo(playerCamera, ActivatePlayerCamera);
                else
                    ActivatePlayerCamera();
            }

            ToggleSwapServerRpc();
        }
    }

    // ---------- Teletransporte ----------

    private void DoTeleport(Vector3 position)
    {
        if (cc != null) cc.enabled = false; // el CharacterController pisa el transform si está activo

        if (netTransform != null)
            netTransform.Teleport(position, transform.rotation, transform.localScale);
        else
            transform.position = position;

        if (cc != null) cc.enabled = true;

        if (rb != null && !rb.isKinematic)
        {
            rb.position = position;
            rb.linearVelocity = Vector3.zero; // en Unity 2022 o anterior: rb.velocity
        }
    }

    [ServerRpc] // RequireOwnership = true por defecto: solo el dueño puede llamarlo
    private void TeleportServerRpc(Vector3 position)
    {
        DoTeleport(position);
    }

    // ---------- Cámara ----------

    private void ActivatePlayerCamera()
    {
        if (playerCamera == null)
        {
            Debug.LogError("PlayerTeleportSwap: 'Player Camera' no está asignada en el prefab.");
            return;
        }

        playerCamera.gameObject.SetActive(true); // por si algún padre quedó apagado
        playerCamera.enabled = true;

        AudioListener al = playerCamera.GetComponent<AudioListener>();
        if (al != null) al.enabled = true;

        if (swapTypewriter != null) swapTypewriter.Play();
    }

    // ---------- Cambio de objetos ----------

    [ServerRpc]
    private void ToggleSwapServerRpc()
    {
        swapped.Value = true; // un solo sentido
    }

    private void OnSwappedChanged(bool oldValue, bool newValue)
    {
        ApplyVisuals(newValue);
    }

    private void ApplyVisuals(bool isSwapped)
    {
        // Antes del swap no se toca nada: los objetos quedan como están en el prefab
        if (!isSwapped) return;

        foreach (GameObject obj in objectsToDeactivate)
            if (obj != null) obj.SetActive(false);

        foreach (GameObject obj in objectsToActivate)
            if (obj != null) obj.SetActive(true);
    }
}