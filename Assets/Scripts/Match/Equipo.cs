using System;
using Unity.Netcode;

public enum Equipo { Alfa = 0, Beta = 1 }

// Una fila de la lista sincronizada: qué equipo tiene cada cliente.
public struct EquipoEntry : INetworkSerializable, IEquatable<EquipoEntry>
{
    public ulong clientId;
    public int equipo;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref equipo);
    }

    public bool Equals(EquipoEntry other)
    {
        return clientId == other.clientId && equipo == other.equipo;
    }
}