using UnityEngine;
using System.Collections;

public class ExplosionBomba : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float duracion = 3f;

    [Header("Objeto que titila")]
    [SerializeField] private GameObject objetoTitileo;

    [Header("Velocidad del titileo")]
    [SerializeField] private float intervaloInicial = 0.5f;
    [SerializeField] private float intervaloFinal = 0.05f;

    [Header("Colores (Alfa y modos sin equipos)")]
    [SerializeField] private Color colorNormal = Color.white;
    [SerializeField] private Color colorRojo = Color.red;

    [Header("Equipo Beta (solo 2 vs 2)")]
    [SerializeField] private Material materialBeta;
    [SerializeField] private Material materialBetaParpadeo;

    private Renderer objetoRenderer;
    private BombInteractable bombLogic;
    private Material materialOriginal;
    private bool materialEsBeta;

    private void Awake()
    {
        bombLogic = GetComponent<BombInteractable>();
    }

    private void Start()
    {
        if (objetoTitileo != null)
        {
            objetoRenderer = objetoTitileo.GetComponent<Renderer>();

            if (objetoRenderer != null)
            {
                materialOriginal = objetoRenderer.sharedMaterial;
                MostrarNormal();
            }
        }

        StartCoroutine(Titileo());
    }

    // Beta solo existe en el modo equipos, y solo si están asignados los dos materiales.
    private bool EsEquipoBeta()
    {
        if (materialBeta == null || materialBetaParpadeo == null) return false;
        if (bombLogic == null || MatchManager.Instance == null) return false;
        if (!MatchManager.Instance.ModoEquipos) return false;

        return MatchManager.Instance.GetEquipo(
            bombLogic.currentOwnerClientId.Value) == 1;
    }

    // Vuelve al material original solo cuando hace falta (evita crear copias de más).
    private void UsarMaterialAlfa()
    {
        if (!materialEsBeta) return;
        materialEsBeta = false;
        objetoRenderer.sharedMaterial = materialOriginal;
    }

    private void MostrarNormal()
    {
        if (objetoRenderer == null) return;

        if (EsEquipoBeta())
        {
            materialEsBeta = true;
            objetoRenderer.sharedMaterial = materialBeta;
        }
        else
        {
            UsarMaterialAlfa();
            objetoRenderer.material.color = colorNormal;
        }
    }

    private void MostrarParpadeo()
    {
        if (objetoRenderer == null) return;

        if (EsEquipoBeta())
        {
            materialEsBeta = true;
            objetoRenderer.sharedMaterial = materialBetaParpadeo;
        }
        else
        {
            UsarMaterialAlfa();
            objetoRenderer.material.color = colorRojo;
        }
    }

    private IEnumerator Titileo()
    {
        float tiempoTranscurrido = 0f;

        while (tiempoTranscurrido < duracion)
        {
            // Pausa el parpadeo si la bomba está siendo bateada
            if (bombLogic != null && bombLogic.isMoving.Value)
            {
                yield return null;
                continue;
            }

            float progreso = tiempoTranscurrido / duracion;

            float intervalo = Mathf.Lerp(
                intervaloInicial,
                intervaloFinal,
                progreso
            );

            MostrarParpadeo();
            yield return new WaitForSeconds(intervalo);

            MostrarNormal();
            yield return new WaitForSeconds(intervalo);

            tiempoTranscurrido += intervalo * 2f;
        }

        // Simular la desaparición de la bomba
        if (objetoTitileo != null)
        {
            objetoTitileo.SetActive(false);
        }
    }
}