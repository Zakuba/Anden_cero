
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class ControladorVideo : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Botones")]
    [SerializeField] private Button botonPlayConEspera;
    [SerializeField] private Button botonPlayAlFinalizar;
    [SerializeField] private Button botonStop;

    [Header("Configuracion")]
    [SerializeField] private float tiempoEspera = 2f;

    private Coroutine reproduccionPendiente;
    private bool videoFinalizado;

    private void Awake()
    {
        videoPlayer.playOnAwake = false;
        videoPlayer.Stop();

        botonPlayConEspera.onClick.AddListener(PlayConEspera);
        botonPlayAlFinalizar.onClick.AddListener(PlayAlFinalizar);
        botonStop.onClick.AddListener(StopVideo);

        videoPlayer.loopPointReached += AlFinalizarVideo;

        // El segundo boton solo se habilita al finalizar.
        botonPlayAlFinalizar.interactable = false;
    }

    private void PlayConEspera()
    {
        CancelarEspera();

        videoFinalizado = false;
        botonPlayAlFinalizar.interactable = false;

        videoPlayer.Stop();
        reproduccionPendiente = StartCoroutine(
            ReproducirDespuesDeEspera()
        );
    }

    private IEnumerator ReproducirDespuesDeEspera()
    {
        yield return new WaitForSeconds(tiempoEspera);

        videoPlayer.time = 0;
        videoPlayer.Play();

        reproduccionPendiente = null;
    }

    private void PlayAlFinalizar()
    {
        if (!videoFinalizado)
            return;

        videoFinalizado = false;
        botonPlayAlFinalizar.interactable = false;

        videoPlayer.Stop();
        videoPlayer.time = 0;
        videoPlayer.Play();
    }

    private void StopVideo()
    {
        CancelarEspera();

        videoFinalizado = false;
        botonPlayAlFinalizar.interactable = false;

        videoPlayer.Stop();
        videoPlayer.time = 0;
        videoPlayer.frame = 0;
        videoPlayer.Play();
        videoPlayer.Pause();
    }


    private void AlFinalizarVideo(VideoPlayer vp)
    {
        videoFinalizado = true;
        botonPlayAlFinalizar.interactable = true;
    }

    private void CancelarEspera()
    {
        if (reproduccionPendiente != null)
        {
            StopCoroutine(reproduccionPendiente);
            reproduccionPendiente = null;
        }
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= AlFinalizarVideo;

        if (botonPlayConEspera != null)
            botonPlayConEspera.onClick.RemoveListener(PlayConEspera);

        if (botonPlayAlFinalizar != null)
            botonPlayAlFinalizar.onClick.RemoveListener(PlayAlFinalizar);

        if (botonStop != null)
            botonStop.onClick.RemoveListener(StopVideo);
    }
}
