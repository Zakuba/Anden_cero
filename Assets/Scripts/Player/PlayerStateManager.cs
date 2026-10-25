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
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Collider ghostTriggerCollider;

    [Header("Feedback Visual")]
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Color colorVivo = Color.white;
    [SerializeField] private Color colorAturdido = Color.yellow;
    [SerializeField] private Color colorMuerto = Color.gray;

    private MatchManager subscribedMatchManager;

    private PlayerLivesUI localLivesUI;

    public NetworkVariable<PlayerState> currentState =
        new NetworkVariable<PlayerState>(
            PlayerState.Vivo,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<int> currentLives =
        new NetworkVariable<int>(
            3,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Vidas máximas de este jugador, visibles para todos (el HUD las necesita para dibujar los corazones).
    public NetworkVariable<int> maxLivesNet =
        new NetworkVariable<int>(
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
        if (!IsOwner)
            return;

        if (localLivesUI == null)
        {
            localLivesUI = FindObjectOfType<PlayerLivesUI>(true);

            RefreshLivesUI();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentLives.Value = maxLives;
            maxLivesNet.Value = maxLives;
            currentState.Value = PlayerState.Vivo;
        }

        currentState.OnValueChanged += OnStateChanged;
        currentLives.OnValueChanged += OnLivesChanged;
        maxLivesNet.OnValueChanged += OnMaxLivesChanged;

        if (IsOwner)
        {
            localLivesUI = FindObjectOfType<PlayerLivesUI>(true);

            RefreshLivesUI();
        }

        ApplyStateProperties(currentState.Value);

        if (MatchManager.Instance != null)
        {
            ConectarMatchManager(MatchManager.Instance);
            MatchManager.Instance.RegisterPlayer(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        currentState.OnValueChanged -= OnStateChanged;
        currentLives.OnValueChanged -= OnLivesChanged;
        maxLivesNet.OnValueChanged -= OnMaxLivesChanged;

        if (subscribedMatchManager != null)
        {
            subscribedMatchManager.MatchStartedChanged -=
                OnMatchStartedChanged;

            subscribedMatchManager = null;
        }
    }

    /// <summary>
    /// Solo servidor. Fija las vidas máximas de este jugador y le llena la vida.
    /// La usa WaveManager al empezar Supervivencia, según la dificultad.
    /// </summary>
    public void ApplyMaxLives(int value)
    {
        if (!IsServer) return;

        maxLives = Mathf.Max(1, value);
        maxLivesNet.Value = maxLives;
        currentLives.Value = maxLives;
    }

    private void OnLivesChanged(int previousValue, int newValue)
    {
        RefreshLivesUI();
    }

    private void OnMaxLivesChanged(int previousValue, int newValue)
    {
        RefreshLivesUI();
    }

    // Corazones del HUD del jugador local: vidas actuales y máximas.
    private void RefreshLivesUI()
    {
        if (!IsOwner || localLivesUI == null) return;
        localLivesUI.UpdateHearts(currentLives.Value, maxLivesNet.Value);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TakeDamageServerRpc()
    {
        if (MatchManager.Instance != null &&
            !MatchManager.Instance.IsMatchRunning)
            return;

        if (currentState.Value == PlayerState.Muerto ||
            currentState.Value == PlayerState.Aturdido)
            return;

        PlayerStats stats = GetComponent<PlayerStats>();

        if (stats != null && stats.IsShielded)
        {
            stats.ConsumeShield();
            return;
        }

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
            currentState.Value = PlayerState.Vivo;
    }

    private void OnStateChanged(PlayerState previous, PlayerState current)
    {
        ApplyStateProperties(current);
    }

    private void ApplyStateProperties(PlayerState state)
    {
        // 1. Cambio de color.
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

        // 2. Animación del personaje activo.
        Animator[] animators = GetComponentsInChildren<Animator>(true);

        foreach (Animator animator in animators)
        {
            if (animator.gameObject.activeInHierarchy)
            {
                animator.SetInteger("PlayerState", (int)state);
                break;
            }
        }

        // 3. Determinar si la partida ya comenzó.
        bool partidaIniciada =
            MatchManager.Instance != null &&
            MatchManager.Instance.IsMatchRunning;

        bool isDead = state == PlayerState.Muerto;

        // 4. El CharacterController solo funciona durante la partida
        // y mientras el jugador no esté muerto.
        if (characterController != null)
        {
            characterController.enabled = partidaIniciada && !isDead;
        }

        // 5. El collider fantasma solo funciona al morir.
        if (ghostTriggerCollider != null)
        {
            ghostTriggerCollider.enabled = isDead;
        }
    }

    // Llamar desde MatchManager al comenzar la partida.
    public void ActualizarColisionesPartida()
    {
        ApplyStateProperties(currentState.Value);
    }

    public void ConectarMatchManager(MatchManager manager)
    {
        if (manager == null)
            return;

        // Evitar suscripciones duplicadas.
        if (subscribedMatchManager == manager)
            return;

        // Desuscribirse del manager anterior, si existe.
        if (subscribedMatchManager != null)
        {
            subscribedMatchManager.MatchStartedChanged -=
                OnMatchStartedChanged;
        }

        subscribedMatchManager = manager;

        subscribedMatchManager.MatchStartedChanged +=
            OnMatchStartedChanged;

        // Aplicar inmediatamente el estado actual.
        ApplyStateProperties(currentState.Value);
    }

    private void OnMatchStartedChanged(bool oldValue, bool newValue)
    {
        ApplyStateProperties(currentState.Value);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void HealServerRpc()
    {
        // Validación de que la partida esté corriendo (siguiendo el patrón de TakeDamageServerRpc)
        if (MatchManager.Instance != null && !MatchManager.Instance.IsMatchRunning)
            return;

        // No se puede curar a un jugador muerto
        if (currentState.Value == PlayerState.Muerto)
            return;

        // Incrementa la vida solo si es menor al máximo permitido
        if (currentLives.Value < maxLives)
        {
            currentLives.Value++;
            
            // Nota: Como currentLives tiene un evento 'OnValueChanged', la interfaz de corazones 
            // del cliente se actualizará automáticamente de manera sincrónica.
        }
    }

    public bool TryHeal()
    {
        if (MatchManager.Instance != null && !MatchManager.Instance.IsMatchRunning)
            return false;

        // No se puede curar a un jugador muerto
        if (currentState.Value == PlayerState.Muerto)
            return false;

        // Si la vida actual es menor al máximo, curamos y retornamos true
        if (currentLives.Value < maxLives)
        {
            currentLives.Value++;
            return true; // Curación exitosa
        }

        return false; // Ya tiene la vida al máximo, no se aplicó la curación
    }
}