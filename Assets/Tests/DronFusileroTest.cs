using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class DronFusileroTest
{
    public enum EstadoFusileroPrueba
    {
        Patrulla,
        Ataque
    }

    // Mock que simula las reglas de negocio de DroneShooterAI.cs y NetworkProjectile.cs
    private class DronFusileroMock
    {
        public bool EsServidor { get; set; } = true;
        public EstadoFusileroPrueba EstadoActual { get; private set; } = EstadoFusileroPrueba.Patrulla;
        public float VelocidadPatrulla { get; set; } = 3.5f;
        public float VelocidadAtaque { get; set; } = 1.5f;
        public float VelocidadActual { get; private set; } = 3.5f;
        public float RangoVision { get; set; } = 12f;
        public float CadenciaDisparo { get; set; } = 1f;
        public float TemporizadorDisparo { get; private set; } = 0f;
        public bool JugadorEnVista { get; set; } = false;
        public bool ObstaculoEnLineaDeVision { get; set; } = false;
        public float AnguloHaciaJugador { get; set; } = 0f; // Grados entre la mira y el objetivo
        public bool ProyectilInstanciado { get; private set; } = false;
        public int VidasJugador { get; set; } = 3;
        public bool EstaDestruido { get; private set; } = false;

        public DronFusileroMock(bool esServidor = true)
        {
            EsServidor = esServidor;
        }

        public void EvaluarLineaDeVision()
        {
            if (!EsServidor || EstaDestruido) return;

            // Criterio: detecta al jugador si está en rango y sin obstáculos intermedios
            if (JugadorEnVista && !ObstaculoEnLineaDeVision)
            {
                if (EstadoActual == EstadoFusileroPrueba.Patrulla)
                {
                    EstadoActual = EstadoFusileroPrueba.Ataque;
                    VelocidadActual = VelocidadAtaque; // Se frena/ralentiza para disparar
                }
            }
            else
            {
                if (EstadoActual == EstadoFusileroPrueba.Ataque)
                {
                    EstadoActual = EstadoFusileroPrueba.Patrulla;
                    VelocidadActual = VelocidadPatrulla; // Reanuda velocidad de patrulla
                }
            }
        }

        public bool IntentarDisparar(float deltaTime)
        {
            if (!EsServidor || EstaDestruido || EstadoActual != EstadoFusileroPrueba.Ataque) return false;

            // Regla de tolerabilidad de angulo <= 10 grados para gatillar disparo
            if (AnguloHaciaJugador > 10f)
            {
                return false;
            }

            TemporizadorDisparo -= deltaTime;
            if (TemporizadorDisparo <= 0f)
            {
                ProyectilInstanciado = true;
                TemporizadorDisparo = CadenciaDisparo;
                return true;
            }

            return false;
        }

        public void ResetearEstadoDisparo()
        {
            ProyectilInstanciado = false;
        }

        public void ImpactarProyectilEnJugador()
        {
            if (VidasJugador > 0)
            {
                VidasJugador -= 1; // Resta 1 vida al jugador
            }
        }

        public void RecibirDañoPorBomba()
        {
            if (!EsServidor) return;
            EstaDestruido = true;
        }
    }

    private DronFusileroMock dronServidor;
    private DronFusileroMock dronCliente;

    [SetUp]
    public void SetUp()
    {
        dronServidor = new DronFusileroMock(esServidor: true);
        dronCliente = new DronFusileroMock(esServidor: false);
    }

    [Test]
    public void Dron_IniciaEnEstadoPatrullaYVelocidadBase()
    {
        Assert.AreEqual(EstadoFusileroPrueba.Patrulla, dronServidor.EstadoActual, "El dron fusilero debe iniciar en estado Patrulla.");
        Assert.AreEqual(3.5f, dronServidor.VelocidadActual, "La velocidad inicial debe corresponder a la velocidad de patrulla.");
    }

    [Test]
    public void Dron_CambiaAEstadoAtaqueYReduceVelocidadAlDetectarJugador()
    {
        dronServidor.JugadorEnVista = true;
        dronServidor.ObstaculoEnLineaDeVision = false;

        dronServidor.EvaluarLineaDeVision();

        Assert.AreEqual(EstadoFusileroPrueba.Ataque, dronServidor.EstadoActual, "El dron debe cambiar al estado Ataque al ver al jugador.");
        Assert.AreEqual(1.5f, dronServidor.VelocidadActual, "El dron debe reducir su velocidad al entrar en modo de ataque.");
    }

    [Test]
    public void Dron_ObstaculoBloqueaVisionYMantieneEstadoPatrulla()
    {
        dronServidor.JugadorEnVista = true;
        dronServidor.ObstaculoEnLineaDeVision = true; // Pared o caja en el camino

        dronServidor.EvaluarLineaDeVision();

        Assert.AreEqual(EstadoFusileroPrueba.Patrulla, dronServidor.EstadoActual, "Un obstáculo debe impedir que el dron pase a modo ataque.");
    }

    [Test]
    public void Dron_ReanudaPatrullaAlPerderDeVistaAlJugador()
    {
        // Forzar entrada a modo ataque
        dronServidor.JugadorEnVista = true;
        dronServidor.EvaluarLineaDeVision();

        // El jugador se esconde
        dronServidor.JugadorEnVista = false;
        dronServidor.EvaluarLineaDeVision();

        Assert.AreEqual(EstadoFusileroPrueba.Patrulla, dronServidor.EstadoActual, "El dron debe volver a patrullar si pierde de vista al jugador.");
        Assert.AreEqual(3.5f, dronServidor.VelocidadActual, "El dron debe recuperar su velocidad normal de patrulla.");
    }

    [Test]
    public void Dron_CancelaDisparoSiElAnguloAlineacionEsMayorADiezGrados()
    {
        dronServidor.JugadorEnVista = true;
        dronServidor.EvaluarLineaDeVision();
        dronServidor.AnguloHaciaJugador = 15f; // Fuera del margen de 10 grados

        bool disparoRealizado = dronServidor.IntentarDisparar(1.1f);

        Assert.IsFalse(disparoRealizado, "El dron no debe disparar si no está correctamente alineado con el objetivo.");
        Assert.IsFalse(dronServidor.ProyectilInstanciado, "No debe instanciarse ningún proyectil si la mira está desviada.");
    }

    [Test]
    public void Dron_DisparaProyectilAlEstarAlineadoYCumplirCooldown()
    {
        dronServidor.JugadorEnVista = true;
        dronServidor.EvaluarLineaDeVision();
        dronServidor.AnguloHaciaJugador = 3f; // Alineado dentro del margen

        bool disparoRealizado = dronServidor.IntentarDisparar(1.1f); // Supera el cooldown de 1s

        Assert.IsTrue(disparoRealizado, "El dron debe efectuar el disparo estando alineado y con el cooldown listo.");
        Assert.IsTrue(dronServidor.ProyectilInstanciado, "Debe instanciarse el proyectil correspondiente.");
    }

    [Test]
    public void Proyectil_RestaUnaVidaAlJugadorAlImpactar()
    {
        int vidasIniciales = dronServidor.VidasJugador;
        
        dronServidor.ImpactarProyectilEnJugador();

        Assert.AreEqual(vidasIniciales - 1, dronServidor.VidasJugador, "El proyectil del fusilero debe quitar exactamente 1 vida al jugador.");
    }

    [Test]
    public void Dron_EsDestruidoAlRecibirImpactoDeBomba()
    {
        dronServidor.RecibirDañoPorBomba();

        Assert.IsTrue(dronServidor.EstaDestruido, "El dron fusilero debe ser destruido al recibir el impacto de una bomba.");
    }
}
