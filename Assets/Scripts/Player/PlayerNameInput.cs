using TMPro;
using UnityEngine;

public class PlayerNameInput : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private int maxLength = 16;

    private void Start()
    {
        inputField.characterLimit = maxLength;

        // Muestra el nombre guardado la última vez
        PlayerData data = PersistenceManager.LoadData();
        if (!string.IsNullOrEmpty(data.nombreJugador))
            inputField.text = data.nombreJugador;

        inputField.onEndEdit.AddListener(SaveName);
    }

    private void OnDestroy()
    {
        // Por si el jugador aprieta "jugar" sin haber salido del campo
        if (inputField != null)
        {
            SaveName(inputField.text);
            inputField.onEndEdit.RemoveListener(SaveName);
        }
    }

    private void SaveName(string value)
    {
        PlayerData data = PersistenceManager.LoadData();
        data.nombreJugador = value.Trim();
        PersistenceManager.SaveData(data);
    }
}