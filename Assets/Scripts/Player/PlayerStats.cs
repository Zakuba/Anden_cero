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

    private readonly NetworkVariable<PowerUpType> activePowerUp = new NetworkVariable<PowerUpType>(
        PowerUpType.Ninguno, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<PowerUpType> queuedPowerUp = new NetworkVariable<PowerUpType>(
        PowerUpType.Ninguno, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Coroutine activeTimerRoutine;

    public int ExplosionRangeBonus => activePowerUp.Value == PowerUpType.Expansor ? bonusExplosionRange : 0;
    public int MaxBombsBonus => activePowerUp.Value == PowerUpType.BombaExtra ? bonusMaxBombs : 0;
    public float MoveSpeedMultiplier => activePowerUp.Value == PowerUpType.Botas ? botasSpeedMultiplier : 1f;
    public float BateoMultiplier => activePowerUp.Value == PowerUpType.Guante ? guanteBateoMultiplier : 1f;
    public bool IsShielded => activePowerUp.Value == PowerUpType.Escudo;

    public override void OnNetworkSpawn()
    {
        activePowerUp.OnValueChanged += OnActivePowerUpChanged;
        UpdateEscudoVisual(activePowerUp.Value);
    }

    public override void OnNetworkDespawn()
    {
        activePowerUp.OnValueChanged -= OnActivePowerUpChanged;
    }

    // Esto lo va a llamar el pickup de HU-03.2 más adelante.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPickupPowerUpServerRpc(PowerUpType type)
    {
    Debug.Log($"[PlayerStats] Power-up solicitado: {type}");

    if (activePowerUp.Value == PowerUpType.Ninguno)
        {
            ActivatePowerUp(type);
        }
        else if (queuedPowerUp.Value == PowerUpType.Ninguno)
        {
            queuedPowerUp.Value = type;
        }
        // Si ya hay uno activo y uno en cola, se ignora: ya está en el límite.
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

        if (queuedPowerUp.Value != PowerUpType.Ninguno)
        {
            PowerUpType next = queuedPowerUp.Value;
            queuedPowerUp.Value = PowerUpType.Ninguno;
            ActivatePowerUp(next);
        }
        else
        {
            activePowerUp.Value = PowerUpType.Ninguno;
        }
    }

    private void OnActivePowerUpChanged(PowerUpType previous, PowerUpType current)
{
    Debug.Log($"[PlayerStats] Power-up activo cambió de {previous} a {current}");
    UpdateEscudoVisual(current);
}

    private void UpdateEscudoVisual(PowerUpType current)
    {
        if (escudoVisual != null)
        {
            escudoVisual.SetActive(current == PowerUpType.Escudo);
        }
    }

// --- SOLO PARA PROBAR, hasta que exista HU-03.2 (drops reales) ---
#if UNITY_EDITOR
    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) RequestPickupPowerUpServerRpc(PowerUpType.Expansor);
        if (Input.GetKeyDown(KeyCode.Alpha2)) RequestPickupPowerUpServerRpc(PowerUpType.BombaExtra);
        if (Input.GetKeyDown(KeyCode.Alpha3)) RequestPickupPowerUpServerRpc(PowerUpType.Botas);
        if (Input.GetKeyDown(KeyCode.Alpha4)) RequestPickupPowerUpServerRpc(PowerUpType.Guante);
        if (Input.GetKeyDown(KeyCode.Alpha5)) RequestPickupPowerUpServerRpc(PowerUpType.Escudo);
    }
#endif
}