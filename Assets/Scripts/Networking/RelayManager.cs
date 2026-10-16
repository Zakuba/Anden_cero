using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public enum ResultadoUnirse
{
    Ok,
    SinInternet,
    SalaNoEncontrada,
    Error
}

public static class RelayManager
{
    // Tipo de conexión: "dtls" cifra los datos (opciones válidas: udp, dtls, wss)
    private const string ConnectionType = "dtls";

    // Código de la sala actual (solo el host lo tiene). Es estático para que
    // sobreviva al cambio de escena y la sala de espera pueda mostrarlo.
    public static string JoinCodeActual { get; private set; }

    // HOST: Reserva una sala en Relay y devuelve el código para compartir.
    // Devuelve null si algo falla.
    public static async Task<string> CreateRelay(int maxConnections)
    {
        // Limpiamos el código anterior para que no quede uno viejo si esto falla
        JoinCodeActual = null;

        if (!UnityServicesInitializer.IsReady)
        {
            Debug.LogError("Unity Services todavía no está listo.");
            return null;
        }

        try
        {
            // 1) Pedimos a Relay que reserve una sala
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            // 2) Obtenemos el código que los demás jugadores van a escribir
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // 3) Le decimos al transporte de red que use Relay en lugar de una IP
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

            // 4) Guardamos el código solo cuando todo salió bien
            JoinCodeActual = joinCode;

            Debug.Log("Sala de Relay creada. Código: " + joinCode);
            return joinCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError("No se pudo crear la sala de Relay: " + e.Message);
            return null;
        }
    }

    // CLIENTE: se une a una sala con el código. Devuelve true si salió bien.
public static async Task<ResultadoUnirse> JoinRelay(string joinCode)
{
    // Sin red, los servicios nunca van a estar listos: lo avisamos con claridad
    if (Application.internetReachability == NetworkReachability.NotReachable)
    {
        Debug.LogError("Sin conexión a internet.");
        return ResultadoUnirse.SinInternet;
    }

    if (!UnityServicesInitializer.IsReady)
    {
        Debug.LogError("Unity Services todavía no está listo.");
        return ResultadoUnirse.Error;
    }

    try
    {
        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

        Debug.Log("Unido a la sala de Relay.");
        return ResultadoUnirse.Ok;
    }
    catch (RelayServiceException e)
    {
        Debug.LogError("No se pudo unir a la sala de Relay: " + e.Message);

        // Relay indica con "Reason" qué pasó; si el código no existe, lo decimos
        if (e.Reason == RelayExceptionReason.JoinCodeNotFound)
            return ResultadoUnirse.SalaNoEncontrada;

        return ResultadoUnirse.Error;
    }
    catch (System.Exception e)
    {
        Debug.LogError("No se pudo unir a la sala de Relay: " + e.Message);
        return ResultadoUnirse.Error;
    }
}
}