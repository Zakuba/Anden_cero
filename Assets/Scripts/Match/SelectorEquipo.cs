using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SelectorEquipo : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button botonAlfa;
    [SerializeField] private Button botonBeta;
    [SerializeField] private TMP_Text textoAlfa;
    [SerializeField] private TMP_Text textoBeta;

    private MatchManager mm;

    private void Start()
    {
        if (panel != null) panel.SetActive(false);
        StartCoroutine(Conectar());
    }

    private IEnumerator Conectar()
    {
        // Espera a que el MatchManager exista y esté sincronizado en red.
        while (MatchManager.Instance == null || !MatchManager.Instance.IsSpawned)
            yield return null;

        mm = MatchManager.Instance;

        // Solo se usa en el mapa 2 vs 2.
        if (!mm.ModoEquipos) yield break;

        botonAlfa.onClick.AddListener(() => mm.CambiarEquipoRpc(0));
        botonBeta.onClick.AddListener(() => mm.CambiarEquipoRpc(1));

        mm.EquiposChanged += Refrescar;
        mm.MatchStartedChanged += OnMatchStartedChanged;

        panel.SetActive(true);
        Refrescar();
    }

    private void OnMatchStartedChanged(bool anterior, bool nuevo)
    {
        if (nuevo && panel != null) panel.SetActive(false);
    }

    private void Refrescar()
    {
        if (mm == null) return;

        ulong yo = NetworkManager.Singleton.LocalClientId;
        int miEquipo = mm.GetEquipo(yo);

        textoAlfa.text = ArmarTexto("EQUIPO ALFA", 0, yo);
        textoBeta.text = ArmarTexto("EQUIPO BETA", 1, yo);

        // Se puede cambiar solo a un equipo donde no estés y que no esté lleno.
        botonAlfa.interactable = miEquipo != 0 && mm.ContarEquipo(0) < MatchManager.MaxPorEquipo;
        botonBeta.interactable = miEquipo != 1 && mm.ContarEquipo(1) < MatchManager.MaxPorEquipo;
    }

    private string ArmarTexto(string titulo, int equipo, ulong yo)
    {
        var clientes = mm.ClientesDeEquipo(equipo);
        string t = $"{titulo} ({clientes.Count}/{MatchManager.MaxPorEquipo})";
        foreach (ulong id in clientes)
            t += $"\nJugador {id + 1}" + (id == yo ? " (vos)" : "");
        return t;
    }

    private void OnDestroy()
    {
        if (mm == null) return;
        mm.EquiposChanged -= Refrescar;
        mm.MatchStartedChanged -= OnMatchStartedChanged;
    }
}