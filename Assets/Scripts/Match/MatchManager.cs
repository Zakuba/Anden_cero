using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    [Header("UI de Resultados")]
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TMP_Text resultsText;

    private readonly List<PlayerStateManager> registeredPlayers = new List<PlayerStateManager>();
    private bool matchStarted = false;
    private bool matchEnded = false;
    private bool checkScheduled = false;

    private void Awake()
    {
        Instance = this;
    }

   public override void OnNetworkSpawn()
{
    if (resultsPanel != null)
    {
        resultsPanel.SetActive(false);
    }

    if (IsServer)
    {
        // Los jugadores ya pueden existir desde antes (spawnearon en el Lobby),
        // asi que los buscamos y registramos apenas el MatchManager aparece en MainGame.
        PlayerStateManager[] existingPlayers = FindObjectsByType<PlayerStateManager>(FindObjectsSortMode.None);
        foreach (PlayerStateManager player in existingPlayers)
        {
            RegisterPlayer(player);
        }
    }
}

    public void RegisterPlayer(PlayerStateManager player)
    {
        if (!IsServer) return;
        if (registeredPlayers.Contains(player)) return;

        registeredPlayers.Add(player);
        player.currentState.OnValueChanged += OnPlayerStateChanged;

        if (!matchStarted && registeredPlayers.Count >= 2)
        {
            matchStarted = true;
        }
    }

    private void OnPlayerStateChanged(PlayerState previous, PlayerState current)
    {
        if (!IsServer || !matchStarted || matchEnded) return;
        if (current != PlayerState.Muerto) return;

        if (!checkScheduled)
        {
            checkScheduled = true;
            StartCoroutine(CheckVictoryNextFrame());
        }
    }

 private IEnumerator CheckVictoryNextFrame()
{
    yield return null;
    checkScheduled = false;

    if (matchEnded) yield break;

    List<PlayerStateManager> alivePlayers = registeredPlayers.FindAll(
        p => p != null && p.currentState.Value != PlayerState.Muerto);

    if (alivePlayers.Count == 1)
    {
        matchEnded = true;
        NetworkObjectReference winnerRef = alivePlayers[0].NetworkObject;
        AnnounceResultClientRpc(alivePlayers[0].OwnerClientId, winnerRef, false);
    }
    else if (alivePlayers.Count == 0)
    {
        matchEnded = true;
        AnnounceResultClientRpc(0, default, true);
    }
}

[ClientRpc]
private void AnnounceResultClientRpc(ulong winnerClientId, NetworkObjectReference winnerRef, bool isDraw)
{
    if (resultsPanel != null) resultsPanel.SetActive(true);

    if (resultsText != null)
    {
        if (isDraw) resultsText.text = "¡Empate!";
        else if (winnerClientId == NetworkManager.Singleton.LocalClientId) resultsText.text = "¡Ganaste!";
        else resultsText.text = $"Perdiste. Jugador {winnerClientId} gana la partida.";
    }

    if (!isDraw && winnerRef.TryGet(out NetworkObject winnerObj))
    {
        // Aca Tomas puede disparar la animación de festejo, por ejemplo:
        // winnerObj.GetComponent<Animator>().SetTrigger("Festejar");
         
    }
}
}