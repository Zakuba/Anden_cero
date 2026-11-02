using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic; // <--- Agrega esta línea

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
    // Flag interno del servidor para inmunidad breve al romper escudo
    private bool isShieldInvulnerable = false;
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

    // Flag del servidor para controlar si el jugador recibió daño durante la partida
    private bool hasTakenDamageThisMatch = false;
    public bool HasTakenDamageThisMatch => hasTakenDamageThisMatch;

    // --- RASTREO EN SERVIDOR PARA DOBLE ELIMINACIÓN ---
    private static readonly Dictionary<ulong, (int kills, float lastKillTime)> killsWindowByAttacker 
        = new Dictionary<ulong, (int kills, float lastKillTime)>();

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
    public void TakeDamageServerRpc(ulong attackerClientId = 999999)
    {
        if (MatchManager.Instance != null && !MatchManager.Instance.IsMatchRunning)
        {
            Debug.LogWarning($"[DobleKill-Debug] Daño rechazado en {gameObject.name}: MatchManager indica que la partida no está en ejecución.");
            return;
        }

        if (currentState.Value == PlayerState.Muerto || currentState.Value == PlayerState.Aturdido)
            return;

        if (isShieldInvulnerable)
            return;
        
        // Modo 2 vs 2: las bombas del propio equipo (incluida la propia) no hacen daño
if (attackerClientId != 999999 &&
    MatchManager.Instance != null &&
    MatchManager.Instance.EsMismoEquipo(attackerClientId, OwnerClientId))
{
    Debug.Log($"[Equipos] Daño ignorado: el atacante {attackerClientId} y la víctima {OwnerClientId} son del mismo equipo.");
    return;
}

        PlayerStats stats = GetComponent<PlayerStats>();

        if (stats != null && stats.IsShielded)
        {
            stats.ConsumeShield();
            StartCoroutine(ShieldBreakGracePeriodRoutine());
            return;
        }

        // REGISTRO DE DAÑO PARA LOGRO:
        hasTakenDamageThisMatch = true;

        currentLives.Value--;

        Debug.Log($"[DobleKill-Debug] {gameObject.name} recibió daño de ClientId: {attackerClientId}. Vidas restantes: {currentLives.Value}");

        if (currentLives.Value <= 0)
        {
            currentState.Value = PlayerState.Muerto;

            // VALIDACIONES DE ELIMINACIÓN
            if (attackerClientId != 999999 && attackerClientId != OwnerClientId)
            {
                // 1. Logro "Primera Baja"
                AwardFirstKillAchievementClientRpc(attackerClientId);

                // 2. Rastreo para "Doble Eliminación"
                float currentTime = Time.time;
                if (killsWindowByAttacker.TryGetValue(attackerClientId, out var tracker))
                {
                    float timeDiff = currentTime - tracker.lastKillTime;
                    Debug.Log($"[DobleKill-Debug] Atacante {attackerClientId} sumó otra baja. Diferencia de tiempo: {timeDiff:F4}s (Límite: 0.35s).");

                    // Ventana de 0.35 s para absorber discrepancias de frames en la física de la explosión
                    if (timeDiff <= 0.35f)
                    {
                        int totalKills = tracker.kills + 1;
                        killsWindowByAttacker[attackerClientId] = (totalKills, currentTime);
                        Debug.Log($"[DobleKill-Debug] ¡CONDICIÓN CUMPLIDA! Atacante {attackerClientId} alcanzó {totalKills} bajas simultáneas.");

                        AwardDoubleKillAchievementClientRpc(attackerClientId);
                        killsWindowByAttacker.Remove(attackerClientId);
                    }
                    else
                    {
                        Debug.LogWarning($"[DobleKill-Debug] Baja fuera de tiempo ({timeDiff:F4}s > 0.35s). Se reinicia contador a 1.");
                        killsWindowByAttacker[attackerClientId] = (1, currentTime);
                    }
                }
                else
                {
                    Debug.Log($"[DobleKill-Debug] Primera baja registrada en la ventana para el atacante {attackerClientId}.");
                    killsWindowByAttacker[attackerClientId] = (1, currentTime);
                }
            }
            else
            {
                Debug.LogWarning($"[DobleKill-Debug] Baja descartada: suicidio o atacante por defecto ({attackerClientId}).");
            }
        }
        else
        {
            StartCoroutine(StunRoutine());
        }
    }

    [ClientRpc]
    private void AwardFirstKillAchievementClientRpc(ulong killerClientId)
    {
        // Se ejecuta únicamente en la máquina del jugador que realizó la eliminación
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == killerClientId)
        {
            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.UnlockAchievement("primera_baja");
            }
        }
    }

    private IEnumerator ShieldBreakGracePeriodRoutine()
    {
        isShieldInvulnerable = true;
        // Otorga 0.3 segundos de protección para que no entren dobles impactos (fuego + zona o multi-hit)
        yield return new WaitForSeconds(0.3f);
        isShieldInvulnerable = false;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void InstantKillServerRpc()
    {
        if (currentState.Value == PlayerState.Muerto)
            return;
        hasTakenDamageThisMatch = true;
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
        // Si MatchManager no existe (como en el minijuego), asumimos que la partida sí está activa (true)
        bool partidaIniciada = MatchManager.Instance == null || MatchManager.Instance.IsMatchRunning;
        bool isDead = (state == PlayerState.Muerto);

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

    /// <summary>
    /// Reinicia el contador de daño al comenzar una partida (solo servidor).
    /// </summary>
    public void ResetMatchDamageTracker()
    {
        if (!IsServer) return;
        hasTakenDamageThisMatch = false;
    }

    [ClientRpc]
    public void AwardInvincibleSurvivorAchievementClientRpc()
    {
        // Se ejecuta únicamente en la máquina del jugador ganador
        if (IsOwner && AchievementManager.Instance != null)
        {
            AchievementManager.Instance.UnlockAchievement("superviviente_invencible");
        }
    }

    [ClientRpc]
    private void AwardDoubleKillAchievementClientRpc(ulong killerClientId)
    {
        Debug.Log($"[DobleKill-Debug] AwardDoubleKillAchievementClientRpc recibido en cliente local ({NetworkManager.Singleton.LocalClientId}). Esperado: {killerClientId}");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == killerClientId)
        {
            Debug.Log("[DobleKill-Debug] Soy el dueño de la doble baja. Invocando AchievementManager...");
            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.UnlockAchievement("doble_kill");
            }
            else
            {
                Debug.LogError("[DobleKill-Debug] Error: AchievementManager.Instance es null en el cliente.");
            }
        }
    }
}