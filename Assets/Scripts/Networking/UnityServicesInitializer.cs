using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public static class UnityServicesInitializer
{
    public static bool IsReady { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static async void Init()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            IsReady = true;
            Debug.Log("Unity Services listo. PlayerID: " + AuthenticationService.Instance.PlayerId);
        }
        catch (System.Exception e)
        {
            IsReady = false;
            Debug.LogError("No se pudo iniciar Unity Services: " + e.Message);
        }
    }
}