
using UnityEngine;

public class LobbySpawnPoint : MonoBehaviour
{
    [Header("Animación del lobby")]
    [Tooltip("0 = Idle, 1 = Sentado, 2 = Parado, 3 = Celular")]
    [SerializeField] private int lobbyAnimation = 0;

    public int LobbyAnimation => lobbyAnimation;
}