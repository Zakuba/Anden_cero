using UnityEngine;

public class AudioControllerGame : MonoBehaviour
{   

    private bool wasMatchRunning = false;
    private bool playingResultado = false;
    private bool playingInGame = false;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PlaySalaDeEsperaMusic();
    }

    // Update is called once per frame
    void Awake()
    {
        PlaySalaDeEsperaMusic();
    }

    void Update(){
        
        // 1. Verificamos si la partida ya dio inicio (si el MatchManager existe y la partida corre)
        bool matchRunning = MatchManager.Instance == null || MatchManager.Instance.IsMatchRunning;

        // Detectar cuando la partida ACABA DE TERMINAR (estaba corriendo y ahora no)
        if (wasMatchRunning && !matchRunning && !playingResultado)
        {   
            PlaySalaDeResultadoMusic();
        }

        wasMatchRunning = matchRunning;

        if(wasMatchRunning && !playingInGame ){
            PlayInGameMusic();
        }
    }

    public void PlaySalaDeEsperaMusic(){
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySalaDeEsperaMusic();
    }

    public void PlayInGameMusic(){
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuMusic();
    }

    public void PlaySalaDeResultadoMusic(){
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySalaDeResultadoMusic();
    }
}
