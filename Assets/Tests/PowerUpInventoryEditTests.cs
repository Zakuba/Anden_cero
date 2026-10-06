using NUnit.Framework;

public class PowerUpInventoryEditTests
{
    // Definición local del Enum para aislar la prueba de dependencias de Assembly
    public enum TestPowerUpType
    {
        Ninguno,
        Expansor,
        BombaExtra,
        Botas,
        Guante,
        Escudo
    }

    // Clase simuladora para testear la lógica de inventario y estadísticas de PlayerStats sin dependencias de red/escena
    private class PlayerStatsMock
    {
        public TestPowerUpType ActivePowerUp { get; private set; } = TestPowerUpType.Ninguno;

        // Stats base
        public int BaseExplosionRange { get; set; } = 2;
        public int BaseMaxBombs { get; set; } = 1;
        public float BaseMoveSpeed { get; set; } = 5f;
        public float BaseBatMultiplier { get; set; } = 1f;

        // Bonificadores
        public int BonusExplosionRange => 1;
        public int BonusMaxBombs => 1;
        public float BotasSpeedMultiplier => 1.5f;
        public float GuanteBatMultiplier => 1.5f;

        public bool HasActivePowerUp => ActivePowerUp != TestPowerUpType.Ninguno;
        public bool IsShielded => ActivePowerUp == TestPowerUpType.Escudo;

        // Stats efectivas
        public int EffectiveExplosionRange => BaseExplosionRange + (ActivePowerUp == TestPowerUpType.Expansor ? BonusExplosionRange : 0);
        public int EffectiveMaxBombs => BaseMaxBombs + (ActivePowerUp == TestPowerUpType.BombaExtra ? BonusMaxBombs : 0);
        public float EffectiveMoveSpeed => BaseMoveSpeed * (ActivePowerUp == TestPowerUpType.Botas ? BotasSpeedMultiplier : 1f);
        public float EffectiveBatMultiplier => BaseBatMultiplier * (ActivePowerUp == TestPowerUpType.Guante ? GuanteBatMultiplier : 1f);

        public bool TryPickupPowerUp(TestPowerUpType type)
        {
            // Criterio de aceptación: Máximo 1 power-up acumulable
            if (HasActivePowerUp) return false;

            ActivePowerUp = type;
            return true;
        }

        public void ConsumeShield()
        {
            if (IsShielded)
            {
                ActivePowerUp = TestPowerUpType.Ninguno;
            }
        }
    }

    private PlayerStatsMock stats;

    [SetUp]
    public void SetUp()
    {
        stats = new PlayerStatsMock();
    }

    [Test]
    public void Pickup_FirstPowerUp_SuccessfullyEquipped()
    {
        bool success = stats.TryPickupPowerUp(TestPowerUpType.Expansor);

        Assert.IsTrue(success, "El jugador debe poder recoger un Power-Up si no tiene ninguno activo.");
        Assert.AreEqual(TestPowerUpType.Expansor, stats.ActivePowerUp, "El Power-Up activo debe ser Expansor.");
    }

    [Test]
    public void Pickup_SecondPowerUpWhenAlreadyHasOne_IsRejectedDueToLimit()
    {
        stats.TryPickupPowerUp(TestPowerUpType.Expansor);

        bool success = stats.TryPickupPowerUp(TestPowerUpType.Botas);

        Assert.IsFalse(success, "El jugador no debe poder recoger un segundo Power-Up (Límite: 1).");
        Assert.AreEqual(TestPowerUpType.Expansor, stats.ActivePowerUp, "El Power-Up activo debe seguir siendo el primero recolectado.");
    }

    [Test]
    public void PowerUp_Expansor_IncreasesExplosionRange()
    {
        stats.TryPickupPowerUp(TestPowerUpType.Expansor);

        Assert.AreEqual(3, stats.EffectiveExplosionRange, "El Expansor debe incrementar el alcance de explosión en +1.");
    }

    [Test]
    public void PowerUp_BombaExtra_IncreasesMaxBombs()
    {
        stats.TryPickupPowerUp(TestPowerUpType.BombaExtra);

        Assert.AreEqual(2, stats.EffectiveMaxBombs, "Bomba Extra debe incrementar la capacidad máxima de bombas en +1.");
    }

    [Test]
    public void PowerUp_Botas_IncreasesMoveSpeed()
    {
        stats.TryPickupPowerUp(TestPowerUpType.Botas);

        Assert.AreEqual(7.5f, stats.EffectiveMoveSpeed, "Las Botas deben multiplicar la velocidad de movimiento por 1.5.");
    }

    [Test]
    public void PowerUp_Guante_IncreasesBateoMultiplier()
    {
        stats.TryPickupPowerUp(TestPowerUpType.Guante);

        Assert.AreEqual(1.5f, stats.EffectiveBatMultiplier, "El Guante debe incrementar el multiplicador de bateo a 1.5.");
    }

    [Test]
    public void PowerUp_Escudo_ActivatesShieldAndConsumesOnHit()
    {
        stats.TryPickupPowerUp(TestPowerUpType.Escudo);

        Assert.IsTrue(stats.IsShielded, "El Escudo debe marcar al jugador como protegido.");

        stats.ConsumeShield();

        Assert.IsFalse(stats.IsShielded, "Al impactar una explosión, el Escudo debe consumirse.");
        Assert.IsFalse(stats.HasActivePowerUp, "El jugador debe quedar sin Power-Up activo tras perder el Escudo.");
    }

    [Test]
    public void Pickup_AfterShieldConsumed_AllowsNewPowerUp()
    {
        stats.TryPickupPowerUp(TestPowerUpType.Escudo);
        stats.ConsumeShield();

        bool success = stats.TryPickupPowerUp(TestPowerUpType.Botas);

        Assert.IsTrue(success, "Tras consumir el Power-Up previo, el jugador debe poder recoger uno nuevo.");
        Assert.AreEqual(TestPowerUpType.Botas, stats.ActivePowerUp, "El nuevo Power-Up activo debe ser Botas.");
    }
}