using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

// Modo espectador: cuando el jugador local muere, la cámara pasa a seguir
// a un compañero vivo. Con Q/E se cambia de compañero.
[RequireComponent(typeof(PlayerStateManager))]
public class PlayerSpectator : NetworkBehaviour
{
    [Header("Controles")]
    [SerializeField] private KeyCode keyPrevious = KeyCode.Q;
    [SerializeField] private KeyCode keyNext = KeyCode.E;

    private PlayerStateManager stateManager;
    private PlayerStateManager target;
    private bool spectating;
    private float nextSearchTime;

    private readonly List<PlayerStateManager> candidates = new List<PlayerStateManager>();

    private void Awake()
    {
        stateManager = GetComponent<PlayerStateManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        stateManager.currentState.OnValueChanged += OnStateChanged;

        // Por si el jugador aparece ya muerto
        if (stateManager.currentState.Value == PlayerState.Muerto)
            StartSpectating();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (stateManager != null)
            stateManager.currentState.OnValueChanged -= OnStateChanged;

        spectating = false;
        target = null;
    }

    private void OnStateChanged(PlayerState previous, PlayerState current)
    {
        if (current == PlayerState.Muerto) StartSpectating();
        else if (spectating) StopSpectating();
    }

    private void StartSpectating()
    {
        spectating = true;
        target = null;
        nextSearchTime = 0f;
        ChangeTarget(1);
    }

    private void StopSpectating()
    {
        spectating = false;
        target = null;
        SetCameraTarget(transform);
    }

    private void Update()
    {
        if (!IsOwner || !spectating) return;
        if (PauseManager.IsGamePaused) return;

        bool targetInvalid = target == null ||
                             target.currentState.Value == PlayerState.Muerto;

        // Si el compañero murió o se fue, buscamos otro (sin buscar en cada frame)
        if (targetInvalid)
        {
            if (Time.time >= nextSearchTime)
            {
                nextSearchTime = Time.time + 0.5f;
                ChangeTarget(1);
            }
            return;
        }

        if (Input.GetKeyDown(keyNext)) ChangeTarget(1);
        else if (Input.GetKeyDown(keyPrevious)) ChangeTarget(-1);
    }

    private void ChangeTarget(int direction)
    {
        candidates.Clear();

        foreach (PlayerStateManager p in FindObjectsByType<PlayerStateManager>(FindObjectsSortMode.None))
        {
            if (p != null && p != stateManager && p.currentState.Value != PlayerState.Muerto)
                candidates.Add(p);
        }

        // Nadie vivo: la cámara queda en el propio jugador
        if (candidates.Count == 0)
        {
            target = null;
            SetCameraTarget(transform);
            return;
        }

        // Orden estable para que Q/E recorran siempre en el mismo orden
        candidates.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        int index = candidates.IndexOf(target); // -1 si el actual ya no es válido

        int next = index < 0
            ? (direction > 0 ? 0 : candidates.Count - 1)
            : (index + direction + candidates.Count) % candidates.Count;

        target = candidates[next];
        SetCameraTarget(target.transform);
        Debug.Log("[Espectador] Siguiendo a " + target.name);
    }

    private void SetCameraTarget(Transform t)
    {
        CinemachineCamera vcam = CinemachineCameraRegistrar.Instance;
        if (vcam == null) return;

        vcam.Target.TrackingTarget = t;
        vcam.Follow = t;
    }
}