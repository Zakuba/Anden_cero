using UnityEngine;
using Unity.Netcode;
using System.Collections;

public enum PlayerState
{
    Vivo,
    Aturdido,
    Muerto
}

public class PlayerStateManager : NetworkBehaviour
{
    [Header("Configuración de Vidas")]
    [SerializeField] private int maxLives = 3;

    [Header("Físicas y Colisiones")]
    [Tooltip("El CharacterController del jugador (sólido en vida).")]
    [SerializeField] private CharacterController characterController;
    
    [Tooltip("Collider secundario que actúa como Trigger cuando el jugador muere.")]
    [SerializeField] private Collider ghostTriggerCollider;

    [Header("Feedback Visual")]
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Color colorVivo = Color.white;
    [SerializeField] private Color colorAturdido = Color.yellow;
    [SerializeField] private Color colorMuerto = Color.gray;

    private PlayerLivesUI localLivesUI;

    public NetworkVariable<PlayerState> currentState = new NetworkVariable<PlayerState>(
        PlayerState.Vivo, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> currentLives = new NetworkVariable<int>(
        3, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
    }

    private void Update()
        {
            // Solo el jugador local debe buscar y actualizar su propia pantalla
            if (!IsOwner) return;

            // RED DE SEGURIDAD: Si no encontró la UI al nacer, la sigue buscando
            if (localLivesUI == null)
            {
                localLivesUI = FindObjectOfType<PlayerLivesUI>(true);
                
                // Si por fin la encontró en este fotograma...
                if (localLivesUI != null)
                {
                    // ...la actualizamos inmediatamente con las vidas actuales
                    localLivesUI.UpdateHearts(currentLives.Value);
                }
            }
        }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentLives.Value = maxLives;
            currentState.Value = PlayerState.Vivo;
        }

        currentState.OnValueChanged += OnStateChanged;
        currentLives.OnValueChanged += OnLivesChanged; // Escuchamos los cambios de vida

        // 2. Búsqueda y configuración de UI solo para el jugador local
        if (IsOwner)
        {
            localLivesUI = FindObjectOfType<PlayerLivesUI>(true);

            if (localLivesUI != null)
            {
                localLivesUI.UpdateHearts(currentLives.Value); // Dibuja los 3 corazones al iniciar
            } else{
                Debug.Log("No se encontro el objeto");
            }
        }

        ApplyStateProperties(currentState.Value);

        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.RegisterPlayer(this);
        }
    }

    // Buena práctica desuscribirse cuando el objeto se destruye
    public override void OnNetworkDespawn()
    {
        currentState.OnValueChanged -= OnStateChanged;
        currentLives.OnValueChanged -= OnLivesChanged;
    }

    // Este método se dispara solo cuando el servidor altera currentLives.Value
    private void OnLivesChanged(int previousValue, int newValue)
    {
        // Solo actualizamos la pantalla de quien recibió el daño
        if (IsOwner && localLivesUI != null)
        {
            localLivesUI.UpdateHearts(newValue);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TakeDamageServerRpc()
    {
        // Si ya está muerto o aturdido, ignora daño
        if (currentState.Value == PlayerState.Muerto || currentState.Value == PlayerState.Aturdido) return;

        // --- PROTECCIÓN POR ESCUDO ---
        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats != null && stats.IsShielded)
        {
            Debug.Log("[PlayerStateManager] ¡Explosión bloqueada por el Escudo!");
            stats.ConsumeShield(); // Consume el escudo en el servidor y apaga el visual
            return; // Anula la pérdida de vida y el aturdimiento
        }

        // Si no tiene escudo, recibe el daño normal
        currentLives.Value--;

        if (currentLives.Value <= 0)
        {
            currentState.Value = PlayerState.Muerto;
        }
        else
        {
            StartCoroutine(StunRoutine());
        }
    }

    private IEnumerator StunRoutine()
    {
        currentState.Value = PlayerState.Aturdido;
        yield return new WaitForSeconds(2f);
        
        if (currentState.Value != PlayerState.Muerto)
        {
            currentState.Value = PlayerState.Vivo;
        }
    }

    private void OnStateChanged(PlayerState previous, PlayerState current)
    {
        ApplyStateProperties(current);
    }

    private void ApplyStateProperties(PlayerState state)
    {
        // 1. Color del modelo
        if (playerRenderer != null)
        {
            switch (state)
            {
                case PlayerState.Vivo:
                    playerRenderer.material.color = colorVivo;
                    break;
                case PlayerState.Aturdido:
                    playerRenderer.material.color = colorAturdido;
                    break;
                case PlayerState.Muerto:
                    playerRenderer.material.color = colorMuerto;
                    break;
            }
        }

        // 2. Transición a Trigger al estar muerto
        bool isDead = (state == PlayerState.Muerto);

        if (characterController != null)
        {
            characterController.enabled = !isDead; // Apaga la colisión sólida al morir
        }

        if (ghostTriggerCollider != null)
        {
            ghostTriggerCollider.enabled = isDead; // Enciende el sensor Trigger
        }
    }
}