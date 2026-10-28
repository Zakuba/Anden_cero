using System;
using System.Collections;
using UnityEngine;

public class MapCameraMover : MonoBehaviour
{
    // Un objeto que se mueve desde donde está hasta copiar el world transform de otro
    [Serializable]
    public class MoveItem
    {
        [Tooltip("El objeto que se mueve (por ejemplo, la puerta cerrada)")]
        public Transform objectToMove;
        [Tooltip("El objeto invisible cuyo world transform se copia (por ejemplo, la puerta abierta)")]
        public Transform target;
    }

    // Se dispara cuando la cámara llega a un waypoint
    [Serializable]
    public class WaypointEvent
    {
        public string name = "Evento";
        [Tooltip("Número del waypoint, empezando en 1 (el cuarto de la lista = 4)")]
        public int waypointNumber = 4;
        [Tooltip("Segundos que tardan los objetos en llegar a su destino")]
        public float moveDuration = 1.5f;
        public MoveItem[] items;
    }

    public static MapCameraMover Instance { get; private set; }

    [Header("Recorrido")]
    [Tooltip("Objetos vacíos de la escena, en orden. Su posición y rotación definen por dónde pasa la cámara. Vacío = viaje directo")]
    [SerializeField] private Transform[] waypoints;
    [Tooltip("Segundos que dura todo el recorrido. 0 = corte instantáneo")]
    [SerializeField] private float travelDuration = 4f;

    [Header("Eventos por waypoint")]
    [SerializeField] private WaypointEvent[] events;

    [Header("Debug")]
    [SerializeField] private bool drawPathGizmo = true;

    [Header("Objeto a activar")]
    [SerializeField] private GameObject laboratorio;

    [Header("Objeto a desactivar")]
    [SerializeField] private GameObject Collapser;

    private Camera cam;
    private AudioListener listener;
    private Behaviour brain; // CinemachineBrain

    private Camera targetCam;
    private Action onArrived;
    private Vector3[] pathPos;
    private Quaternion[] pathRot;
    private bool[] eventFired;
    private float startSize, startFov;
    private float t;
    private bool active;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        listener = GetComponent<AudioListener>();

        foreach (Behaviour b in GetComponents<Behaviour>())
        {
            if (b.GetType().Name == "CinemachineBrain")
            {
                brain = b;
                break;
            }
        }
    }

    public void SwitchTo(Camera playerCamera, Action arrivedCallback)
    {
        if (brain != null) brain.enabled = false; // Cinemachine deja de pisar el transform

        targetCam = playerCamera;
        onArrived = arrivedCallback;

        // Puntos del recorrido: [inicio, waypoints..., destino]
        int wpCount = waypoints != null ? waypoints.Length : 0;
        pathPos = new Vector3[wpCount + 2];
        pathRot = new Quaternion[wpCount + 2];

        pathPos[0] = transform.position;
        pathRot[0] = transform.rotation;

        for (int i = 0; i < wpCount; i++)
        {
            Transform wp = waypoints[i];
            pathPos[i + 1] = wp != null ? wp.position : pathPos[i];
            pathRot[i + 1] = wp != null ? wp.rotation : pathRot[i];
        }

        eventFired = new bool[events != null ? events.Length : 0];

        startSize = cam.orthographicSize;
        startFov = cam.fieldOfView;
        t = 0f;
        active = true;
    }

    private void LateUpdate()
    {
        if (!active) return;

        if (targetCam == null)
        {
            active = false;
            return;
        }

        t = travelDuration <= 0f ? 1f : Mathf.Min(1f, t + Time.deltaTime / travelDuration);
        float e = Mathf.SmoothStep(0f, 1f, t);

        // El último punto es la cámara del player y se actualiza cada frame
        int last = pathPos.Length - 1;
        pathPos[last] = targetCam.transform.position;
        pathRot[last] = targetCam.transform.rotation;

        // Posición sobre la curva
        float u = e * last;
        int seg = Mathf.Min((int)u, last - 1);
        float f = u - seg;

        Vector3 p0 = pathPos[Mathf.Max(seg - 1, 0)];
        Vector3 p1 = pathPos[seg];
        Vector3 p2 = pathPos[seg + 1];
        Vector3 p3 = pathPos[Mathf.Min(seg + 2, last)];

        transform.position = CatmullRom(p0, p1, p2, p3, f);
        transform.rotation = Quaternion.Slerp(pathRot[seg], pathRot[seg + 1], Mathf.SmoothStep(0f, 1f, f));

        // Zoom / FOV: del valor inicial al de la cámara del player
        if (cam.orthographic)
            cam.orthographicSize = Mathf.Lerp(startSize, targetCam.orthographicSize, e);
        else
            cam.fieldOfView = Mathf.Lerp(startFov, targetCam.fieldOfView, e);

        CheckEvents(u);

        if (t >= 1f)
        {
            active = false;
            transform.SetPositionAndRotation(targetCam.transform.position, targetCam.transform.rotation);

            // Primero se enciende la del player y después se apaga esta
            onArrived?.Invoke();
            cam.enabled = false;
            if (listener != null) listener.enabled = false;
        }
    }

    // ---------- Eventos ----------

    // u = posición sobre el recorrido. El waypoint número n está en u = n (u = 0 es el inicio)
    private void CheckEvents(float u)
    {
        if (events == null) return;

        int wpCount = waypoints != null ? waypoints.Length : 0;

        for (int i = 0; i < events.Length; i++)
        {
            if (eventFired[i]) continue;

            int n = Mathf.Clamp(events[i].waypointNumber, 1, Mathf.Max(1, wpCount));
            if (u >= n)
            {
                eventFired[i] = true;
                StartCoroutine(MoveItemsRoutine(events[i]));
            }
        }
    }

    private IEnumerator MoveItemsRoutine(WaypointEvent ev)
    {
        if (ev.items == null || ev.items.Length == 0) yield break;

        Collapser.SetActive(false);
        laboratorio.SetActive(true);

        // Poses de partida
        Vector3[] startPos = new Vector3[ev.items.Length];
        Quaternion[] startRot = new Quaternion[ev.items.Length];
        for (int i = 0; i < ev.items.Length; i++)
        {
            Transform o = ev.items[i].objectToMove;
            if (o == null) continue;
            startPos[i] = o.position;
            startRot[i] = o.rotation;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.0001f, ev.moveDuration);

        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime / duration;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed));

            for (int i = 0; i < ev.items.Length; i++)
            {
                Transform o = ev.items[i].objectToMove;
                Transform tg = ev.items[i].target;
                if (o == null || tg == null) continue;

                o.SetPositionAndRotation(
                    Vector3.Lerp(startPos[i], tg.position, e),
                    Quaternion.Slerp(startRot[i], tg.rotation, e));
            }

            yield return null;
        }

        // Asegura la pose final exacta
        foreach (MoveItem item in ev.items)
        {
            if (item.objectToMove != null && item.target != null)
                item.objectToMove.SetPositionAndRotation(item.target.position, item.target.rotation);
        }
    }

    // ---------- Curva y debug ----------

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // Dibuja el recorrido y los movimientos de los eventos en la Scene view
    private void OnDrawGizmos()
    {
        if (!drawPathGizmo) return;

        if (waypoints != null && waypoints.Length > 0)
        {
            Gizmos.color = Color.cyan;
            Vector3 prev = transform.position;

            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawLine(prev, waypoints[i].position);
                Gizmos.DrawWireSphere(waypoints[i].position, 0.4f);
                Gizmos.DrawRay(waypoints[i].position, waypoints[i].forward * 1.5f); // hacia dónde mira
                prev = waypoints[i].position;
            }
        }

        if (events != null)
        {
            Gizmos.color = Color.green;
            foreach (WaypointEvent ev in events)
            {
                if (ev == null || ev.items == null) continue;
                foreach (MoveItem item in ev.items)
                {
                    if (item.objectToMove == null || item.target == null) continue;
                    Gizmos.DrawLine(item.objectToMove.position, item.target.position);
                    Gizmos.DrawWireSphere(item.target.position, 0.25f);
                }
            }
        }
    }
}