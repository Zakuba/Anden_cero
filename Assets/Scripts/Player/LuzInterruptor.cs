using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LuzInterruptor : MonoBehaviour
{
    public Light luz;
    public bool empiezaEncendida = false;

    void Start()
    {
        if (luz == null) luz = GetComponent<Light>();
        if (luz != null) luz.enabled = empiezaEncendida;
    }

    void Update()
    {
        if (luz == null) return;

#if ENABLE_INPUT_SYSTEM
        bool presionada = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
        bool presionada = Input.GetKeyDown(KeyCode.F);
#endif

        if (presionada) luz.enabled = !luz.enabled;
    }
}