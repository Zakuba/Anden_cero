
using UnityEngine;

public class LobbySpawnPoint : MonoBehaviour
{
    [Header("Animación del lobby")]
    [SerializeField] private int lobbyAnimation = 0;

    public int LobbyAnimation => lobbyAnimation;
}