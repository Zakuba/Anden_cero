using UnityEngine;

public class AudioControllerLobby : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuMusic();
    }

    // Update is called once per frame
    void Awake()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuMusic();
    }
}
