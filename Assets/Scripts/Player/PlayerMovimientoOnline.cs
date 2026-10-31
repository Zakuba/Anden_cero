using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

// Movimiento simple, SOLO para validar la sincronizacion de HU-01.3.
// El movimiento real en cuadricula es responsabilidad de HU-02.1.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovimientoOnline : NetworkBehaviour
{
    [Header("Configuración de Velocidad")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Configuración de Controles")]
    [SerializeField] private KeyCode keyUp = KeyCode.W;
    [SerializeField] private KeyCode keyDown = KeyCode.S;
    [SerializeField] private KeyCode keyLeft = KeyCode.A;
    [SerializeField] private KeyCode keyRight = KeyCode.D;

    [Header("Primera persona (después del swap)")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxPitch = 80f;
    [Tooltip("Multiplicador de velocidad después del swap. 0.5 = la mitad de rápido")]
    [SerializeField] private float swappedSpeedMultiplier = 0.5f;

    private CharacterController controller;
    private Vector3 movementInput;
    private float verticalVelocity = 0f;
    private const float Gravity = -9.81f;
    private PlayerStats playerStats;
    private PlayerStateManager stateManager;
    private EasterEggManager swap;
    private float pitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerStats = GetComponent<PlayerStats>();
        stateManager = GetComponent<PlayerStateManager>();
        swap = GetComponent<EasterEggManager>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            StartCoroutine(BindCameraWhenReady());
        }
    }

    private System.Collections.IEnumerator BindCameraWhenReady()
    {
        // Espera hasta que la cámara exista y esté inicializada
        float timeout = 5f;
        float elapsed = 0f;

        while (CinemachineCameraRegistrar.Instance == null && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (CinemachineCameraRegistrar.Instance != null)
        {
            CinemachineCamera vcam = CinemachineCameraRegistrar.Instance;

            // Asignación de target para Unity 6
            vcam.Target.TrackingTarget = transform;
            vcam.Follow = transform;

            Debug.Log($"[Cámara Build] Vinculada exitosamente al jugador {gameObject.name}");
        }
        else
        {
            Debug.LogError("[Cámara Build] Timeout: No se encontró CinemachineCameraRegistrar.");
        }
    }

    private void Update()
    {
        // 1. Si el juego está pausado, ignoramos todo input. En primera persona se libera el cursor.
        if (PauseManager.IsGamePaused)
        {
            if (IsOwner && swap != null && swap.IsSwapped && Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }

        // Antes de arrancar la partida, los drones no pueden chocar ni empujar al jugador.
        // Se aplica en todas las instancias, no solo en el dueño.
        bool matchRunning = MatchManager.Instance == null || MatchManager.Instance.IsMatchRunning;
        int enemigosMask = LayerMask.GetMask("Enemigos");
        controller.excludeLayers = matchRunning ? 0 : enemigosMask;

        if (!IsOwner) return;

        if (stateManager != null && stateManager.currentState.Value != PlayerState.Vivo) return;

        bool easterEggActivo = swap != null && swap.IsSwapped;

        if (matchRunning || easterEggActivo) GatherCustomInput();
        else movementInput = Vector3.zero;

        MoveAndRotate();
    }

    private void GatherCustomInput()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(keyRight)) horizontal += 1f;
        if (Input.GetKey(keyLeft)) horizontal -= 1f;
        if (Input.GetKey(keyUp)) vertical += 1f;
        if (Input.GetKey(keyDown)) vertical -= 1f;

        movementInput = new Vector3(horizontal, 0f, vertical).normalized;
    }

    private void MoveAndRotate()
    {

        if (controller == null || !controller.enabled || !gameObject.activeInHierarchy)
            return;

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += Gravity * Time.deltaTime;
        }

        bool firstPerson = swap != null && swap.IsSwapped;
        Vector3 moveDirection;

        if (firstPerson)
        {
            // Mouse: gira el cuerpo en horizontal y la cámara en vertical
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            transform.Rotate(0f, Input.GetAxis("Mouse X") * mouseSensitivity, 0f);

            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -maxPitch, maxPitch);
            if (swap.PlayerCamera != null)
                swap.PlayerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // W = adelante, S = atrás, A/D = costados, relativos a hacia dónde mira el jugador
            moveDirection = transform.right * movementInput.x + transform.forward * movementInput.z;
        }
        else
        {
            // Comportamiento anterior (ejes del mundo, el cuerpo se orienta hacia donde camina)
            moveDirection = new Vector3(movementInput.x, 0f, movementInput.z);

            if (moveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        float effectiveSpeed = moveSpeed * (playerStats != null ? playerStats.MoveSpeedMultiplier : 1f);
        if (firstPerson) effectiveSpeed *= swappedSpeedMultiplier;

        Vector3 finalVelocity = (moveDirection * effectiveSpeed) + (Vector3.up * verticalVelocity);
        controller.Move(finalVelocity * Time.deltaTime);
    }
}