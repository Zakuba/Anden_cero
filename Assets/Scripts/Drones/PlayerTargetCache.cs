using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lista compartida de jugadores para los drones (solo servidor).
/// Se refresca como mucho cada RefreshInterval segundos para TODOS los drones juntos,
/// en vez de que cada dron busque jugadores por su cuenta en cada frame.
///
/// La lista puede tener jugadores destruidos (null) o muertos entre un refresco y otro:
/// quien la use debe comprobarlo.
/// </summary>
public static class PlayerTargetCache
{
    private const float RefreshInterval = 0.5f;

    private static readonly List<PlayerStateManager> players = new List<PlayerStateManager>();
    private static float lastRefreshTime = -999f;

    public static IReadOnlyList<PlayerStateManager> Players
    {
        get
        {
            // Time.time < lastRefreshTime cubre el caso de entrar a Play sin recargar el dominio
            if (Time.time < lastRefreshTime || Time.time - lastRefreshTime >= RefreshInterval)
                Refresh();

            return players;
        }
    }

    private static void Refresh()
    {
        lastRefreshTime = Time.time;

        players.Clear();
        players.AddRange(Object.FindObjectsByType<PlayerStateManager>(FindObjectsSortMode.None));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        players.Clear();
        lastRefreshTime = -999f;
    }
}
