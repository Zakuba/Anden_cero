
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

            if (localLivesUI != null)
                localLivesUI.UpdateHearts(currentLives.Value);
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

        if (IsOwner)
        {
            localLivesUI = FindObjectOfType<PlayerLivesUI>(true);

            if (localLivesUI != null)
                localLivesUI.UpdateHearts(currentLives.Value);
        }

        ApplyStateProperties(currentState.Value);

        // Registrar al jugador en el MatchManager.
        if (MatchManager.Instance != null)
            MatchManager.Instance.RegisterPlayer(this);
    }

    public override void OnNetworkDespawn()
    {
        currentState.OnValueChanged -= OnStateChanged;
        currentLives.OnValueChanged -= OnLivesChanged;
    }

    private void OnLivesChanged(int previousValue, int newValue)
    {
        if (IsOwner && localLivesUI != null)
            localLivesUI.UpdateHearts(newValue);
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
}