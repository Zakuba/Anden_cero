using System;
using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class TypewriterText : MonoBehaviour
{
    [Header("Texto")]
    [Tooltip("Si lo dejás vacío, usa el texto que ya tiene el componente")]
    [TextArea(2, 6)]
    [SerializeField] private string message;

    [Header("Velocidad")]
    [SerializeField] private float charsPerSecond = 25f;
    [Tooltip("Segundos de espera antes de empezar")]
    [SerializeField] private float startDelay = 0.3f;
    [Tooltip("Pausa extra después de . , ! ? : ; (0 = sin pausa)")]
    [SerializeField] private float punctuationPause = 0.15f;

    [Header("Final")]
    [Tooltip("Segundos que queda el texto completo antes de ocultarse. 0 = queda para siempre")]
    [SerializeField] private float hideAfter = 0f;

    public bool IsTyping { get; private set; }
    public event Action OnFinished;

    private TMP_Text tmp;
    private Coroutine routine;

    private void Awake()
    {
        tmp = GetComponent<TMP_Text>();
        if (string.IsNullOrEmpty(message)) message = tmp.text;

        // Arranca invisible para que no se vea el texto completo antes de empezar
        tmp.text = message;
        tmp.maxVisibleCharacters = 0;
    }

    /// Escribe el texto del Inspector.
    public void Play() => Play(message);

    /// Escribe un texto distinto.
    public void Play(string newMessage)
    {
        gameObject.SetActive(true);
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(TypeRoutine(newMessage));
    }

    /// Muestra todo el texto de golpe.
    public void Skip()
    {
        if (!IsTyping) return;
        if (routine != null) StopCoroutine(routine);
        Finish();
    }

    private IEnumerator TypeRoutine(string text)
    {
        IsTyping = true;
        tmp.text = text;
        tmp.maxVisibleCharacters = 0;
        tmp.ForceMeshUpdate(); // necesario para que textInfo tenga el conteo correcto

        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

        int total = tmp.textInfo.characterCount;
        float delay = 1f / Mathf.Max(0.01f, charsPerSecond);

        for (int i = 1; i <= total; i++)
        {
            tmp.maxVisibleCharacters = i;

            float wait = delay;
            char c = tmp.textInfo.characterInfo[i - 1].character;
            if (punctuationPause > 0f && (c == '.' || c == ',' || c == '!' || c == '?' || c == ':' || c == ';'))
                wait += punctuationPause;

            yield return new WaitForSecondsRealtime(wait);
        }

        Finish();

        if (hideAfter > 0f)
        {
            yield return new WaitForSecondsRealtime(hideAfter);
            tmp.maxVisibleCharacters = 0;
        }
    }

    private void Finish()
    {
        tmp.maxVisibleCharacters = int.MaxValue;
        IsTyping = false;
        OnFinished?.Invoke();
    }
}