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

    private Animator characterAnimator;

    private PlayerLivesUI localLivesUI;

    public NetworkVariable<PlayerState> currentState = new NetworkVariable<PlayerState>(
        PlayerState.Vivo,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> currentLives = new NetworkVariable<int>(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // Solo el jugador local debe buscar y actualizar su propia pantalla
        if (!IsOwner) return;

        // Si no encontró la UI al nacer, la sigue buscando
        if (localLivesUI == null)
        {
            localLivesUI = FindObjectOfType<PlayerLivesUI>(true);

            if (localLivesUI != null)
            {
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
        currentLives.OnValueChanged += OnLivesChanged;

        // Búsqueda y configuración de UI solo para el jugador local
        if (IsOwner)
        {
            localLivesUI = FindObjectOfType<PlayerLivesUI>(true);

            if (localLivesUI != null)
            {
                localLivesUI.UpdateHearts(currentLives.Value);
            }
        }

        ApplyStateProperties(currentState.Value);

        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.RegisterPlayer(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        currentState.OnValueChanged -= OnStateChanged;
        currentLives.OnValueChanged -= OnLivesChanged;
    }

    private void OnLivesChanged(int previousValue, int newValue)
    {
        if (IsOwner && localLivesUI != null)
        {
            localLivesUI.UpdateHearts(newValue);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TakeDamageServerRpc()
    {
        // No recibe daño hasta que empiece la partida
        if (MatchManager.Instance != null &&
            !MatchManager.Instance.IsMatchRunning)
            return;

        // Si ya está muerto o aturdido, ignora daño
        if (currentState.Value == PlayerState.Muerto ||
            currentState.Value == PlayerState.Aturdido)
            return;

        // Protección por escudo
        PlayerStats stats = GetComponent<PlayerStats>();

        if (stats != null && stats.IsShielded)
        {
            stats.ConsumeShield();
            return;
        }

        // Pierde una vida
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

    /// <summary>
    /// Muerte instantánea. Ignora escudo y vidas restantes.
    /// Utilizado por el tren u otros peligros mortales.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void InstantKillServerRpc()
    {
        if (currentState.Value == PlayerState.Muerto)
            return;

        currentLives.Value = 0;
        currentState.Value = PlayerState.Muerto;
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
        // =========================================
        // 1. CAMBIO DE COLOR
        // =========================================

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

        // =========================================
        // 2. BUSCAR ANIMATOR DEL PERSONAJE ACTIVO
        // =========================================

        Animator animatorActivo = ObtenerAnimatorActivo();

        if (animatorActivo != null)
        {
            animatorActivo.SetInteger("PlayerState", (int)state);
        }

        // =========================================
        // 3. COLISIONES
        // =========================================

        bool isDead = state == PlayerState.Muerto;

        if (characterController != null)
        {
            characterController.enabled = !isDead;
        }

        if (ghostTriggerCollider != null)
        {
            ghostTriggerCollider.enabled = isDead;
        }
    }

    private Animator ObtenerAnimatorActivo()
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);

        foreach (Animator animator in animators)
        {
            if (animator.gameObject.activeInHierarchy)
            {
                return animator;
            }
        }

        return null;
    }
}