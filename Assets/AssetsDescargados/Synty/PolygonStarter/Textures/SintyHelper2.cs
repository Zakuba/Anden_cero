
using System.Collections;
using UnityEngine;

public class SintyHelper2 : MonoBehaviour
{
    [SerializeField] private float tiempoEspera = 2f;

    private bool activado = false;

    public void Cerrar()
    {
        if (activado)
            return;

        activado = true;
        StartCoroutine(CerrarDespuesDeEsperar());
    }

    private IEnumerator CerrarDespuesDeEsperar()
    {
        yield return new WaitForSecondsRealtime(tiempoEspera);

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
