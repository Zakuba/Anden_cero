using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

// Movimiento simple, SOLO para validar la sincronizacion de HU-01.3.
// El movimiento real en cuadricula es responsabilidad de HU-02.1.
[RequireComponent(typeof(CharacterController))]
public class TempMovementForSync : NetworkBehaviour
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

        // Esta condición es obligatoria: solo el dueño local configura su propia cámara
        if (IsOwner)
        {
            SetupLocalCamera();
        }
    }

private void SetupLocalCamera()
    {
        CinemachineCamera vcam = FindAnyObjectByType<CinemachineCamera>();
        if (vcam != null)
        {
            vcam.Target.TrackingTarget = transform;

            var composer = vcam.GetComponent<CinemachinePositionComposer>();
            if (composer != null)
            {
                composer.CameraDistance = 15f; 
                composer.TargetOffset = new Vector3(0f, 1f, 0f);
            }
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