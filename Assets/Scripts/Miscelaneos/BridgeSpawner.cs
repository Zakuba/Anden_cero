using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BridgeSpawner : MonoBehaviour
{
    [System.Serializable]
    public class BridgePoint
    {
        [Header("Posición del puente")]
        public Vector3 posicion;

        [Header("Activar este punto")]
        public bool activo = true;

        [HideInInspector]
        public GameObject puenteActual;

        [HideInInspector]
        public bool respawnEnCurso = false;
    }

    [Header("Objetos posibles")]
    [SerializeField]
    private List<GameObject> bridgePrefabs =
        new List<GameObject>();

    [Header("Cantidad de puentes iniciales")]
    [SerializeField] private int cantidad = 5;

    [Header("Alturas")]
    [SerializeField] private float alturaSpawn = 8f;
    [SerializeField] private float alturaFinal = 0f;

    [Header("Caída")]
    [SerializeField] private float velocidadCaida = 5f;

    [SerializeField] private float tiempoEntreCaidas = 3f;

    [Header("Respawn")]
    [SerializeField] private float tiempoRespawn = 5f;

    [Header("Impacto")]
    [SerializeField] private float fuerzaRebote = 0.15f;
    [SerializeField] private float duracionImpacto = 0.15f;
    [SerializeField] private float escalaImpacto = 0.05f;

    [Header("Puntos donde pueden caer")]
    [SerializeField]
    private List<BridgePoint> puntosCaida =
        new List<BridgePoint>();

    [Header("Opciones")]
    [SerializeField] private bool comenzarAutomaticamente = true;

    [Header("Gizmos")]
    [SerializeField] private float tamanoGizmo = 0.5f;

    private List<GameObject> puentesSpawneados =
        new List<GameObject>();

    private Coroutine colaSpawnCoroutine;

    private void Start()
    {
        if (comenzarAutomaticamente)
        {
            IniciarSpawner();
        }
    }

    private void Update()
    {
        LimpiarPuentesDestruidos();
    }

    // =========================================================
    // INICIO
    // =========================================================

    private IEnumerator SpawnInicial()
    {
        if (bridgePrefabs.Count == 0)
        {
            Debug.LogWarning(
                "BridgeSpawner: No hay prefabs de puentes asignados."
            );

            yield break;
        }

        List<BridgePoint> puntosDisponibles =
            ObtenerPuntosDisponibles();

        MezclarLista(puntosDisponibles);

        int puentesCreados = 0;

        foreach (BridgePoint punto in puntosDisponibles)
        {
            if (puentesCreados >= cantidad)
                break;

            if (punto.puenteActual != null)
                continue;

            CrearPuente(punto);

            puentesCreados++;

            // Esperar antes de crear el siguiente
            if (puentesCreados < cantidad)
            {
                yield return new WaitForSeconds(
                    tiempoEntreCaidas
                );
            }
        }
    }

    // =========================================================
    // RESPawn
    // =========================================================

    private IEnumerator ColaRespawn()
    {
        while (true)
        {
            BridgePoint puntoParaRespawn = null;

            // Buscar un único punto destruido
            for (int i = 0; i < puntosCaida.Count; i++)
            {
                BridgePoint punto =
                    puntosCaida[i];

                if (!punto.activo)
                    continue;

                if (punto.puenteActual == null &&
                    punto.respawnEnCurso)
                {
                    puntoParaRespawn = punto;
                    break;
                }
            }

            // Si encontramos uno, esperar su tiempo
            if (puntoParaRespawn != null)
            {
                yield return new WaitForSeconds(
                    tiempoRespawn
                );

                if (puntoParaRespawn != null &&
                    puntoParaRespawn.activo &&
                    puntoParaRespawn.puenteActual == null)
                {
                    puntoParaRespawn.respawnEnCurso =
                        false;

                    CrearPuente(
                        puntoParaRespawn
                    );
                }

                // Esperar antes de procesar el siguiente
                yield return new WaitForSeconds(
                    tiempoEntreCaidas
                );

                continue;
            }

            // No hay nada esperando
            yield return null;
        }
    }

    // =========================================================
    // DETECTAR PUENTES DESTRUIDOS
    // =========================================================

    private void LimpiarPuentesDestruidos()
    {
        for (int i = 0; i < puntosCaida.Count; i++)
        {
            BridgePoint punto =
                puntosCaida[i];

            if (!punto.activo)
                continue;

            // Si ya hay puente, no hacemos nada
            if (punto.puenteActual != null)
                continue;

            // Si ya está esperando respawn, no duplicamos
            if (punto.respawnEnCurso)
                continue;

            // Marcarlo para la cola
            punto.respawnEnCurso = true;
        }
    }

    // =========================================================
    // CREAR PUENTE
    // =========================================================

    private void CrearPuente(BridgePoint punto)
    {
        if (punto == null)
            return;

        if (punto.puenteActual != null)
            return;

        if (bridgePrefabs.Count == 0)
            return;

        int indiceAleatorio =
            Random.Range(
                0,
                bridgePrefabs.Count
            );

        GameObject prefabElegido =
            bridgePrefabs[indiceAleatorio];

        if (prefabElegido == null)
        {
            Debug.LogWarning(
                "BridgeSpawner: Uno de los prefabs de la lista es NULL."
            );

            return;
        }

        Vector3 posicion =
            punto.posicion;

        posicion.y =
            alturaSpawn;

        GameObject nuevoPuente =
            Instantiate(
                prefabElegido,
                posicion,
                prefabElegido.transform.rotation
            );

        punto.puenteActual =
            nuevoPuente;

        puentesSpawneados.Add(
            nuevoPuente
        );

        StartCoroutine(
            CaerPuente(
                nuevoPuente
            )
        );
    }

    // =========================================================
    // CAÍDA
    // =========================================================

    private IEnumerator CaerPuente(
        GameObject puente
    )
    {
        while (
            puente != null &&
            puente.transform.position.y >
            alturaFinal
        )
        {
            Vector3 posicion =
                puente.transform.position;

            posicion.y -=
                velocidadCaida *
                Time.deltaTime;

            if (posicion.y < alturaFinal)
            {
                posicion.y =
                    alturaFinal;
            }

            puente.transform.position =
                posicion;

            yield return null;
        }

        if (puente == null)
            yield break;

        Vector3 posicionFinal =
            puente.transform.position;

        posicionFinal.y =
            alturaFinal;

        puente.transform.position =
            posicionFinal;

        yield return StartCoroutine(
            EfectoImpacto(
                puente
            )
        );
    }

    // =========================================================
    // IMPACTO
    // =========================================================

    private IEnumerator EfectoImpacto(
        GameObject puente
    )
    {
        if (puente == null)
            yield break;

        Transform objeto =
            puente.transform;

        Vector3 posicionOriginal =
            objeto.position;

        float tiempo = 0f;

        while (
            tiempo <
            duracionImpacto
        )
        {
            if (puente == null)
                yield break;

            tiempo +=
                Time.deltaTime;

            float progreso =
                tiempo /
                duracionImpacto;

            float intensidad =
                1f - progreso;

            float temblorX =
                Random.Range(-1f, 1f) *
                fuerzaRebote *
                intensidad;

            float temblorY =
                Random.Range(-1f, 1f) *
                fuerzaRebote *
                intensidad;

            float temblorZ =
                Random.Range(-1f, 1f) *
                fuerzaRebote *
                intensidad;

            objeto.position =
                posicionOriginal +
                new Vector3(
                    temblorX,
                    temblorY,
                    temblorZ
                );

            yield return null;
        }

        if (puente != null)
        {
            objeto.position =
                posicionOriginal;
        }
    }

    // =========================================================
    // UTILIDADES
    // =========================================================

    private List<BridgePoint> ObtenerPuntosDisponibles()
    {
        List<BridgePoint> disponibles =
            new List<BridgePoint>();

        foreach (BridgePoint punto in puntosCaida)
        {
            if (punto.activo &&
                punto.puenteActual == null)
            {
                disponibles.Add(punto);
            }
        }

        return disponibles;
    }

    private void MezclarLista(
        List<BridgePoint> lista
    )
    {
        for (int i = 0; i < lista.Count; i++)
        {
            int indiceAleatorio =
                Random.Range(
                    i,
                    lista.Count
                );

            BridgePoint temporal =
                lista[i];

            lista[i] =
                lista[indiceAleatorio];

            lista[indiceAleatorio] =
                temporal;
        }
    }

    // =========================================================
    // CONTROL EXTERNO
    // =========================================================

    public void IniciarSpawner()
    {
        if (colaSpawnCoroutine != null)
        {
            StopCoroutine(
                colaSpawnCoroutine
            );
        }

        foreach (BridgePoint punto in puntosCaida)
        {
            punto.puenteActual = null;
            punto.respawnEnCurso = false;
        }

        // Spawn inicial
        StartCoroutine(
            SpawnInicial()
        );

        // Cola permanente de respawn
        colaSpawnCoroutine =
            StartCoroutine(
                ColaRespawn()
            );
    }

    public void EliminarPuentes()
    {
        for (
            int i = puentesSpawneados.Count - 1;
            i >= 0;
            i--
        )
        {
            if (
                puentesSpawneados[i] != null
            )
            {
                Destroy(
                    puentesSpawneados[i]
                );
            }
        }

        puentesSpawneados.Clear();

        foreach (BridgePoint punto in puntosCaida)
        {
            punto.puenteActual = null;
            punto.respawnEnCurso = false;
        }
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmos()
    {
        if (puntosCaida == null)
            return;

        foreach (BridgePoint punto in puntosCaida)
        {
            if (!punto.activo)
                continue;

            Vector3 posicionFinal =
                punto.posicion;

            posicionFinal.y =
                alturaFinal;

            Vector3 posicionSpawn =
                punto.posicion;

            posicionSpawn.y =
                alturaSpawn;

            Gizmos.DrawLine(
                posicionSpawn,
                posicionFinal
            );

            Gizmos.DrawWireSphere(
                posicionSpawn,
                tamanoGizmo
            );

            Gizmos.DrawWireCube(
                posicionFinal,
                Vector3.one *
                tamanoGizmo
            );
        }
    }
}