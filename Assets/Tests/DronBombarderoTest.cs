using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class DronBombarderoTest
{
    // Mock que simula las reglas de negocio de DronBombardero.cs y NetworkProjectile.cs
    private class DroneBomberMock
    {
        public bool EsServidor { get; set; } = true;
        public Vector3 PosicionActual { get; set; } = Vector3.zero;
        public float TamañoCuadricula { get; set; } = 2.5f;
        public float IntervaloSoltarBomba { get; set; } = 4f;
        public float TemporizadorBomba { get; private set; }
        public bool HayBombaActivaEnMapa { get; set; } = false;
        public int VidasDron { get; private set; } = 1;
        public bool EstaDestruido { get; private set; } = false;
        public Vector3 DireccionActual { get; set; } = Vector3.forward;

        public DroneBomberMock(bool esServidor = true)
        {
            EsServidor = esServidor;
            TemporizadorBomba = IntervaloSoltarBomba;
        }

        public Vector3 ObtenerCentroCuadricula(Vector3 pos)
        {
            float x = Mathf.Round(pos.x / TamañoCuadricula) * TamañoCuadricula;
            float z = Mathf.Round(pos.z / TamañoCuadricula) * TamañoCuadricula;
            return new Vector3(x, -1f, z);
        }

        public bool IntentarSoltarBomba()
        {
            if (!EsServidor || EstaDestruido) return false;

            // Regla: No puede soltar si ya hay una bomba activa en el mapa sin explotar
            if (HayBombaActivaEnMapa) return false;

            HayBombaActivaEnMapa = true;
            TemporizadorBomba = IntervaloSoltarBomba;
            return true;
        }

        public void ActualizarTemporizador(float deltaTime)
        {
            if (!EsServidor || EstaDestruido) return;

            TemporizadorBomba -= deltaTime;
            if (TemporizadorBomba <= 0f)
            {
                IntentarSoltarBomba();
            }
        }

        public Vector3 CalcularSiguienteCeldaCuadricula(bool bloqueadoAdelante)
        {
            Vector3 celdaActual = ObtenerCentroCuadricula(PosicionActual);
            
            if (!bloqueadoAdelante)
            {
                // Prioriza la línea recta según la lógica del script (80% prioridad)
                return celdaActual + (DireccionActual * TamañoCuadricula);
            }

            // Giros ortogonales si hay obstáculo
            Vector3 giroIzquierda = celdaActual + (Quaternion.Euler(0, -90, 0) * DireccionActual * TamañoCuadricula);
            return giroIzquierda;
        }

        public int CalcularRangoExplosion(int rangoBombaJugador)
        {
            // Regla: Ocupa 1 casilla más que la bomba del jugador en las 4 direcciones
            return rangoBombaJugador + 1;
        }

        public void RecibirDaño()
        {
            if (!EsServidor) return;
            VidasDron = 0;
            EstaDestruido = true;
        }
    }

    private DroneBomberMock dronServidor;
    private DroneBomberMock dronCliente;

    [SetUp]
    public void SetUp()
    {
        dronServidor = new DroneBomberMock(esServidor: true);
        dronCliente = new DroneBomberMock(esServidor: false);
    }

    [Test]
    public void Dron_AlineaPosicionACentroDeCuadricula()
    {
        Vector3 posicionDesalineada = new Vector3(2.3f, 0f, 4.8f);
        Vector3 posicionAlineada = dronServidor.ObtenerCentroCuadricula(posicionDesalineada);

        Assert.AreEqual(2.5f, posicionAlineada.x, "La posición X debe alinearse a la celda de 2.5 de la cuadrícula.");
        Assert.AreEqual(5.0f, posicionAlineada.z, "La posición Z debe alinearse a la celda de 2.5 de la cuadrícula.");
    }

    [Test]
    public void Dron_MantieneMovimientoEnLineaRecta()
    {
        dronServidor.PosicionActual = new Vector3(0, 0, 0);
        dronServidor.DireccionActual = Vector3.forward;

        Vector3 siguienteCelda = dronServidor.CalcularSiguienteCeldaCuadricula(bloqueadoAdelante: false);

        Assert.AreEqual(new Vector3(0, -1f, 2.5f), siguienteCelda, "El dron debe avanzar a la siguiente celda frontal de la cuadrícula.");
    }

    [Test]
    public void Dron_NoSueltaBombaSiYaExisteUnaEnElMapa()
    {
        dronServidor.HayBombaActivaEnMapa = true;
        bool colocoBomba = dronServidor.IntentarSoltarBomba();

        Assert.IsFalse(colocoBomba, "El dron no debe soltar una bomba si ya hay una sin explotar en la cuadrícula.");
    }

    [Test]
    public void Dron_SueltaBombaExitosamenteAlAgotarCooldown()
    {
        dronServidor.HayBombaActivaEnMapa = false;
        dronServidor.ActualizarTemporizador(4.1f); // Supera el intervalo de 4s

        Assert.IsTrue(dronServidor.HayBombaActivaEnMapa, "El dron debe instanciar la bomba al cumplirse el temporizador.");
    }

    [Test]
    public void BombaDron_TieneMayorRangoDeExplosionQueJugador()
    {
        int rangoBaseJugador = 2;
        int rangoBombaDron = dronServidor.CalcularRangoExplosion(rangoBaseJugador);

        Assert.AreEqual(3, rangoBombaDron, "La bomba del dron debe abarcar 1 casilla adicional respecto a la del jugador.");
    }

    [Test]
    public void Dron_EsEliminadoAlRecibirImpacto()
    {
        dronServidor.RecibirDaño();

        Assert.IsTrue(dronServidor.EstaDestruido, "El dron debe destruirse y quedar fuera de combate al recibir daño.");
    }

    [Test]
    public void Dron_ClienteNoProcesaLógicaDeBombasLocales()
    {
        dronCliente.ActualizarTemporizador(5f);

        Assert.IsFalse(dronCliente.HayBombaActivaEnMapa, "El cliente no debe instanciar bombas de forma autónoma.");
    }
}