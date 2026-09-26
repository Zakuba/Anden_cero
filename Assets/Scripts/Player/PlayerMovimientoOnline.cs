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

    private CharacterController controller;
    private Vector3 movementInput;
    private float verticalVelocity = 0f;
    private const float Gravity = -9.81f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            StartCoroutine(SetupCameraRoutine());
        }
    }

    private System.Collections.IEnumerator SetupCameraRoutine()
    {
        CinemachineCamera vcam = null;
        int maxAttempts = 20; // Reintentar durante varios frames
        int currentAttempt = 0;

        // Busca la cámara hasta encontrarla o agotar intentos
        while (vcam == null && currentAttempt < maxAttempts)
        {
            // 1. Intento por tipo
            vcam = FindAnyObjectByType<CinemachineCamera>();

            // 2. Si no la encuentra por tipo, buscar por GameObject directo en la escena
            if (vcam == null)
            {
                GameObject camObj = GameObject.Find("CinemachineCamera");
                if (camObj != null)
                {
                    vcam = camObj.GetComponent<CinemachineCamera>();
                }
            }

            if (vcam != null)
            {
                // Asignar el Transform del jugador como objetivo en Unity 6
                vcam.Target.TrackingTarget = transform;
                Debug.Log($"[Cámara] ¡Objetivo asignado con éxito a: {gameObject.name} en el intento {currentAttempt}!");
                yield break;
            }

            currentAttempt++;
            yield return null; // Esperar al siguiente frame
        }

        if (vcam == null)
        {
            Debug.LogError("[Cámara] Error crítico: No se pudo localizar la CinemachineCamera tras varios intentos.");
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        GatherCustomInput();
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
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += Gravity * Time.deltaTime;
        }

        Vector3 moveDirection = new Vector3(movementInput.x, 0f, movementInput.z);

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        Vector3 finalVelocity = (moveDirection * moveSpeed) + (Vector3.up * verticalVelocity);
        controller.Move(finalVelocity * Time.deltaTime);
    }
}