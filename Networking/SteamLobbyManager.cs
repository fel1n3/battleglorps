using System;
using System.Collections.Generic;
using BattleGlorps.Core.Autoloads;
using Godot;
using Steamworks;

namespace BattleGlorps;

public partial class SteamLobbyManager : Node
{
    public CSteamID CurrentLobbyId { get; private set; }

    public event Action<CSteamID> OnLobbyCreated;
    public event Action<List<LobbyInfo>> OnLobbyListUpdated;
    public event Action<CSteamID> OnLobbyJoined;
    public event Action<string> OnStatusMessage;

    private CallResult<LobbyCreated_t> _lobbyCreated;
    private CallResult<LobbyMatchList_t> _lobbyList;
    private CallResult<LobbyEnter_t> _lobbyEnter;
    private Callback<GameLobbyJoinRequested_t> _joinRequest;

    public override void _Ready()
    {
        _joinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
    }

    public void CreateLobby()
    {
        OnStatusMessage?.Invoke("Creating lobby...");
        var call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 4);
        _lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreatedCallback);
        _lobbyCreated.Set(call);
    }

    private void OnLobbyCreatedCallback(LobbyCreated_t param, bool bIOFailure)
    {
        if (param.m_eResult != EResult.k_EResultOK || bIOFailure)
        {
            OnStatusMessage?.Invoke("Failed to create lobby.");
            return;
        }

        CurrentLobbyId = new CSteamID(param.m_ulSteamIDLobby);
        OnStatusMessage?.Invoke($"Lobby created: {CurrentLobbyId}");

        SteamMatchmaking.SetLobbyData(CurrentLobbyId, "HostAddress", SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(CurrentLobbyId, "Name", SteamFriends.GetPersonaName() + "'s Lobby");
        SteamMatchmaking.SetLobbyData(CurrentLobbyId, "game_id", "battle_glorps");

        OnLobbyJoined?.Invoke(CurrentLobbyId);
        OnLobbyCreated?.Invoke(CurrentLobbyId);
    }

    public void RefreshLobbyList()
    {
        SteamMatchmaking.AddRequestLobbyListStringFilter("game_id", "battle_glorps", ELobbyComparison.k_ELobbyComparisonEqual);
        var call = SteamMatchmaking.RequestLobbyList();

        _lobbyList = CallResult<LobbyMatchList_t>.Create((param, bIoFailure) =>
        {
            var results = new List<LobbyInfo>();
            for (int i = 0; i < param.m_nLobbiesMatching; i++)
            {
                var id = SteamMatchmaking.GetLobbyByIndex(i);
                results.Add(new LobbyInfo
                {
                    LobbyId = id.m_SteamID,
                    Name = SteamMatchmaking.GetLobbyData(id, "Name"),
                    PlayerCount = SteamMatchmaking.GetNumLobbyMembers(id),
                    MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(id)
                });
            }

            OnLobbyListUpdated?.Invoke(results);
        });
        _lobbyList.Set(call);
    }

    public void JoinLobby(CSteamID lobbyId)
    {
        var call = SteamMatchmaking.JoinLobby(lobbyId);
        _lobbyEnter = CallResult<LobbyEnter_t>.Create((param, bIoFailure) =>
        {
            if (param.m_EChatRoomEnterResponse == (uint) EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                CurrentLobbyId = new CSteamID(param.m_ulSteamIDLobby);

                var ownerId = SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);

                if (ownerId != SteamUser.GetSteamID())
                {
                    SteamManager.Instance.Connection.ConnectToPeer(ownerId);
                }

                OnLobbyJoined?.Invoke(CurrentLobbyId);
            }
        });
        _lobbyEnter.Set(call);
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t param)
    {
        JoinLobby(param.m_steamIDLobby);
    }
}
