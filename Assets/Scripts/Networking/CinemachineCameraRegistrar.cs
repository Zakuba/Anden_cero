using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineCamera))]
public class CinemachineCameraRegistrar : MonoBehaviour
{
    public static CinemachineCamera Instance { get; private set; }

    private void Awake()
    {
        Instance = GetComponent<CinemachineCamera>();
    }

    private void OnDestroy()
    {
        if (Instance == GetComponent<CinemachineCamera>())
        {
            Instance = null;
        }
    }
}