using UnityEngine;

public class TrainCollision : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("🚂 ¡EL TREN CHOCÓ CONTRA " + collision.gameObject.name + "!");

        if (collision.gameObject.CompareTag("Bridge"))
        {
            Debug.Log("💥 ¡ES UN PUENTE!");

            Destroy(collision.gameObject);
        }
    }
}