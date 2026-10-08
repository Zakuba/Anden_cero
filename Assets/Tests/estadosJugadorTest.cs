using NUnit.Framework;
using UnityEngine;

public class estadosJugadorTest
{
    // Mock que simula las reglas de negocio de PlayerStateManager.cs
    private class EstadoJugadorMock
    {
        public int Vidas { get; private set; } = 3;
        public int BombasDisponibles { get; private set; } = 1;
        public bool TienePowerUpBombaExtra { get; set; } = false;
        public bool EstaAturdido { get; private set; } = false;
        public bool EstaMuerto { get; private set; } = false;
        public bool EsTrigger { get; private set; } = false;

        public void RecibirDaño(int cantidad)
        {
            if (EstaMuerto) return;

            Vidas -= cantidad;
            if (Vidas <= 0)
            {
                Vidas = 0;
                EstaMuerto = true;
                EsTrigger = true; // Al morir es marcado como Trigger
            }
        }

        public void AplicarAturdimiento()
        {
            if (!EstaMuerto)
            {
                EstaAturdido = true;
            }
        }

        public void RecuperarDeAturdimiento()
        {
            EstaAturdido = false;
        }

        public bool PuedoMoverme()
        {
            if (EstaMuerto || EstaAturdido) return false;
            return true;
        }

        public bool PuedoColocarBomba()
        {
            if (EstaMuerto || EstaAturdido) return false;
            return BombasDisponibles > 0;
        }

        public bool IntentarColocarBomba()
        {
            if (!PuedoColocarBomba()) return false;

            BombasDisponibles--;
            return true;
        }

        public void OtorgarPowerUpBomba()
        {
            TienePowerUpBombaExtra = true;
            BombasDisponibles += 1; // Incrementa el límite disponible
        }
    }

    private EstadoJugadorMock jugador;

    [SetUp]
    public void SetUp()
    {
        jugador = new EstadoJugadorMock();
    }

    [Test]
    public void Jugador_IniciaConTresVidasYUnaBombaBase()
    {
        Assert.AreEqual(3, jugador.Vidas, "El jugador debe iniciar con 3 vidas.");
        Assert.AreEqual(1, jugador.BombasDisponibles, "El jugador debe iniciar con 1 bomba disponible.");
        Assert.IsFalse(jugador.EstaAturdido, "El jugador no debe iniciar aturdido.");
        Assert.IsFalse(jugador.EstaMuerto, "El jugador debe iniciar vivo.");
    }

    [Test]
    public void Jugador_PowerUpIncrementaCantidadDeBombasDisponibles()
    {
        jugador.OtorgarPowerUpBomba();

        Assert.IsTrue(jugador.TienePowerUpBombaExtra, "El jugador debe registrar el Power Up de bomba.");
        Assert.AreEqual(2, jugador.BombasDisponibles, "El límite de bombas debe incrementarse al obtener el Power Up.");
    }

    [Test]
    public void JugadorAturdido_NoPuedeMoverseNiColocarBombas()
    {
        jugador.AplicarAturdimiento();

        Assert.IsFalse(jugador.PuedoMoverme(), "Un jugador aturdido no debe poder moverse.");
        Assert.IsFalse(jugador.PuedoColocarBomba(), "Un jugador aturdido no debe poder colocar bombas.");
        Assert.IsFalse(jugador.IntentarColocarBomba(), "El intento de colocar bomba debe ser rechazado si está aturdido.");
    }

    [Test]
    public void JugadorAturdido_RecuperaAccionesAlFinalizarAturdimiento()
    {
        jugador.AplicarAturdimiento();
        jugador.RecuperarDeAturdimiento();

        Assert.IsTrue(jugador.PuedoMoverme(), "El jugador debe poder moverse al quitar el aturdimiento.");
        Assert.IsTrue(jugador.PuedoColocarBomba(), "El jugador debe poder colocar bombas tras recuperarse.");
    }

    [Test]
    public void Jugador_EsMarcadoComoMuertoYTriggerAlPerderTodasLasVidas()
    {
        jugador.RecibirDaño(3);

        Assert.AreEqual(0, jugador.Vidas, "Las vidas del jugador deben llegar a 0.");
        Assert.IsTrue(jugador.EstaMuerto, "El jugador debe ser marcado como muerto.");
        Assert.IsTrue(jugador.EsTrigger, "El collider del jugador debe ser marcado como Trigger al morir.");
    }

    [Test]
    public void JugadorMuerto_NoPuedeRealizarNingunaAccion()
    {
        jugador.RecibirDaño(3);

        Assert.IsFalse(jugador.PuedoMoverme(), "Un jugador muerto no debe poder realizar movimiento.");
        Assert.IsFalse(jugador.PuedoColocarBomba(), "Un jugador muerto no puede colocar bombas.");
        Assert.IsFalse(jugador.IntentarColocarBomba(), "El intento de colocar bomba debe fallar si está muerto.");
    }

    [Test]
    public void JugadorMuerto_NoPuedeRecibirAturdimiento()
    {
        jugador.RecibirDaño(3);
        jugador.AplicarAturdimiento();

        Assert.IsFalse(jugador.EstaAturdido, "Un jugador muerto no debe cambiar su estado a aturdido.");
    }
}
