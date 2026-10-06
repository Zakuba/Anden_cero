using UnityEngine;
using TMPro;
using System.Collections;

[System.Serializable]
public class CharacterData
{
    public GameObject characterPrefab;
    public TMP_Text characterText;

    [Header("Objeto asociado")]
    public GameObject objetoAsociado;

    [Header("Posición")]
    public Vector3 offsetSpawn = Vector3.zero;
}

public class CharacterSelection : MonoBehaviour
{
    [Header("Personajes disponibles")]
    [SerializeField] private CharacterData[] characters;

    [Header("Punto de Spawn")]
    [SerializeField] private Transform previewPoint;

    [Header("Puerta de transición")]
    [SerializeField] private Transform puertaReal;
    [SerializeField] private Transform puertaFinal;

    [Header("Animación de transición")]
    [SerializeField] private float duracionCierre = 0.25f;
    [SerializeField] private float tiempoCerrada = 0.05f;
    [SerializeField] private float duracionApertura = 0.25f;

    private int currentIndex = 0;

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

        if (previewPoint == null)
        {
            Debug.LogError("No se asignó el Preview Point.");
            return;
        }

        posicionInicialPuerta = puertaReal.position;
        rotacionInicialPuerta = puertaReal.rotation;

        DesactivarTodosLosPersonajes();
        OcultarTodosLosTextos();
        DesactivarTodosLosObjetos();

        ShowCharacter();
    }

    private void DesactivarTodosLosPersonajes()
    {
        foreach (CharacterData character in characters)
        {
            if (character.characterPrefab != null)
            {
                character.characterPrefab.SetActive(false);
            }
        }
    }

    private void DesactivarTodosLosObjetos()
    {
        foreach (CharacterData character in characters)
        {
            if (character.objetoAsociado != null)
            {
                character.objetoAsociado.SetActive(false);
            }
        }
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

        yield return StartCoroutine(
            MoverPuerta(
                puertaReal.position,
                puertaReal.rotation,
                puertaFinal.position,
                puertaFinal.rotation,
                duracionCierre
            )
        );

        ShowCharacter();

        yield return new WaitForSeconds(tiempoCerrada);

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
        foreach (CharacterData character in characters)
        {
            if (character.characterPrefab != null)
            {
                character.characterPrefab.SetActive(false);
            }
        }

        OcultarTodosLosTextos();
        DesactivarTodosLosObjetos();

        CharacterData characterSeleccionado =
            characters[currentIndex];

        // GUARDAR SELECCIÓN
        PlayerSelectionData.selectedCharacterIndex = currentIndex;

        if (characterSeleccionado.characterPrefab != null)
        {
            GameObject personaje =
                characterSeleccionado.characterPrefab;

            personaje.transform.SetPositionAndRotation(
                previewPoint.position +
                characterSeleccionado.offsetSpawn,
                previewPoint.rotation
            );

            personaje.SetActive(true);
        }

        if (characterSeleccionado.characterText != null)
        {
            characterSeleccionado.characterText.gameObject.SetActive(true);
        }

        if (characterSeleccionado.objetoAsociado != null)
        {
            characterSeleccionado.objetoAsociado.SetActive(true);
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

            float t =
                Mathf.Clamp01(tiempo / duracion);

            t =
                Mathf.SmoothStep(0f, 1f, t);

            puertaReal.position =
                Vector3.Lerp(
                    posicionComienzo,
                    posicionDestino,
                    t
                );

            puertaReal.rotation =
                Quaternion.Slerp(
                    rotacionComienzo,
                    rotacionDestino,
                    t
                );

            yield return null;
        }

        puertaReal.position =
            posicionDestino;

        puertaReal.rotation =
            rotacionDestino;
    }

    public GameObject GetSelectedCharacter()
    {
        return characters[currentIndex].characterPrefab;
    }

    public int GetSelectedCharacterIndex()
    {
        return currentIndex;
    }
}