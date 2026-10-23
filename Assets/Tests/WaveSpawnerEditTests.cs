using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// US: Spawner de oleadas (Supervivencia).
/// Clase de tests de EditMode AUTOCONTENIDA: no depende de ningun script del juego
/// (igual que los otros tests del proyecto, trabaja con copias/mocks de la logica).
///
///  - WaveCalculator (abajo) es una COPIA de Assets/Oleadas/WaveCalculator.cs.
///  - WaveFlowSim es un simulador de la maquina de estados de WaveManager
///    (Idle -> Rest -> Combat -> Rest ... -> Finished) con un reloj falso.
///
/// Si cambia la logica real, hay que actualizar la copia/simulador.
/// </summary>
public class WaveSpawnerEditTests
{
    // ==================================================================
    //  COPIA de WaveCalculator.cs (Assets/Oleadas)
    // ==================================================================
    private static class WaveCalculator
    {
        public static int TotalEnemies(int wave, int baseDrones, int increment)
        {
            wave = Math.Max(1, wave);
            return Math.Max(1, baseDrones + (wave - 1) * increment);
        }

        /// <summary>
        /// Tope de drones vivos al mismo tiempo, según los jugadores vivos.
        /// Con capPerPlayer = 0 el tope es fijo (capBase).
        /// </summary>
        public static int MaxAlive(int capBase, int capPerPlayer, int alivePlayers)
        {
            alivePlayers = Math.Max(1, alivePlayers);
            return Math.Max(1, capBase + capPerPlayer * alivePlayers);
        }

        /// <summary>
        /// Reparte el total entre Fusileros y Bombarderos. La proporción de
        /// Fusileros crece con la oleada hasta ratioMax. Si hay 2 o más drones,
        /// siempre hay al menos uno de cada tipo.
        /// </summary>
        public static void Composition(
            int total, int wave,
            float ratioStart, float ratioStep, float ratioMax,
            out int shooters, out int bombers)
        {
            total = Math.Max(0, total);
            wave = Math.Max(1, wave);

            float ratio = ratioStart + (wave - 1) * ratioStep;
            ratio = Math.Min(ratio, ratioMax);
            ratio = Math.Max(0f, Math.Min(1f, ratio));

            shooters = (int)Math.Round((double)(total * ratio), MidpointRounding.AwayFromZero);

            if (total >= 2)
                shooters = Math.Max(1, Math.Min(shooters, total - 1));
            else
                shooters = Math.Min(shooters, total);

            bombers = total - shooters;
        }


        public static bool HasHealer(int wave, int firstWave, int everyN)
        {
            if (firstWave < 1 || everyN < 1) return false;
            return wave >= firstWave && (wave - firstWave) % everyN == 0;
        }

        /// <summary>
        /// Multiplicador de velocidad de los drones: 1 en la oleada 1 y sube
        /// perWave por oleada hasta maxMultiplier.
        /// </summary>
        public static float SpeedMultiplier(int wave, float perWave, float maxMultiplier)
        {
            wave = Math.Max(1, wave);
            float value = 1f + (wave - 1) * perWave;
            return Math.Min(value, Math.Max(1f, maxMultiplier));
        }
    }

    // ==================================================================
    //  TESTS DEL CALCULO DE OLEADAS
    // ==================================================================
    // ------------------------------------------------------------------
    //  Cantidad de drones por oleada
    // ------------------------------------------------------------------

    [TestCase(1, 3)]
    [TestCase(2, 5)]
    [TestCase(10, 21)]
    public void Cantidad_CadaOleadaSumaElIncremento(int oleada, int esperado)
    {
        Assert.AreEqual(esperado, WaveCalculator.TotalEnemies(oleada, 3, 2));
    }

    [Test]
    public void Cantidad_OleadaCeroONegativa_SeTrataComoOleadaUno()
    {
        Assert.AreEqual(3, WaveCalculator.TotalEnemies(0, 3, 2));
        Assert.AreEqual(3, WaveCalculator.TotalEnemies(-5, 3, 2));
    }

    [Test]
    public void Cantidad_NuncaEsMenorAUnDron_AunConValoresNegativos()
    {
        Assert.AreEqual(1, WaveCalculator.TotalEnemies(1, 0, 2));
        Assert.AreEqual(1, WaveCalculator.TotalEnemies(10, 3, -2));
    }

    [Test]
    public void Cantidad_Estres_1000OleadasSumanSiempreElMismoIncremento()
    {
        for (int w = 1; w < 1000; w++)
            Assert.AreEqual(2, WaveCalculator.TotalEnemies(w + 1, 3, 2) - WaveCalculator.TotalEnemies(w, 3, 2), "Oleada " + w);
    }

    // ------------------------------------------------------------------
    //  Tope de drones vivos
    // ------------------------------------------------------------------

    [TestCase(1, 6)]
    [TestCase(4, 12)]
    public void Tope_EscalaConLosJugadoresVivos(int vivos, int esperado)
    {
        Assert.AreEqual(esperado, WaveCalculator.MaxAlive(4, 2, vivos));
    }

    [Test]
    public void Tope_ConCeroJugadoresVivos_SeCalculaComoUnJugador()
    {
        Assert.AreEqual(6, WaveCalculator.MaxAlive(4, 2, 0));
    }

    [Test]
    public void Tope_JugadoresMuerenYNuncaRevivem_ElTopeSoloBaja()
    {
        int[] vivos = { 4, 3, 2, 1, 0 };
        int[] esperado = { 12, 10, 8, 6, 6 };
        int anterior = int.MaxValue;

        for (int i = 0; i < vivos.Length; i++)
        {
            int tope = WaveCalculator.MaxAlive(4, 2, vivos[i]);
            Assert.AreEqual(esperado[i], tope);
            Assert.LessOrEqual(tope, anterior);
            anterior = tope;
        }
    }

    // ------------------------------------------------------------------
    //  Composicion: fusileros / bombarderos
    // ------------------------------------------------------------------

    [Test]
    public void Composicion_Oleada1_UnFusileroYDosBombarderos()
    {
        int fusileros, bombarderos;
        WaveCalculator.Composition(3, 1, 0.34f, 0.03f, 0.6f, out fusileros, out bombarderos);

        Assert.AreEqual(1, fusileros);
        Assert.AreEqual(2, bombarderos);
    }

    [TestCase(0, 1, 0, 0)]
    [TestCase(2, 1, 1, 1)]
    [TestCase(21, 10, 13, 8)]
    public void Composicion_ValoresConocidos(int total, int oleada, int fusilerosEsp, int bombarderosEsp)
    {
        int fusileros, bombarderos;
        WaveCalculator.Composition(total, oleada, 0.34f, 0.03f, 0.6f, out fusileros, out bombarderos);

        Assert.AreEqual(fusilerosEsp, fusileros);
        Assert.AreEqual(bombarderosEsp, bombarderos);
    }

    [TestCase(1, 34)]
    [TestCase(10, 60)]
    [TestCase(50, 60)]
    public void Composicion_ProporcionDeFusilerosCreceHastaElMaximo(int oleada, int fusilerosEsperados)
    {
        int fusileros, bombarderos;
        WaveCalculator.Composition(100, oleada, 0.34f, 0.03f, 0.6f, out fusileros, out bombarderos);

        Assert.AreEqual(fusilerosEsperados, fusileros);
        Assert.AreEqual(100 - fusilerosEsperados, bombarderos);
    }

    [Test]
    public void Composicion_Estres_SiempreSumaElTotalYHayUnoDeCadaTipo()
    {
        for (int oleada = 1; oleada <= 60; oleada++)
        for (int total = 0; total <= 150; total++)
        {
            int f, b;
            WaveCalculator.Composition(total, oleada, 0.34f, 0.03f, 0.6f, out f, out b);

            Assert.AreEqual(total, f + b, "La suma no coincide. total=" + total + " oleada=" + oleada);
            Assert.GreaterOrEqual(f, 0);
            Assert.GreaterOrEqual(b, 0);
            if (total >= 2)
            {
                Assert.GreaterOrEqual(f, 1, "Falta fusilero. total=" + total);
                Assert.GreaterOrEqual(b, 1, "Falta bombardero. total=" + total);
            }
        }
    }

    // ------------------------------------------------------------------
    //  Dron Sanador y velocidad
    // ------------------------------------------------------------------

    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(5, true)]
    public void Sanador_AparecePorDefectoEnLasOleadas2_5_8(int oleada, bool esperado)
    {
        Assert.AreEqual(esperado, WaveCalculator.HasHealer(oleada, 2, 3));
    }

    [Test]
    public void Sanador_ConfiguracionEnCero_NuncaAparece()
    {
        Assert.IsFalse(WaveCalculator.HasHealer(5, 0, 3), "firstWave = 0 significa 'nunca'.");
        Assert.IsFalse(WaveCalculator.HasHealer(5, 2, 0), "everyN = 0 significa 'nunca'.");
    }

    [TestCase(1, 1.0f)]
    [TestCase(2, 1.05f)]
    [TestCase(100, 1.6f)]
    public void Velocidad_SubeCincoPorCientoPorOleadaHastaElMaximo(int oleada, float esperado)
    {
        Assert.AreEqual(esperado, WaveCalculator.SpeedMultiplier(oleada, 0.05f, 1.6f), 0.0001f);
    }

    [Test]
    public void Velocidad_Estres_NuncaBajaNiSuperaElMaximo()
    {
        float anterior = 0f;
        for (int w = 1; w <= 1000; w++)
        {
            float v = WaveCalculator.SpeedMultiplier(w, 0.05f, 1.6f);
            Assert.GreaterOrEqual(v, 1f);
            Assert.LessOrEqual(v, 1.6f);
            Assert.GreaterOrEqual(v, anterior);
            anterior = v;
        }
    }

    [Test]
    public void US_CadaOleadaSiguienteTieneMasDronesOMasVelocidad()
    {
        for (int w = 1; w < 100; w++)
        {
            bool masDrones = WaveCalculator.TotalEnemies(w + 1, 3, 2) > WaveCalculator.TotalEnemies(w, 3, 2);
            bool masVelocidad = WaveCalculator.SpeedMultiplier(w + 1, 0.05f, 1.6f) > WaveCalculator.SpeedMultiplier(w, 0.05f, 1.6f);
            Assert.IsTrue(masDrones || masVelocidad, "La oleada " + (w + 1) + " no es mas dificil.");
        }
    }

    // ==================================================================
    //  SIMULADOR DE WaveManager (apoyo de los tests de flujo)
    // ==================================================================
    // ------------------------------------------------------------------
    //  Tipos auxiliares
    // ------------------------------------------------------------------
    private enum Fase { Idle, Rest, Combat, Finished }
    private enum TipoDron { Fusilero, Bombardero, Sanador }

    private class Dron
    {
        public int Id;
        public TipoDron Tipo;
        public bool CuentaParaOleada { get { return Tipo != TipoDron.Sanador; } }
    }

    private class Punto
    {
        public float X, Z;
        public bool EnNavMesh = true;
        public Punto(float x, float z, bool enNavMesh = true) { X = x; Z = z; EnNavMesh = enNavMesh; }
    }

    private class Jugador
    {
        public float X, Z;
        public bool Vivo = true;
        public Jugador(float x = 0f, float z = 0f) { X = x; Z = z; }
    }

    private class RegistroSpawn
    {
        public int Oleada;
        public TipoDron Tipo;
        public float MultVelocidad;
        public int PuntoIdx;
        public int VivosAntes;     // -1 si es sanador
        public int TopeEnMomento;  // -1 si es sanador
        public float Tiempo;
    }

    // ------------------------------------------------------------------
    //  Simulador de WaveManager (solo logica del servidor)
    // ------------------------------------------------------------------
    private class WaveFlowSim
    {
        // Configuracion (valores por defecto de WaveManager; spawnInterval 0.5 para usar dt binarios exactos)
        public int BaseDrones = 3, Incremento = 2, TopeBase = 4, TopePorJugador = 2;
        public float IntervaloSpawn = 0.5f;
        public float RatioIni = 0.34f, RatioPaso = 0.03f, RatioMax = 0.6f;
        public int SanadorPrimeraOleada = 2, SanadorCadaN = 3;
        public float RetrasoPrimeraOleada = 5f, Descanso = 8f;
        public float VelPorOleada = 0.05f, VelMax = 1.6f;
        public float DistMinJugadores = 14f;
        public bool HayPrefabFusilero = true, HayPrefabBombardero = true, HayPrefabSanador = true;
        public bool EsServidor = true;
        public bool PartidaEnCurso = false; // MatchManager.IsMatchRunning

        public List<Punto> Puntos = new List<Punto>();
        public List<Jugador> Jugadores = new List<Jugador>();
        public Random Rng = new Random(12345);

        // Estado replicado
        public Fase Estado { get; private set; }
        public int OleadaActual { get; private set; }
        public int EnemigosVivos { get; private set; }
        public int EnemigosRestantes { get; private set; }
        public float FinDescanso { get { return restEnd; } }
        public float Tiempo { get; private set; }

        // Estado de servidor
        private readonly Queue<TipoDron> cola = new Queue<TipoDron>();
        private readonly HashSet<Dron> activos = new HashSet<Dron>();
        private readonly HashSet<Dron> sanadores = new HashSet<Dron>();
        private readonly List<Punto> validos = new List<Punto>();
        private float dt, spawnTimer, restEnd, multVel = 1f;
        private bool habilitado = true, iniciada;
        private float inicioPartida, segCongelados, inicioOleadaSeg, tiempoParaLlegar;
        private int olaCongelada, siguienteId;

        public List<RegistroSpawn> Spawns = new List<RegistroSpawn>();
        public int SanadoresRetirados;
        public List<int> OleadasConSanador = new List<int>();

        public int ColaCount { get { return cola.Count; } }
        public int SanadoresActivos { get { return sanadores.Count; } }
        public bool Habilitado { get { return habilitado; } }
        public float TiempoParaLlegarALaOleada { get { return tiempoParaLlegar; } }
        public List<Dron> Activos { get { return new List<Dron>(activos); } }
        public List<Dron> ListaSanadores { get { return new List<Dron>(sanadores); } }

        public WaveFlowSim()
        {
            Estado = Fase.Idle;
            Puntos.Add(new Punto(30, 0));
            Puntos.Add(new Punto(-30, 0));
            Puntos.Add(new Punto(0, 30));
            Puntos.Add(new Punto(0, -30));
            Jugadores.Add(new Jugador(0, 0));
        }

        // ---- OnNetworkSpawn ----
        public void AlSpawnear()
        {
            if (!EsServidor) return;
            if (!Validar()) habilitado = false;
        }

        private bool Validar()
        {
            bool ok = true;
            ok &= HayPrefabBombardero;
            ok &= HayPrefabFusilero;
            if (Puntos == null || Puntos.Count == 0) ok = false;
            return ok;
        }

        // ---- Update ----
        public void Tick(float deltaTime)
        {
            Tiempo += deltaTime;
            dt = deltaTime;
            Update();
        }

        private void Update()
        {
            if (!EsServidor || !habilitado) return;

            bool running = PartidaEnCurso;
            switch (Estado)
            {
                case Fase.Idle:
                    if (running) StartRun();
                    break;
                case Fase.Rest:
                    if (!running) { FinishRun(); break; }
                    if (Tiempo >= restEnd) StartWave();
                    break;
                case Fase.Combat:
                    if (!running) { FinishRun(); break; }
                    UpdateCombat();
                    break;
            }
        }

        private void StartRun()
        {
            iniciada = true;
            inicioPartida = Tiempo;
            inicioOleadaSeg = 0f;
            tiempoParaLlegar = 0f;
            OleadaActual = 1;
            BeginRest(RetrasoPrimeraOleada);
        }

        private void BeginRest(float duracion)
        {
            Estado = Fase.Rest;
            restEnd = Tiempo + duracion;
            Refresh();
        }

        private void StartWave()
        {
            int wave = OleadaActual;
            int total = WaveCalculator.TotalEnemies(wave, BaseDrones, Incremento);
            int shooters, bombers;
            WaveCalculator.Composition(total, wave, RatioIni, RatioPaso, RatioMax, out shooters, out bombers);

            var comp = new List<TipoDron>(total);
            for (int i = 0; i < shooters; i++) comp.Add(TipoDron.Fusilero);
            for (int i = 0; i < bombers; i++) comp.Add(TipoDron.Bombardero);
            Mezclar(comp);

            cola.Clear();
            foreach (TipoDron t in comp) cola.Enqueue(t);

            multVel = WaveCalculator.SpeedMultiplier(wave, VelPorOleada, VelMax);
            spawnTimer = 0f;
            inicioOleadaSeg = Tiempo - inicioPartida;
            Estado = Fase.Combat;

            if (HayPrefabSanador && sanadores.Count == 0 &&
                WaveCalculator.HasHealer(wave, SanadorPrimeraOleada, SanadorCadaN))
            {
                if (TrySpawn(TipoDron.Sanador, -1, -1)) OleadasConSanador.Add(wave);
            }

            Refresh();
        }

        private void UpdateCombat()
        {
            spawnTimer -= dt;

            if (cola.Count > 0 && spawnTimer <= 0f)
            {
                spawnTimer = IntervaloSpawn;

                int tope = TopeActual();
                int vivosAntes = activos.Count;
                if (vivosAntes < tope && TrySpawn(cola.Peek(), vivosAntes, tope))
                {
                    cola.Dequeue();
                    Refresh();
                }
            }

            if (cola.Count == 0 && activos.Count == 0)
                EndWave();
        }

        private void EndWave()
        {
            var lista = new List<Dron>(sanadores);
            sanadores.Clear();
            SanadoresRetirados += lista.Count;

            OleadaActual++;
            BeginRest(Descanso);
        }

        private void FinishRun()
        {
            if (Estado == Fase.Finished) return;

            if (iniciada)
            {
                olaCongelada = Estado == Fase.Combat ? OleadaActual : Math.Max(0, OleadaActual - 1);
                segCongelados = Tiempo - inicioPartida;
                tiempoParaLlegar = inicioOleadaSeg;
            }

            cola.Clear();
            activos.Clear();
            sanadores.Clear();
            Estado = Fase.Finished;
            Refresh();
        }

        // MatchManager -> EndRun
        public void EndRun(out int waveReached, out float survivedSeconds)
        {
            if (EsServidor) FinishRun();
            waveReached = olaCongelada;
            survivedSeconds = segCongelados;
        }

        // WaveEnemy.OnNetworkDespawn -> NotifyEnemyDespawned
        public void NotificarDespawn(Dron d)
        {
            if (!EsServidor) return;
            activos.Remove(d);
            sanadores.Remove(d);
            Refresh();
        }

        private void Refresh()
        {
            if (!EsServidor) return;
            EnemigosVivos = activos.Count;
            EnemigosRestantes = activos.Count + cola.Count;
        }

        // ---- Spawn ----
        private bool TrySpawn(TipoDron tipo, int vivosAntes, int tope)
        {
            if (tipo == TipoDron.Sanador && !HayPrefabSanador) return false;

            int idx;
            if (!ElegirPunto(out idx)) return false;

            var d = new Dron { Id = siguienteId++, Tipo = tipo };
            if (d.CuentaParaOleada) activos.Add(d); else sanadores.Add(d);

            Spawns.Add(new RegistroSpawn
            {
                Oleada = OleadaActual,
                Tipo = tipo,
                MultVelocidad = tipo == TipoDron.Sanador ? 1f : multVel, // el sanador no se escala
                PuntoIdx = idx,
                VivosAntes = vivosAntes,
                TopeEnMomento = tope,
                Tiempo = Tiempo
            });
            return true;
        }

        public bool ElegirPunto(out int indice)
        {
            indice = -1;
            if (Puntos == null || Puntos.Count == 0) return false;

            validos.Clear();
            var indicesValidos = new List<int>();
            bool algunoEnNavMesh = false;
            float mejorDist = -1f;
            int mejorIdx = -1;

            for (int i = 0; i < Puntos.Count; i++)
            {
                Punto p = Puntos[i];
                if (p == null || !p.EnNavMesh) continue;

                algunoEnNavMesh = true;
                float dist = DistMinAJugadores(p);
                if (dist >= DistMinJugadores) indicesValidos.Add(i);
                if (dist > mejorDist) { mejorDist = dist; mejorIdx = i; }
            }

            if (!algunoEnNavMesh) return false;

            indice = indicesValidos.Count > 0
                ? indicesValidos[Rng.Next(indicesValidos.Count)]
                : mejorIdx;
            return true;
        }

        private float DistMinAJugadores(Punto p)
        {
            float min = float.PositiveInfinity;
            foreach (Jugador j in Jugadores)
            {
                if (!j.Vivo) continue;
                float dx = j.X - p.X;
                float dz = j.Z - p.Z;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d < min) min = d;
            }
            return min;
        }

        public int JugadoresVivos()
        {
            int n = 0;
            foreach (Jugador j in Jugadores) if (j.Vivo) n++;
            return n;
        }

        public int TopeActual()
        {
            return WaveCalculator.MaxAlive(TopeBase, TopePorJugador, JugadoresVivos());
        }

        private void Mezclar(List<TipoDron> lista)
        {
            for (int i = lista.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                TipoDron tmp = lista[i]; lista[i] = lista[j]; lista[j] = tmp;
            }
        }
    }

    // ------------------------------------------------------------------
    //  Helpers de test
    // ------------------------------------------------------------------
    private static WaveFlowSim CrearSim(int jugadoresVivos = 1)
    {
        var sim = new WaveFlowSim();
        sim.Jugadores.Clear();
        for (int i = 0; i < jugadoresVivos; i++) sim.Jugadores.Add(new Jugador(i, 0));
        sim.AlSpawnear();
        return sim;
    }

    /// <summary>Arranca la partida (Idle -> Rest, oleada 1).</summary>
    private static void Iniciar(WaveFlowSim sim)
    {
        sim.PartidaEnCurso = true;
        sim.Tick(0f);
    }

    private static void AvanzarHastaCombate(WaveFlowSim sim)
    {
        int guarda = 0;
        while (sim.Estado != Fase.Combat && guarda++ < 100000) sim.Tick(0.25f);
        Assert.AreEqual(Fase.Combat, sim.Estado, "No se llego a Combat.");
    }

    private static void MatarTodosLosVivos(WaveFlowSim sim)
    {
        foreach (Dron d in sim.Activos) sim.NotificarDespawn(d);
    }

    /// <summary>Juega la oleada actual matando todo cada tick hasta que pasa a Rest. Devuelve los ticks usados.</summary>
    private static int LimpiarOleadaActual(WaveFlowSim sim)
    {
        int ticks = 0;
        while (sim.Estado == Fase.Combat && ticks < 1000000)
        {
            sim.Tick(0.25f);
            MatarTodosLosVivos(sim);
            ticks++;
        }
        sim.Tick(0.25f); // deja que se evalue el fin de oleada si quedo pendiente
        return ticks;
    }

    private static void LlegarACombateEnOleada(WaveFlowSim sim, int oleada)
    {
        Iniciar(sim);
        AvanzarHastaCombate(sim);
        int guarda = 0;
        while (sim.OleadaActual < oleada && guarda++ < 1000)
        {
            LimpiarOleadaActual(sim);
            AvanzarHastaCombate(sim);
        }
        Assert.AreEqual(oleada, sim.OleadaActual);
        Assert.AreEqual(Fase.Combat, sim.Estado);
    }

    private static int Contar(WaveFlowSim sim, int oleada, TipoDron tipo)
    {
        int n = 0;
        foreach (RegistroSpawn r in sim.Spawns) if (r.Oleada == oleada && r.Tipo == tipo) n++;
        return n;
    }


    // ------------------------------------------------------------------
    //  Flujo de oleadas (simulador de WaveManager)
    // ------------------------------------------------------------------

    [Test]
    public void Inicio_EstadoIdleOleadaCeroSinEnemigos()
    {
        var sim = CrearSim();

        Assert.AreEqual(Fase.Idle, sim.Estado);
        Assert.AreEqual(0, sim.OleadaActual);
        Assert.AreEqual(0, sim.EnemigosVivos);
        Assert.AreEqual(0, sim.EnemigosRestantes);
        Assert.AreEqual(0, sim.Spawns.Count);
    }

    [Test]
    public void Idle_AlEmpezarLaPartida_PasaADescansoConOleadaUno()
    {
        var sim = CrearSim();

        Iniciar(sim);

        Assert.AreEqual(Fase.Rest, sim.Estado);
        Assert.AreEqual(1, sim.OleadaActual);
        Assert.AreEqual(5f, sim.FinDescanso, 0.0001f, "El primer descanso dura firstWaveDelay (5 s).");
    }

    [Test]
    public void Descanso_ArrancaLaOleadaExactamenteAlCumplirseElRetraso()
    {
        var sim = CrearSim();
        Iniciar(sim);

        sim.Tick(4.75f);
        sim.Tick(0.25f); // t = 5.0

        Assert.AreEqual(Fase.Combat, sim.Estado);
    }

    [Test]
    public void Tope_NuncaSeSuperaCuandoNoMuereNadie()
    {
        var sim = CrearSim(1);
        LlegarACombateEnOleada(sim, 5); // 11 drones, tope 6

        for (int i = 0; i < 400; i++) sim.Tick(0.25f);

        Assert.AreEqual(6, sim.EnemigosVivos);
        Assert.AreEqual(5, sim.ColaCount);
        Assert.AreEqual(11, sim.EnemigosRestantes);
    }

    [Test]
    public void Tope_CuandoMuereUnDronLaColaRellenaElHueco()
    {
        var sim = CrearSim(1);
        LlegarACombateEnOleada(sim, 5);
        for (int i = 0; i < 400; i++) sim.Tick(0.25f);

        sim.NotificarDespawn(sim.Activos[0]);
        Assert.AreEqual(5, sim.EnemigosVivos);
        Assert.AreEqual(10, sim.EnemigosRestantes);

        for (int i = 0; i < 4; i++) sim.Tick(0.25f);

        Assert.AreEqual(6, sim.EnemigosVivos);
        Assert.AreEqual(4, sim.ColaCount);
    }

    [Test]
    public void Tope_JugadoresMuertosNoRevivenYElTopeBajaAlSiguienteSpawn()
    {
        var sim = CrearSim(4);
        LlegarACombateEnOleada(sim, 10);
        for (int i = 0; i < 400; i++) sim.Tick(0.25f);
        Assert.AreEqual(12, sim.EnemigosVivos);

        // Mueren 3 jugadores: el tope baja de 12 a 6, y nadie revive.
        sim.Jugadores[1].Vivo = false;
        sim.Jugadores[2].Vivo = false;
        sim.Jugadores[3].Vivo = false;
        Assert.AreEqual(6, sim.TopeActual());

        for (int i = 0; i < 100; i++) sim.Tick(0.25f);
        Assert.AreEqual(12, sim.EnemigosVivos, "Los drones ya vivos no desaparecen.");
        Assert.AreEqual(9, sim.ColaCount, "No debe salir ningun dron nuevo mientras vivos >= tope.");

        // Se eliminan 7 drones: quedan 5 (< 6) y sale uno nuevo, luego se frena en 6.
        List<Dron> vivos = sim.Activos;
        for (int i = 0; i < 7; i++) sim.NotificarDespawn(vivos[i]);
        for (int i = 0; i < 100; i++) sim.Tick(0.25f);

        Assert.AreEqual(6, sim.EnemigosVivos);
        Assert.AreEqual(8, sim.ColaCount);
    }

    [Test]
    public void FinOleada_NoTerminaMientrasQuedenDronesEnCola()
    {
        var sim = CrearSim(1);
        LlegarACombateEnOleada(sim, 5); // 11 drones, tope 6
        for (int i = 0; i < 400; i++) sim.Tick(0.25f);

        MatarTodosLosVivos(sim);
        Assert.AreEqual(0, sim.EnemigosVivos);
        Assert.AreEqual(5, sim.ColaCount);

        sim.Tick(0.25f);

        Assert.AreEqual(Fase.Combat, sim.Estado, "Con 0 vivos pero drones en cola la oleada sigue.");
        Assert.AreEqual(5, sim.OleadaActual);
    }

    [Test]
    public void FinOleada_AlNoQuedarDronesPasaADescansoYSubeLaOleada()
    {
        var sim = CrearSim(1);
        LlegarACombateEnOleada(sim, 1);
        for (int i = 0; i < 20; i++) sim.Tick(0.25f);

        MatarTodosLosVivos(sim);
        sim.Tick(0.25f);

        Assert.AreEqual(Fase.Rest, sim.Estado);
        Assert.AreEqual(2, sim.OleadaActual);
        Assert.AreEqual(0, sim.EnemigosVivos);
        Assert.AreEqual(0, sim.EnemigosRestantes);
    }

    [Test]
    public void FinOleada_ElDescansoEntreOleadasDuraRestDuration()
    {
        var sim = CrearSim(1);
        LlegarACombateEnOleada(sim, 1);
        for (int i = 0; i < 20; i++) sim.Tick(0.25f);
        MatarTodosLosVivos(sim);
        sim.Tick(0.25f);
        float inicioDescanso = sim.Tiempo;

        Assert.AreEqual(inicioDescanso + 8f, sim.FinDescanso, 0.0001f);

        sim.Tick(7.75f);
        Assert.AreEqual(Fase.Rest, sim.Estado, "A los 7.75 s todavia descansa.");

        sim.Tick(0.25f);
        Assert.AreEqual(Fase.Combat, sim.Estado, "A los 8 s empieza la nueva oleada.");
        Assert.AreEqual(2, sim.OleadaActual);
    }

    [Test]
    public void Spawns_LaCantidadPorOleadaCoincideConLaFormula_Oleadas1a25()
    {
        var sim = CrearSim(4);
        Iniciar(sim);
        AvanzarHastaCombate(sim);

        for (int w = 1; w <= 25; w++)
        {
            int esperado = WaveCalculator.TotalEnemies(w, 3, 2);
            LimpiarOleadaActual(sim);
            int real = Contar(sim, w, TipoDron.Fusilero) + Contar(sim, w, TipoDron.Bombardero);
            Assert.AreEqual(esperado, real, "Cantidad incorrecta en oleada " + w);
            AvanzarHastaCombate(sim);
        }
    }

    [Test]
    public void Spawns_LaComposicionCoincideConElCalculador_Oleadas1a25()
    {
        var sim = CrearSim(4);
        Iniciar(sim);
        AvanzarHastaCombate(sim);

        for (int w = 1; w <= 25; w++)
        {
            int total = WaveCalculator.TotalEnemies(w, 3, 2);
            int sEsp, bEsp;
            WaveCalculator.Composition(total, w, 0.34f, 0.03f, 0.6f, out sEsp, out bEsp);

            LimpiarOleadaActual(sim);

            Assert.AreEqual(sEsp, Contar(sim, w, TipoDron.Fusilero), "Fusileros oleada " + w);
            Assert.AreEqual(bEsp, Contar(sim, w, TipoDron.Bombardero), "Bombarderos oleada " + w);
            AvanzarHastaCombate(sim);
        }
    }

    [Test]
    public void Spawns_LaVelocidadDeCadaDronCoincideConLaOleada()
    {
        var sim = CrearSim(4);
        Iniciar(sim);
        AvanzarHastaCombate(sim);

        for (int w = 1; w <= 20; w++)
        {
            LimpiarOleadaActual(sim);
            AvanzarHastaCombate(sim);
        }

        float esperado = 1f;
        for (int w = 1; w <= 20; w++)
        {
            esperado = WaveCalculator.SpeedMultiplier(w, 0.05f, 1.6f);
            foreach (RegistroSpawn r in sim.Spawns)
            {
                if (r.Oleada != w || r.Tipo == TipoDron.Sanador) continue;
                Assert.AreEqual(esperado, r.MultVelocidad, 0.0001f, "Velocidad incorrecta en oleada " + w);
            }
        }
    }

    [Test]
    public void Sanador_ApareceAlEmpezarLaOleadaDos_FueraDeLaColaYDelTope()
    {
        var sim = CrearSim(1);
        LlegarACombateEnOleada(sim, 2);

        Assert.AreEqual(1, sim.SanadoresActivos);
        Assert.AreEqual(0, sim.EnemigosVivos, "El sanador no cuenta como enemigo vivo.");
        Assert.AreEqual(5, sim.EnemigosRestantes, "El sanador no suma a los restantes.");
        Assert.AreEqual(5, sim.ColaCount);
    }

    [Test]
    public void Sanador_NoImpideTerminarLaOleada()
    {
        var sim = CrearSim(1);
        LlegarACombateEnOleada(sim, 2);
        for (int i = 0; i < 30; i++) sim.Tick(0.25f);
        MatarTodosLosVivos(sim);
        sim.Tick(0.25f);

        Assert.AreEqual(Fase.Rest, sim.Estado, "Con el sanador vivo la oleada igual termina.");
        Assert.AreEqual(3, sim.OleadaActual);
    }

    [Test]
    public void Puntos_SoloSeUsanLosQueCumplenLaDistanciaMinima_Estres500Sorteos()
    {
        var sim = CrearSim(1); // jugador en (0,0)
        sim.Puntos.Clear();
        sim.Puntos.Add(new Punto(30, 0));  // 0: valido
        sim.Puntos.Add(new Punto(2, 0));   // 1: demasiado cerca
        sim.Puntos.Add(new Punto(0, 40));  // 2: valido

        var usados = new HashSet<int>();
        for (int i = 0; i < 500; i++)
        {
            int idx;
            Assert.IsTrue(sim.ElegirPunto(out idx));
            Assert.AreNotEqual(1, idx, "Eligio un punto a menos de la distancia minima.");
            usados.Add(idx);
        }

        Assert.IsTrue(usados.Contains(0) && usados.Contains(2), "El sorteo debe repartir entre todos los puntos validos.");
    }

    [Test]
    public void SpawnFallido_PuntosFueraDelNavMesh_LaOleadaNoAvanzaNiSpawnea_Caracterizacion()
    {
        // CARACTERIZACION / RIESGO: si ningun punto esta sobre el NavMesh, TrySpawn falla siempre,
        // la cola nunca se vacia y la oleada queda trabada en Combat sin tiempo limite.
        var sim = CrearSim(1);
        LlegarACombateEnOleada(sim, 1);
        foreach (Punto p in sim.Puntos) p.EnNavMesh = false;

        for (int i = 0; i < 2400; i++) sim.Tick(0.25f); // 10 minutos simulados

        Assert.AreEqual(Fase.Combat, sim.Estado);
        Assert.AreEqual(1, sim.OleadaActual);
        Assert.AreEqual(0, sim.Spawns.Count);
        Assert.AreEqual(3, sim.EnemigosRestantes);
    }

    [Test]
    public void FinPartida_EnCombate_LaOleadaAlcanzadaEsLaActual()
    {
        var sim = CrearSim(1);
        LlegarACombateEnOleada(sim, 3);
        for (int i = 0; i < 10; i++) sim.Tick(0.25f);

        sim.PartidaEnCurso = false;
        sim.Tick(0.25f);

        int ola; float seg;
        sim.EndRun(out ola, out seg);

        Assert.AreEqual(Fase.Finished, sim.Estado);
        Assert.AreEqual(3, ola);
    }

    [Test]
    public void FinPartida_SeLimpianLosEnemigosYLaCola()
    {
        var sim = CrearSim(1);
        LlegarACombateEnOleada(sim, 5);
        for (int i = 0; i < 100; i++) sim.Tick(0.25f);
        Assert.Greater(sim.EnemigosVivos, 0);

        sim.PartidaEnCurso = false;
        sim.Tick(0.25f);

        Assert.AreEqual(0, sim.EnemigosVivos);
        Assert.AreEqual(0, sim.ColaCount);
        Assert.AreEqual(0, sim.EnemigosRestantes);
        Assert.AreEqual(0, sim.SanadoresActivos);
    }

    [Test]
    public void Cliente_NoEjecutaLaLogicaDeOleadas()
    {
        var sim = CrearSim(1);
        sim.EsServidor = false;
        sim.PartidaEnCurso = true;

        for (int i = 0; i < 200; i++) sim.Tick(1f);

        Assert.AreEqual(Fase.Idle, sim.Estado);
        Assert.AreEqual(0, sim.Spawns.Count);
        Assert.AreEqual(0, sim.OleadaActual);
    }

    [Test]
    public void Estres_PartidaAleatoriaDe60Oleadas_MueresYMatanAlAzar_InvariantesSeCumplen()
    {
        var sim = CrearSim(4);
        var rng = new Random(2024);
        Iniciar(sim);

        int anteriorOleada = sim.OleadaActual;
        int ticks = 0;
        while (sim.OleadaActual < 60 && ticks++ < 2000000)
        {
            sim.Tick(0.25f);

            // Los jugadores van muriendo de a poco y nunca revives (pero siempre queda uno vivo)
            if (rng.NextDouble() < 0.0005 && sim.JugadoresVivos() > 1)
            {
                foreach (Jugador j in sim.Jugadores) { if (j.Vivo) { j.Vivo = false; break; } }
            }

            foreach (Dron d in sim.Activos) if (rng.NextDouble() < 0.25) sim.NotificarDespawn(d);

            Assert.GreaterOrEqual(sim.OleadaActual, anteriorOleada, "La oleada nunca retrocede.");
            Assert.LessOrEqual(sim.OleadaActual - anteriorOleada, 1, "La oleada no salta de a mas de 1.");
            Assert.AreEqual(sim.EnemigosVivos + sim.ColaCount, sim.EnemigosRestantes);
            anteriorOleada = sim.OleadaActual;
        }

        Assert.GreaterOrEqual(sim.OleadaActual, 60, "La partida deberia poder avanzar hasta la oleada 60.");
        foreach (RegistroSpawn r in sim.Spawns)
        {
            if (r.Tipo == TipoDron.Sanador) continue;
            Assert.Less(r.VivosAntes, r.TopeEnMomento, "Spawn con el tope lleno.");
        }
    }
}