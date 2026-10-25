using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerAvatarNet : NetworkBehaviour
{
    [SerializeField] RawImage avatarImage;

    byte[] serverCopy;       // solo lo usa el servidor
    Texture2D currentTex;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            var png = AvatarStorage.LoadPng();
            if (png != null && png.Length > 0)
            {
                Show(png);
                SubmitAvatarServerRpc(png);
            }
        }
        else
        {
            RequestAvatarServerRpc();
        }
    }

    [ServerRpc]
    void SubmitAvatarServerRpc(byte[] png)
    {
        serverCopy = png;
        ApplyAvatarClientRpc(png);
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestAvatarServerRpc(ServerRpcParams p = default)
    {
        if (serverCopy == null) return;
        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { p.Receive.SenderClientId } }
        };
        ApplyAvatarClientRpc(serverCopy, target);
    }

    [ClientRpc]
    void ApplyAvatarClientRpc(byte[] png, ClientRpcParams p = default)
    {
        if (IsOwner) return;
        Show(png);
    }

    void Show(byte[] png)
    {
        if (currentTex != null) Destroy(currentTex);
        currentTex = new Texture2D(2, 2);
        currentTex.LoadImage(png);
        avatarImage.texture = currentTex;
        avatarImage.enabled = true;
    }

    void OnDestroy()
    {
        if (currentTex != null) Destroy(currentTex);
    }
}