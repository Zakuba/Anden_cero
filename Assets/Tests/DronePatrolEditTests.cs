using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class DronePatrolEditTests
{
    // Mock para simular la lógica de patrullaje e IA de drones en red
    private class DronePatrolMock
    {
        public bool IsHost { get; set; } = true;
        public Vector3 CurrentPosition { get; set; } = Vector3.zero;
        public List<Vector3> Waypoints { get; private set; } = new List<Vector3>();
        public int CurrentWaypointIndex { get; private set; } = 0;
        public Vector3 TargetDestination { get; private set; }
        public bool IsPlayerInSight { get; set; } = false;
        public bool IsPatrollingRandomly { get; private set; } = false;

        public DronePatrolMock(bool isHost = true)
        {
            IsHost = isHost;
        }

        public void SetWaypoints(List<Vector3> waypoints)
        {
            Waypoints = waypoints;
            if (Waypoints.Count > 0)
            {
                TargetDestination = Waypoints[0];
            }
        }

        public bool CalculatePathfinding()
        {
            // Criterio de aceptación: El pathfinding ocurre exclusivamente en el Host
            if (!IsHost) return false;

            if (!IsPlayerInSight)
            {
                if (Waypoints != null && Waypoints.Count > 0)
                {
                    IsPatrollingRandomly = false;
                    TargetDestination = Waypoints[CurrentWaypointIndex];
                }
                else
                {
                    // Si no hay waypoints predefinidos, patrulla de forma aleatoria
                    IsPatrollingRandomly = true;
                    TargetDestination = CurrentPosition + new Vector3(
                        UnityEngine.Random.Range(-5f, 5f),
                        0f,
                        UnityEngine.Random.Range(-5f, 5f)
                    );
                }
            }

            return true;
        }

        public void AdvanceToNextWaypoint()
        {
            if (!IsHost || Waypoints == null || Waypoints.Count == 0) return;

            CurrentWaypointIndex = (CurrentWaypointIndex + 1) % Waypoints.Count;
            TargetDestination = Waypoints[CurrentWaypointIndex];
        }
    }

    private DronePatrolMock hostDrone;
    private DronePatrolMock clientDrone;

    [SetUp]
    public void SetUp()
    {
        hostDrone = new DronePatrolMock(isHost: true);
        clientDrone = new DronePatrolMock(isHost: false);
    }

    [Test]
    public void Pathfinding_ExecutesOnlyOnHost_ClientIsRejected()
    {
        bool hostCalculated = hostDrone.CalculatePathfinding();
        bool clientCalculated = clientDrone.CalculatePathfinding();

        Assert.IsTrue(hostCalculated, "El Host debe ser capaz de calcular el pathfinding del dron.");
        Assert.IsFalse(clientCalculated, "El Cliente no debe calcular pathfinding (debe recibir la posición sincronizada por red).");
    }

    [Test]
    public void Drone_PatrolsWaypointsSequentially()
    {
        var waypoints = new List<Vector3>
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(10, 0, 10)
        };

        hostDrone.SetWaypoints(waypoints);
        hostDrone.CalculatePathfinding();

        Assert.AreEqual(waypoints[0], hostDrone.TargetDestination, "El dron debe dirigirse al primer waypoint de la lista.");

        hostDrone.AdvanceToNextWaypoint();
        Assert.AreEqual(waypoints[1], hostDrone.TargetDestination, "El dron debe avanzar secuencialmente al segundo waypoint.");
    }

    [Test]
    public void Drone_LoopsWaypoints_WhenReachingEnd()
    {
        var waypoints = new List<Vector3>
        {
            new Vector3(0, 0, 0),
            new Vector3(5, 0, 0)
        };

        hostDrone.SetWaypoints(waypoints);
        hostDrone.AdvanceToNextWaypoint(); // Pasa al waypoint 1
        hostDrone.AdvanceToNextWaypoint(); // Debe volver al waypoint 0

        Assert.AreEqual(waypoints[0], hostDrone.TargetDestination, "Al completar el circuito de waypoints, el dron debe volver al primero.");
    }

    [Test]
    public void Drone_NoWaypointsAndNoPlayerInSight_PatrolsRandomly()
    {
        hostDrone.SetWaypoints(new List<Vector3>()); // Lista de waypoints vacía
        hostDrone.IsPlayerInSight = false;

        hostDrone.CalculatePathfinding();

        Assert.IsTrue(hostDrone.IsPatrollingRandomly, "Si no hay waypoints y no hay jugador a la vista, el dron debe activar el modo aleatorio.");
        Assert.AreNotEqual(Vector3.zero, hostDrone.TargetDestination, "El dron debe generar un destino aleatorio válido distinto a su origen.");
    }

    [Test]
    public void Drone_WithWaypointsAndNoPlayerInSight_PrefersWaypointsOverRandom()
    {
        var waypoints = new List<Vector3> { new Vector3(2, 0, 2) };
        hostDrone.SetWaypoints(waypoints);
        hostDrone.IsPlayerInSight = false;

        hostDrone.CalculatePathfinding();

        Assert.IsFalse(hostDrone.IsPatrollingRandomly, "Si hay waypoints predefinidos, el dron no debe usar patrullaje aleatorio.");
        Assert.AreEqual(waypoints[0], hostDrone.TargetDestination, "El destino del dron debe ser el waypoint definido.");
    }

    [Test]
    public void Drone_ClientCannotAdvanceWaypointsLocally()
    {
        var waypoints = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        clientDrone.SetWaypoints(waypoints);

        clientDrone.AdvanceToNextWaypoint();

        Assert.AreEqual(0, clientDrone.CurrentWaypointIndex, "El cliente no debe modificar los índices de waypoints por su cuenta.");
    }

    [Test]
    public void Drone_PlayerInSight_StopsStandardPatrolPathfinding()
    {
        var waypoints = new List<Vector3> { new Vector3(5, 0, 5) };
        hostDrone.SetWaypoints(waypoints);
        hostDrone.IsPlayerInSight = true;

        hostDrone.CalculatePathfinding();

        Assert.IsFalse(hostDrone.IsPatrollingRandomly, "Al detectar al jugador, la IA interrumpe el patrullaje automático para pasar a persecución/ataque.");
    }
}