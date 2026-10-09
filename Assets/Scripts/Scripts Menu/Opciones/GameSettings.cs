
[System.Serializable]
public class GameSettings
{
    // --- Vídeo y Gráficos ---
    public int resolutionIndex = -1;
     public bool fullScreen = true;
    public int qualityLevel = 2; // Ajustar según tus niveles (ej. 2 = Alto)

    // --- Atmósfera ---
    public float brightness = 1.0f;
    public float contrast = 1.0f;

    // --- Red ---
    public bool showPing = false;
    public bool showFPS = false;
    // --- Controles ---
    public string inputRebinds = ""; // Aquí se guardarán los overrides del Input System en JSON

}
