using Unity.Netcode;
using UnityEngine;
using System.Collections;

public enum PowerUpType
{
    Ninguno,
    Expansor,
    BombaExtra,
    Botas,
    Guante,
    Escudo
}

[RequireComponent(typeof(PlayerBombController))]
public class PlayerStats : NetworkBehaviour
{
    [Header("Duración del power-up activo")]
    [SerializeField] private float powerUpDuration = 15f;

    [Header("Efectos de cada power-up")]
    [SerializeField] private int bonusExplosionRange = 1;
    [SerializeField] private int bonusMaxBombs = 1;
    [SerializeField] private float botasSpeedMultiplier = 1.5f;
    [SerializeField] private float guanteBateoMultiplier = 1.5f;

    [Header("Indicador visual de Escudo")]
    [SerializeField] private GameObject escudoVisual;

    private PowerUpUI localPowerUpUI;
    public float PowerUpDuration => powerUpDuration;

    private readonly NetworkVariable<PowerUpType> activePowerUp = new NetworkVariable<PowerUpType>(
        PowerUpType.Ninguno, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Coroutine activeTimerRoutine;

    public int ExplosionRangeBonus => activePowerUp.Value == PowerUpType.Expansor ? bonusExplosionRange : 0;
    public int MaxBombsBonus => activePowerUp.Value == PowerUpType.BombaExtra ? bonusMaxBombs : 0;
    public float MoveSpeedMultiplier => activePowerUp.Value == PowerUpType.Botas ? botasSpeedMultiplier : 1f;
    public float BateoMultiplier => activePowerUp.Value == PowerUpType.Guante ? guanteBateoMultiplier : 1f;
    public bool IsShielded => activePowerUp.Value == PowerUpType.Escudo;

    // Para que PowerUpPickup sepa si este jugador puede agarrar uno nuevo
    public bool HasActivePowerUp => activePowerUp.Value != PowerUpType.Ninguno;

    public override void OnNetworkSpawn()
    {
        activePowerUp.OnValueChanged += OnActivePowerUpChanged;
        UpdateEscudoVisual(activePowerUp.Value);

        if (IsOwner)
        {
            localPowerUpUI = FindObjectOfType<PowerUpUI>(true);
        }
    }

    public override void OnNetworkDespawn()
    {
        activePowerUp.OnValueChanged -= OnActivePowerUpChanged;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPickupPowerUpServerRpc(PowerUpType type)
    {
        // Si ya tiene uno activo, se ignora (PowerUpPickup ya deberia haber
        // filtrado esto antes de llamar, pero lo dejamos como resguardo).
        if (activePowerUp.Value != PowerUpType.Ninguno) return;

        Debug.Log($"[PlayerStats] Power-up solicitado: {type}");
        ActivatePowerUp(type);
    }

    private void ActivatePowerUp(PowerUpType type)
    {
        activePowerUp.Value = type;

        if (activeTimerRoutine != null)
        {
            StopCoroutine(activeTimerRoutine);
        }
        activeTimerRoutine = StartCoroutine(PowerUpTimer());
    }

    private IEnumerator PowerUpTimer()
    {
        yield return new WaitForSeconds(powerUpDuration);
        activePowerUp.Value = PowerUpType.Ninguno;
    }

    private void OnActivePowerUpChanged(PowerUpType previous, PowerUpType current)
    {
        Debug.Log($"[PlayerStats] Power-up activo cambió de {previous} a {current}");
        UpdateEscudoVisual(current);

        if (IsOwner && localPowerUpUI != null)
        {
            if (current != PowerUpType.Ninguno)
            {
                // Enciende el HUD con el tipo de efecto y los segundos de duración
                localPowerUpUI.ActivateEffect(current, powerUpDuration);
            }
            else
            {
                // Apaga el HUD cuando el tiempo termina o se rompe el escudo
                localPowerUpUI.DeactivateEffect();
            }
        }
    }

    private void UpdateEscudoVisual(PowerUpType current)
    {
        if (escudoVisual != null)
        {
            escudoVisual.SetActive(current == PowerUpType.Escudo);
        }
    }

    /// Consume el escudo de inmediato tras bloquear una explosión.
    /// Solo debe llamarse en el Servidor/Host.
    public void ConsumeShield()
    {
        if (!IsServer) return;

        if (activePowerUp.Value == PowerUpType.Escudo)
        {
            if (activeTimerRoutine != null)
            {
                StopCoroutine(activeTimerRoutine);
            }
            activePowerUp.Value = PowerUpType.Ninguno;
        }
    }

    // --- SOLO PARA PROBAR, hasta que exista un pickup real por teclado ---
#if UNITY_EDITOR
    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) RequestPickupPowerUpServerRpc(PowerUpType.Expansor);
        if (Input.GetKeyDown(KeyCode.Alpha2)) RequestPickupPowerUpServerRpc(PowerUpType.BombaExtra);
        if (Input.GetKeyDown(KeyCode.Alpha3)) RequestPickupPowerUpServerRpc(PowerUpType.Botas);
        if (Input.GetKeyDown(KeyCode.Alpha4)) RequestPickupPowerUpServerRpc(PowerUpType.Guante);
        if (Input.GetKeyDown(KeyCode.Alpha5)) RequestPickupPowerUpServerRpc(PowerUpType.Escudo);

        // RED DE SEGURIDAD: Si no encontró la UI al nacer, la sigue buscando
        if (localPowerUpUI == null)
        {
            localPowerUpUI = FindObjectOfType<PowerUpUI>(true);
        }
    }
#endif
}