using UnityEngine;
using TMPro;
using System.Collections;

[System.Serializable]
public class CharacterData
{
    public GameObject characterPrefab;
    public TMP_Text characterText;
}

public class CharacterSelection : MonoBehaviour
{
    [Header("Personajes disponibles")]
    [SerializeField] private CharacterData[] characters;

    [Header("Preview")]
    [SerializeField] private Transform previewPoint;

    [Header("Puerta de transición")]
    [SerializeField] private Transform puertaReal;
    [SerializeField] private Transform puertaFinal;

    [Header("Animación de transición")]
    [SerializeField] private float duracionCierre = 0.25f;
    [SerializeField] private float tiempoCerrada = 0.05f;
    [SerializeField] private float duracionApertura = 0.25f;

    private int currentIndex = 0;
    private GameObject currentCharacter;

    private Vector3 posicionInicialPuerta;
    private Quaternion rotacionInicialPuerta;

    private bool transicionando = false;

    private void Start()
    {
        if (characters == null || characters.Length == 0)
        {
            Debug.LogError("No hay personajes configurados.");
            return;
        }

        // Guardamos la posición inicial de la puerta
        posicionInicialPuerta = puertaReal.position;
        rotacionInicialPuerta = puertaReal.rotation;

        // Ocultamos todos los textos
        OcultarTodosLosTextos();

        // Mostramos el primer personaje
        ShowCharacter();
    }

    public void NextCharacter()
    {
        if (transicionando)
            return;

        currentIndex++;

        if (currentIndex >= characters.Length)
            currentIndex = 0;

        StartCoroutine(TransicionPersonaje());
    }

    public void PreviousCharacter()
    {
        if (transicionando)
            return;

        currentIndex--;

        if (currentIndex < 0)
            currentIndex = characters.Length - 1;

        StartCoroutine(TransicionPersonaje());
    }

    private IEnumerator TransicionPersonaje()
    {
        transicionando = true;

        // =========================
        // CERRAR PUERTA
        // =========================

        yield return StartCoroutine(
            MoverPuerta(
                puertaReal.position,
                puertaReal.rotation,
                puertaFinal.position,
                puertaFinal.rotation,
                duracionCierre
            )
        );

        // =========================
        // CAMBIAR PERSONAJE
        // =========================

        ShowCharacter();

        // Pequeña pausa con la puerta cerrada
        yield return new WaitForSeconds(tiempoCerrada);

        // =========================
        // ABRIR PUERTA
        // =========================

        yield return StartCoroutine(
            MoverPuerta(
                puertaReal.position,
                puertaReal.rotation,
                posicionInicialPuerta,
                rotacionInicialPuerta,
                duracionApertura
            )
        );

        transicionando = false;
    }

    private void ShowCharacter()
    {
        // Elimina el personaje anterior
        if (currentCharacter != null)
        {
            Destroy(currentCharacter);
        }

        // Oculta todos los textos
        OcultarTodosLosTextos();

        // Crea el nuevo personaje
        currentCharacter = Instantiate(
            characters[currentIndex].characterPrefab,
            previewPoint.position,
            previewPoint.rotation,
            previewPoint
        );

        // Muestra solamente el texto del personaje seleccionado
        if (characters[currentIndex].characterText != null)
        {
            characters[currentIndex].characterText.gameObject.SetActive(true);
        }
    }

    private void OcultarTodosLosTextos()
    {
        foreach (CharacterData character in characters)
        {
            if (character.characterText != null)
            {
                character.characterText.gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator MoverPuerta(
        Vector3 posicionComienzo,
        Quaternion rotacionComienzo,
        Vector3 posicionDestino,
        Quaternion rotacionDestino,
        float duracion
    )
    {
        float tiempo = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;

            float t = Mathf.Clamp01(tiempo / duracion);

            // Movimiento suave
            t = Mathf.SmoothStep(0f, 1f, t);

            puertaReal.position = Vector3.Lerp(
                posicionComienzo,
                posicionDestino,
                t
            );

            puertaReal.rotation = Quaternion.Slerp(
                rotacionComienzo,
                rotacionDestino,
                t
            );

            yield return null;
        }

        // Aseguramos que termine exactamente en el destino
        puertaReal.position = posicionDestino;
        puertaReal.rotation = rotacionDestino;
    }

    public GameObject GetSelectedCharacter()
    {
        return characters[currentIndex].characterPrefab;
    }
}