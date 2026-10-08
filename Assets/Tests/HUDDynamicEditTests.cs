using NUnit.Framework;
using System;

public class HUDDynamicEditTests
{
    public enum TestPowerUpType
    {
        Ninguno,
        Expansor,
        BombaExtra,
        Botas,
        Guante,
        Escudo
    }

    // Mock para simular la gestión del HUD y la sincronización de red del jugador
    private class HUDControllerMock
    {
        // Eventos para notificar cambios en la UI (Patrón Observador)
        public event Action<int> OnLivesChanged;
        public event Action<int> OnMaxBombsChanged;
        public event Action<TestPowerUpType> OnPowerUpChanged;

        // Estado sincronizado
        public int CurrentLives { get; private set; } = 3;
        public int CurrentMaxBombs { get; private set; } = 1;
        public TestPowerUpType ActivePowerUp { get; private set; } = TestPowerUpType.Ninguno;

        // Elementos simulados de la UI
        public int DisplayedLives { get; private set; }
        public int DisplayedMaxBombs { get; private set; }
        public TestPowerUpType DisplayedPowerUpIcon { get; private set; }

        public HUDControllerMock()
        {
            // Suscripción a eventos de red/estado
            OnLivesChanged += lives => DisplayedLives = lives;
            OnMaxBombsChanged += bombs => DisplayedMaxBombs = bombs;
            OnPowerUpChanged += powerUp => DisplayedPowerUpIcon = powerUp;

            // Inicializar UI
            RefreshUI();
        }

        public void RefreshUI()
        {
            DisplayedLives = CurrentLives;
            DisplayedMaxBombs = CurrentMaxBombs;
            DisplayedPowerUpIcon = ActivePowerUp;
        }

        // Simulación de sincronización de red (NetworkVariable / RPC)
        public void NetworkUpdateLives(int newLives)
        {
            CurrentLives = Math.Max(0, newLives);
            OnLivesChanged?.Invoke(CurrentLives);
        }

        public void NetworkUpdateMaxBombs(int newMaxBombs)
        {
            CurrentMaxBombs = Math.Max(0, newMaxBombs);
            OnMaxBombsChanged?.Invoke(CurrentMaxBombs);
        }

        public void NetworkUpdatePowerUp(TestPowerUpType newPowerUp)
        {
            ActivePowerUp = newPowerUp;
            OnPowerUpChanged?.Invoke(ActivePowerUp);
        }
    }

    private HUDControllerMock hud;

    [SetUp]
    public void SetUp()
    {
        hud = new HUDControllerMock();
    }

    [Test]
    public void HUD_InitialState_ReflectsDefaultPlayerStats()
    {
        Assert.AreEqual(3, hud.DisplayedLives, "El HUD debe inicializar con 3 vidas por defecto.");
        Assert.AreEqual(1, hud.DisplayedMaxBombs, "El HUD debe inicializar con 1 bomba como límite base.");
        Assert.AreEqual(TestPowerUpType.Ninguno, hud.DisplayedPowerUpIcon, "El HUD no debe mostrar ningún ícono de Power-Up al inicio.");
    }

    [Test]
    public void HUD_OnPlayerDamage_UpdatesLivesInRealTime()
    {
        hud.NetworkUpdateLives(2);

        Assert.AreEqual(2, hud.DisplayedLives, "La UI del HUD debe reflejar inmediatamente la pérdida de vida a 2.");
    }

    [Test]
    public void HUD_OnPlayerDeath_DisplaysZeroLives()
    {
        hud.NetworkUpdateLives(0);

        Assert.AreEqual(0, hud.DisplayedLives, "El HUD debe reflejar 0 vidas cuando el jugador es eliminado.");
    }

    [Test]
    public void HUD_OnBombaExtraPickup_UpdatesMaxBombsIconCount()
    {
        hud.NetworkUpdateMaxBombs(2);

        Assert.AreEqual(2, hud.DisplayedMaxBombs, "El HUD debe actualizar el límite visual de bombas a 2.");
    }

    [Test]
    public void HUD_OnPowerUpPickup_DisplaysActivePowerUpIcon()
    {
        hud.NetworkUpdatePowerUp(TestPowerUpType.Escudo);

        Assert.AreEqual(TestPowerUpType.Escudo, hud.DisplayedPowerUpIcon, "El HUD debe activar el ícono correspondiente al Escudo.");
    }

    [Test]
    public void HUD_OnPowerUpConsumedOrLost_ClearsPowerUpIcon()
    {
        hud.NetworkUpdatePowerUp(TestPowerUpType.Escudo);
        hud.NetworkUpdatePowerUp(TestPowerUpType.Ninguno);

        Assert.AreEqual(TestPowerUpType.Ninguno, hud.DisplayedPowerUpIcon, "El HUD debe limpiar el ícono del Power-Up cuando se consume.");
    }

    [Test]
    public void HUD_OnMultipleNetworkUpdates_StaysSynchronized()
    {
        // Simulación de ráfaga de eventos de red
        hud.NetworkUpdateLives(2);
        hud.NetworkUpdatePowerUp(TestPowerUpType.Botas);
        hud.NetworkUpdateMaxBombs(3);

        Assert.AreEqual(2, hud.DisplayedLives, "Las vidas deben quedar en 2.");
        Assert.AreEqual(TestPowerUpType.Botas, hud.DisplayedPowerUpIcon, "El Power-Up debe ser Botas.");
        Assert.AreEqual(3, hud.DisplayedMaxBombs, "El máximo de bombas debe ser 3.");
    }
}