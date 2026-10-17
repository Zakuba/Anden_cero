using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class TrainSpawnerNivel2 : NetworkBehaviour
{
    [System.Serializable]
    public class Via
    {
        [Header("Configuración de la vía")]
        public Transform puntoInicio;
        public Transform puntoFinal;

        [Header("Objeto asociado")]
        public GameObject objetoAsociado;

        [Header("Punto donde desaparece el objeto")]
        public Transform puntoDesaparicion;

        [HideInInspector]
        public GameObject trenActual;

        [HideInInspector]
        public bool ocupada = false;

        [HideInInspector]
        public bool objetoActivo = false;
    }

    [Header("Trenes")]
    [SerializeField] private List<GameObject> trenPrefabs = new List<GameObject>();

    [Header("Vías")]
    [SerializeField] private List<Via> vias = new List<Via>();

    [Header("Movimiento")]
    [SerializeField] private float velocidad = 10f;

    [Header("Tiempo entre trenes")]
    [SerializeField] private float tiempoMinimo = 4f;
    [SerializeField] private float tiempoMaximo = 10f;

    [Header("FX Puente Destruido")]
    [SerializeField] private GameObject fxPuenteDestruido;

    [Header("Opciones")]
    [SerializeField] private bool iniciarAutomaticamente = true;

    private float temporizador;
    private bool spawnerActivo = false;
    private int ultimaViaUsada = -1;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // REGLA CLAVE: Solo el Servidor/Host calcula tiempos, vías y generación de trenes
        if (IsServer && iniciarAutomaticamente)
        {
            IniciarSpawner();
        }
    }

    private void Update()
    {
        // Solo el Servidor ejecuta el movimiento físico, el temporizador y la toma de decisiones
        if (!IsServer || !spawnerActivo)
            return;

        MoverTrenes();

        temporizador -= Time.deltaTime;

        if (temporizador <= 0f)
        {
            IntentarCrearTren();
            temporizador = Random.Range(tiempoMinimo, tiempoMaximo);
        }
    }

    private void MoverTrenes()
    {
        for (int i = 0; i < vias.Count; i++)
        {
            Via via = vias[i];

            if (via.trenActual == null)
            {
                via.ocupada = false;
                continue;
            }

            MoverTren(via, i);

            if (via.trenActual != null)
            {
                DetectarPuentes(via.trenActual);
                ComprobarPuntoDesaparicion(via, i);
            }
        }
    }

    private void MoverTren(Via via, int indiceVia)
    {
        GameObject tren = via.trenActual;
        Vector3 posicionActual = tren.transform.position;

        Vector3 nuevaPosicion = Vector3.MoveTowards(
            posicionActual,
            via.puntoFinal.position,
            velocidad * Time.deltaTime
        );

        tren.transform.position = nuevaPosicion;

        Vector3 direccion = via.puntoFinal.position - posicionActual;

        if (direccion != Vector3.zero)
        {
            Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion.normalized);
            tren.transform.rotation = Quaternion.Slerp(
                tren.transform.rotation,
                rotacionObjetivo,
                10f * Time.deltaTime
            );
        }

        // Si llegó al final del recorrido
        if (Vector3.Distance(tren.transform.position, via.puntoFinal.position) < 0.05f)
        {
            NetworkObject netObj = tren.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(tren);
            }

            via.trenActual = null;
            via.ocupada = false;

            if (via.objetoAsociado != null && via.objetoActivo)
            {
                SetObjetoAsociadoClientRpc(indiceVia, false);
                via.objetoActivo = false;
            }
        }
    }

    private void IntentarCrearTren()
    {
        if (trenPrefabs.Count == 0 || vias.Count == 0) return;

        List<int> viasDisponibles = new List<int>();

        for (int i = 0; i < vias.Count; i++)
        {
            Via via = vias[i];

            if (via.puntoInicio != null &&
                via.puntoFinal != null &&
                !via.ocupada &&
                i != ultimaViaUsada)
            {
                viasDisponibles.Add(i);
            }
        }

        if (viasDisponibles.Count == 0) return;

        int indiceAleatorio = Random.Range(0, viasDisponibles.Count);
        int indiceViaElegida = viasDisponibles[indiceAleatorio];
        Via viaElegida = vias[indiceViaElegida];

        CrearTrenEnVia(viaElegida, indiceViaElegida);
    }

    private void CrearTrenEnVia(Via via, int indiceVia)
    {
        int indiceTren = Random.Range(0, trenPrefabs.Count);
        GameObject trenPrefabElegido = trenPrefabs[indiceTren];

        if (trenPrefabElegido == null) return;

        // 1. Instanciar en el Servidor
        GameObject nuevoTren = Instantiate(
            trenPrefabElegido,
            via.puntoInicio.position,
            via.puntoInicio.rotation
        );

        // 2. Spawnear por red para que se replique a los clientes
        NetworkObject netObj = nuevoTren.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }

        via.trenActual = nuevoTren;
        via.ocupada = true;
        ultimaViaUsada = indiceVia;

        // 3. Activar objeto asociado en todos los clientes sincronizadamente
        if (via.objetoAsociado != null)
        {
            SetObjetoAsociadoClientRpc(indiceVia, true);
            via.objetoActivo = true;
        }

        Rigidbody rb = nuevoTren.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void ComprobarPuntoDesaparicion(Via via, int indiceVia)
    {
        if (via.trenActual == null || !via.objetoActivo || via.objetoAsociado == null || via.puntoDesaparicion == null)
            return;

        float distancia = Vector3.Distance(
            via.trenActual.transform.position,
            via.puntoDesaparicion.position
        );

        if (distancia <= 0.5f)
        {
            SetObjetoAsociadoClientRpc(indiceVia, false);
            via.objetoActivo = false;
        }
    }

    private void DetectarPuentes(GameObject tren)
    {
        if (tren == null) return;

        Collider colliderTren = tren.GetComponent<Collider>();
        if (colliderTren == null) return;

        Bounds boundsTren = colliderTren.bounds;
        Collider[] objetosDetectados = Physics.OverlapBox(
            boundsTren.center,
            boundsTren.extents,
            tren.transform.rotation
        );

        foreach (Collider objeto in objetosDetectados)
        {
            Transform actual = objeto.transform;

            while (actual != null)
            {
                if (actual.CompareTag("Bridge"))
                {
                    Vector3 posicionPuente = actual.position;

                    // Si el puente es un NetworkObject, lo despawnea limpiamente
                    NetworkObject puenteNetObj = actual.GetComponent<NetworkObject>();
                    if (puenteNetObj != null && puenteNetObj.IsSpawned)
                    {
                        puenteNetObj.Despawn(true);
                    }
                    else
                    {
                        // Si es un objeto estático de la escena, notifica a los clientes su destrucción
                        DestroyBridgeClientRpc(posicionPuente);
                        Destroy(actual.gameObject);
                    }

                    // Notifica a los clientes para reproducir los efectos visuales del puente destruido
                    SpawnBridgeFxClientRpc(posicionPuente);
                    break;
                }

                actual = actual.parent;
            }
        }
    }

    // --- RPCs para sincronizar estado visual y decorativo ---

    [ClientRpc]
    private void SetObjetoAsociadoClientRpc(int indiceVia, bool activo)
    {
        if (indiceVia >= 0 && indiceVia < vias.Count && vias[indiceVia].objetoAsociado != null)
        {
            vias[indiceVia].objetoAsociado.SetActive(activo);
            vias[indiceVia].objetoActivo = activo;
        }
    }

    [ClientRpc]
    private void DestroyBridgeClientRpc(Vector3 pos)
    {
        if (IsServer) return; // El servidor ya lo destruyó localmente

        Collider[] hits = Physics.OverlapSphere(pos, 1.5f);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Bridge"))
            {
                Destroy(hit.transform.root.gameObject);
                break;
            }
        }
    }

    [ClientRpc]
    private void SpawnBridgeFxClientRpc(Vector3 posicion)
    {
        if (fxPuenteDestruido == null) return;

        GameObject fx = Instantiate(fxPuenteDestruido, posicion, Quaternion.identity);
        Destroy(fx, ObtenerDuracionFX(fx));
    }

    private float ObtenerDuracionFX(GameObject fx)
    {
        ParticleSystem[] sistemas = fx.GetComponentsInChildren<ParticleSystem>();
        float duracionMaxima = 0f;

        foreach (ParticleSystem sistema in sistemas)
        {
            ParticleSystem.MainModule main = sistema.main;
            float duracion = main.loop ? 2f : main.duration;
            float duracionTotal = duracion + main.startLifetime.constantMax;

            if (duracionTotal > duracionMaxima)
            {
                duracionMaxima = duracionTotal;
            }
        }

        return duracionMaxima <= 0f ? 2f : duracionMaxima;
    }

    public void IniciarSpawner()
    {
        if (!IsServer) return;

        spawnerActivo = true;
        ultimaViaUsada = -1;
        temporizador = Random.Range(tiempoMinimo, tiempoMaximo);
    }

    public void DetenerSpawner()
    {
        if (!IsServer) return;
        spawnerActivo = false;
    }

    public void EliminarTodosLosTrenes()
    {
        if (!IsServer) return;

        for (int i = 0; i < vias.Count; i++)
        {
            Via via = vias[i];
            if (via.trenActual != null)
            {
                NetworkObject netObj = via.trenActual.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    Destroy(via.trenActual);
                }

                via.trenActual = null;
                via.ocupada = false;
            }

            if (via.objetoAsociado != null)
            {
                SetObjetoAsociadoClientRpc(i, false);
            }
            via.objetoActivo = false;
        }
    }

    private void OnDrawGizmos()
    {
        if (vias == null) return;

        foreach (Via via in vias)
        {
            if (via.puntoInicio == null || via.puntoFinal == null) continue;

            Gizmos.DrawLine(via.puntoInicio.position, via.puntoFinal.position);
            Gizmos.DrawWireSphere(via.puntoInicio.position, 0.5f);
            Gizmos.DrawWireSphere(via.puntoFinal.position, 0.5f);

            if (via.puntoDesaparicion != null)
            {
                Gizmos.DrawWireSphere(via.puntoDesaparicion.position, 0.5f);
            }
        }
    }
}