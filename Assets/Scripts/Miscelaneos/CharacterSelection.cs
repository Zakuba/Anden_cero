using UnityEngine;
using TMPro;

[System.Serializable]
public class CharacterData
{
    public string characterName;
    public GameObject characterPrefab;
}

public class CharacterSelection : MonoBehaviour
{
    [Header("Personajes disponibles")]
    [SerializeField] private CharacterData[] characters;

    [Header("Preview")]
    [SerializeField] private Transform previewPoint;

    [Header("UI")]
    [SerializeField] private TMP_Text characterNameText;

    private int currentIndex = 0;
    private GameObject currentCharacter;

    private void Start()
    {
        if (characters == null || characters.Length == 0)
        {
            Debug.LogError("No hay personajes configurados.");
            return;
        }

        ShowCharacter();
    }

    public void NextCharacter()
    {
        currentIndex++;

        if (currentIndex >= characters.Length)
            currentIndex = 0;

        ShowCharacter();
    }

    public void PreviousCharacter()
    {
        currentIndex--;

        if (currentIndex < 0)
            currentIndex = characters.Length - 1;

        ShowCharacter();
    }

    private void ShowCharacter()
    {
        // Elimina el personaje que estaba mostrando
        if (currentCharacter != null)
        {
            Destroy(currentCharacter);
        }

        // Crea el nuevo personaje
        currentCharacter = Instantiate(
            characters[currentIndex].characterPrefab,
            previewPoint.position,
            previewPoint.rotation,
            previewPoint
        );

        // Actualiza el nombre
        if (characterNameText != null)
        {
            characterNameText.text = characters[currentIndex].characterName;
        }
    }

    public GameObject GetSelectedCharacter()
    {
        return characters[currentIndex].characterPrefab;
    }

    public string GetSelectedCharacterName()
    {
        return characters[currentIndex].characterName;
    }
}