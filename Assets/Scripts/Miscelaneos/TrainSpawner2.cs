using UnityEngine;
using System.Collections.Generic;

public class TrainSpawnerNivel2 : MonoBehaviour
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
    [SerializeField]
    private List<GameObject> trenPrefabs =
        new List<GameObject>();

    [Header("Vías")]
    [SerializeField]
    private List<Via> vias =
        new List<Via>();

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

    private void Start()
    {
        if (iniciarAutomaticamente)
        {
            IniciarSpawner();
        }
    }

    private void Update()
    {
        if (!spawnerActivo)
            return;

        MoverTrenes();

        temporizador -= Time.deltaTime;

        if (temporizador <= 0f)
        {
            IntentarCrearTren();

            temporizador = Random.Range(
                tiempoMinimo,
                tiempoMaximo
            );
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

            MoverTren(via);

            if (via.trenActual != null)
            {
                DetectarPuentes(via.trenActual);
                ComprobarPuntoDesaparicion(via);
            }
        }
    }

    private void MoverTren(Via via)
    {
        GameObject tren = via.trenActual;

        Vector3 posicionActual = tren.transform.position;

        Vector3 nuevaPosicion = Vector3.MoveTowards(
            posicionActual,
            via.puntoFinal.position,
            velocidad * Time.deltaTime
        );

        tren.transform.position = nuevaPosicion;

        Vector3 direccion =
            via.puntoFinal.position - posicionActual;

        if (direccion != Vector3.zero)
        {
            Quaternion rotacionObjetivo =
                Quaternion.LookRotation(
                    direccion.normalized
                );

            tren.transform.rotation =
                Quaternion.Slerp(
                    tren.transform.rotation,
                    rotacionObjetivo,
                    10f * Time.deltaTime
                );
        }

        if (Vector3.Distance(
            tren.transform.position,
            via.puntoFinal.position
        ) < 0.05f)
        {
            Destroy(tren);

            via.trenActual = null;
            via.ocupada = false;

            // Seguridad: si el objeto seguía activo,
            // lo apagamos al terminar el recorrido.
            if (via.objetoAsociado != null &&
                via.objetoActivo)
            {
                via.objetoAsociado.SetActive(false);
                via.objetoActivo = false;
            }
        }
    }

    private void IntentarCrearTren()
    {
        if (trenPrefabs.Count == 0)
        {
            Debug.LogWarning(
                "TrainSpawnerNivel2: No hay prefabs de tren asignados."
            );
            return;
        }

        if (vias.Count == 0)
        {
            Debug.LogWarning(
                "TrainSpawnerNivel2: No hay vías configuradas."
            );
            return;
        }

        List<int> viasDisponibles =
            new List<int>();

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

        if (viasDisponibles.Count == 0)
        {
            return;
        }

        int indiceAleatorio =
            Random.Range(
                0,
                viasDisponibles.Count
            );

        int indiceViaElegida =
            viasDisponibles[indiceAleatorio];

        Via viaElegida =
            vias[indiceViaElegida];

        CrearTrenEnVia(
            viaElegida,
            indiceViaElegida
        );
    }

    private void CrearTrenEnVia(
        Via via,
        int indiceVia
    )
    {
        int indiceTren =
            Random.Range(
                0,
                trenPrefabs.Count
            );

        GameObject trenPrefabElegido =
            trenPrefabs[indiceTren];

        if (trenPrefabElegido == null)
        {
            Debug.LogWarning(
                "TrainSpawnerNivel2: Uno de los prefabs de tren es NULL."
            );
            return;
        }

        GameObject nuevoTren =
            Instantiate(
                trenPrefabElegido,
                via.puntoInicio.position,
                via.puntoInicio.rotation
            );

        via.trenActual =
            nuevoTren;

        via.ocupada =
            true;

        ultimaViaUsada =
            indiceVia;

        // Activar el objeto asociado
        if (via.objetoAsociado != null)
        {
            via.objetoAsociado.SetActive(true);
            via.objetoActivo = true;
        }

        Rigidbody rb =
            nuevoTren.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void ComprobarPuntoDesaparicion(Via via)
    {
        if (via.trenActual == null)
            return;

        if (!via.objetoActivo)
            return;

        if (via.objetoAsociado == null)
            return;

        if (via.puntoDesaparicion == null)
            return;

        float distancia =
            Vector3.Distance(
                via.trenActual.transform.position,
                via.puntoDesaparicion.position
            );

        if (distancia <= 0.5f)
        {
            via.objetoAsociado.SetActive(false);
            via.objetoActivo = false;
        }
    }

    private void DetectarPuentes(GameObject tren)
    {
        if (tren == null)
            return;

        Collider colliderTren =
            tren.GetComponent<Collider>();

        if (colliderTren == null)
            return;

        Bounds boundsTren =
            colliderTren.bounds;

        Collider[] objetosDetectados =
            Physics.OverlapBox(
                boundsTren.center,
                boundsTren.extents,
                tren.transform.rotation
            );

        foreach (Collider objeto in objetosDetectados)
        {
            Transform actual =
                objeto.transform;

            while (actual != null)
            {
                if (actual.CompareTag("Bridge"))
                {
                    Debug.Log(
                        "💥 Tren destruyó puente: " +
                        actual.name
                    );

                    Vector3 posicionFX =
                        actual.position;

                    if (fxPuenteDestruido != null)
                    {
                        GameObject fx =
                            Instantiate(
                                fxPuenteDestruido,
                                posicionFX,
                                Quaternion.identity
                            );

                        Destroy(
                            fx,
                            ObtenerDuracionFX(fx)
                        );
                    }

                    Destroy(
                        actual.gameObject
                    );

                    break;
                }

                actual =
                    actual.parent;
            }
        }
    }

    private float ObtenerDuracionFX(GameObject fx)
    {
        ParticleSystem[] sistemas =
            fx.GetComponentsInChildren<ParticleSystem>();

        float duracionMaxima = 0f;

        foreach (ParticleSystem sistema in sistemas)
        {
            ParticleSystem.MainModule main =
                sistema.main;

            float duracion =
                main.duration;

            if (main.loop)
            {
                duracion = 2f;
            }

            float duracionTotal =
                duracion +
                main.startLifetime.constantMax;

            if (duracionTotal >
                duracionMaxima)
            {
                duracionMaxima =
                    duracionTotal;
            }
        }

        if (duracionMaxima <= 0f)
        {
            duracionMaxima = 2f;
        }

        return duracionMaxima;
    }

    public void IniciarSpawner()
    {
        spawnerActivo =
            true;

        ultimaViaUsada =
            -1;

        temporizador =
            Random.Range(
                tiempoMinimo,
                tiempoMaximo
            );
    }

    public void DetenerSpawner()
    {
        spawnerActivo =
            false;
    }

    public void EliminarTodosLosTrenes()
    {
        foreach (Via via in vias)
        {
            if (via.trenActual != null)
            {
                Destroy(
                    via.trenActual
                );

                via.trenActual = null;
                via.ocupada = false;
            }

            if (via.objetoAsociado != null)
            {
                via.objetoAsociado.SetActive(false);
            }

            via.objetoActivo = false;
        }
    }

    private void OnDrawGizmos()
    {
        if (vias == null)
            return;

        foreach (Via via in vias)
        {
            if (via.puntoInicio == null ||
                via.puntoFinal == null)
                continue;

            Gizmos.DrawLine(
                via.puntoInicio.position,
                via.puntoFinal.position
            );

            Gizmos.DrawWireSphere(
                via.puntoInicio.position,
                0.5f
            );

            Gizmos.DrawWireSphere(
                via.puntoFinal.position,
                0.5f
            );

            if (via.puntoDesaparicion != null)
            {
                Gizmos.DrawWireSphere(
                    via.puntoDesaparicion.position,
                    0.5f
                );
            }
        }
    }
}