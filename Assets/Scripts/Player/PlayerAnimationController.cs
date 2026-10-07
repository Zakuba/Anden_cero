using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;

    private Vector3 ultimaPosicion;

    private void Start()
    {
        ultimaPosicion = transform.position;

        Debug.Log(
            "[ANIM] PlayerAnimationController iniciado en " +
            gameObject.name
        );
    }

    private void Update()
    {
        if (animator == null)
        {
            Debug.LogWarning("[ANIM] No hay Animator asignado.");
            return;
        }

        float distancia =
            Vector3.Distance(
                transform.position,
                ultimaPosicion
            );

        float velocidad =
            distancia / Mathf.Max(Time.deltaTime, 0.0001f);

        ultimaPosicion = transform.position;

        animator.SetFloat("Speed", velocidad);

        Debug.Log(
            "[ANIM] Posición: " +
            transform.position +
            " | Velocidad: " +
            velocidad
        );
    }

    public void SetAnimator(Animator nuevoAnimator)
    {
        animator = nuevoAnimator;

        Debug.Log(
            "[ANIM] Animator asignado: " +
            nuevoAnimator.gameObject.name
        );
    }
}