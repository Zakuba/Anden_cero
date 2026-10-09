using UnityEngine;
using System.Collections;

public class animacionCamLobby : MonoBehaviour
{
    [Header("Luz roja de la cámara")]
    public GameObject luzRoja;
    public float intervaloTitileo = 0.5f;

    [Header("Animación de los tres objetos")]
    public GameObject objeto1;
    public GameObject objeto2;
    public GameObject objeto3;
    public float intervaloAnimacion = 0.4f;

    private void Start()
    {
        if (luzRoja != null)
            StartCoroutine(TitilarLuz());

        if (objeto1 != null && objeto2 != null && objeto3 != null)
            StartCoroutine(AnimarObjetos());
    }

    private IEnumerator TitilarLuz()
    {
        while (true)
        {
            luzRoja.SetActive(true);
            yield return new WaitForSeconds(intervaloTitileo);

            luzRoja.SetActive(false);
            yield return new WaitForSeconds(intervaloTitileo);
        }
    }

    private IEnumerator AnimarObjetos()
    {
        while (true)
        {
            objeto1.SetActive(true);
            objeto2.SetActive(false);
            objeto3.SetActive(false);
            yield return new WaitForSeconds(intervaloAnimacion);

            objeto1.SetActive(true);
            objeto2.SetActive(true);
            objeto3.SetActive(false);
            yield return new WaitForSeconds(intervaloAnimacion);

            objeto1.SetActive(true);
            objeto2.SetActive(true);
            objeto3.SetActive(true);
            yield return new WaitForSeconds(intervaloAnimacion);
        }
    }
}