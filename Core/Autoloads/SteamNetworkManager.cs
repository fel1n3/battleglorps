using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using BattleGlorps;
using BattleGlorps.Classes;
using Steamworks;

public partial class SteamNetworkManager : Node
{
    public static SteamNetworkManager Instance { get; private set; }

    public event Action<List<LobbyInfo>> OnLobbyListUpdated;
    public event Action<CSteamID> OnPlayerConnected;
    public event Action<CSteamID> OnPlayerDisconnected;
    public event Action<string> OnChatMessageReceived;
    public event Action<string> OnConnectionStatusMessage;
    
    
    private HSteamListenSocket _listenSocket = HSteamListenSocket.Invalid;
    private readonly List<HSteamNetConnection> _connections = new();
    private Callback<SteamNetConnectionStatusChangedCallback_t> _connectionStatusChangedCallback;
    private const int KMaxMessagesPerPoll = 32;

    private CSteamID _currentLobbyId;
    private CallResult<LobbyCreated_t> _lobbyCreated;
    private CallResult<LobbyMatchList_t> _lobbyList;
    private CallResult<LobbyEnter_t> _lobbyEnter;
    private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequested;

    private byte _localNetworkId = 0;
    private byte _nextNetworkId = 1;
    
    [Export] private PackedScene _playerScene;
    [Export] private Node3D _spawnParent;
    private Dictionary<byte, NetworkedPlayer> _playerList = new();
    private Dictionary<HSteamNetConnection, byte> _connToNetId = new();
    public override void _Ready()
    {
        Instance = this;

        GD.Print("init steam api");

        if (!SteamAPI.Init())
        {
            GD.PrintErr("steamapi init failed is steam running");
            return;
        }

        _connectionStatusChangedCallback =
            Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);

        _gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        
    }

    private void CreateP2PHost()
    {
        if (_listenSocket != HSteamListenSocket.Invalid) return;
        try
        {
            _listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
            GD.Print($"steam created listen socket: {_listenSocket.ToString()}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"steam createlistensocketp2p threw: {e}");
        }
    }

    public override void _Process(double delta)
    {
        SteamAPI.RunCallbacks();
        PollIncomingMessages();
    }

    public void CreateLobby()
    {
        SteamAPICall_t call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 4);
        _lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyCreated.Set(call);
        OnConnectionStatusMessage?.Invoke("Creating lobby...");
    }

    private void OnLobbyCreated(LobbyCreated_t param, bool bIOFailure)
    {
        if (param.m_eResult != EResult.k_EResultOK || bIOFailure) return;

        _currentLobbyId = new CSteamID(param.m_ulSteamIDLobby);
        OnConnectionStatusMessage?.Invoke($"Lobby created id: {_currentLobbyId}");

        CreateP2PHost();

        SteamMatchmaking.SetLobbyData(_currentLobbyId, "HostAddress", SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(_currentLobbyId, "Name", SteamFriends.GetPersonaName() + "'s Lobby");

        SteamMatchmaking.SetLobbyData(_currentLobbyId, "game_id", "battle_glorps_v1");
    }

    public void GetLobbyList()
    {
        OnConnectionStatusMessage?.Invoke("Searching for lobbies.....");

        SteamMatchmaking.AddRequestLobbyListStringFilter("game_id", "battle_glorps_v1",
            ELobbyComparison.k_ELobbyComparisonEqual);
        
        SteamAPICall_t call = SteamMatchmaking.RequestLobbyList();
        _lobbyList = CallResult<LobbyMatchList_t>.Create(OnLobbyListReceived);
        _lobbyList.Set(call);
    }

    private void OnLobbyListReceived(LobbyMatchList_t param, bool bIOFailure)
    {
        List<LobbyInfo> foundLobbies = new List<LobbyInfo>();

        for (int i = 0; i < param.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            foundLobbies.Add(new LobbyInfo
            {
                LobbyId = lobbyId.m_SteamID,
                Name=SteamMatchmaking.GetLobbyData(lobbyId, "Name"),
                PlayerCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId),
                MaxPlayers =  SteamMatchmaking.GetLobbyMemberLimit(lobbyId)
            });
        }

        OnLobbyListUpdated?.Invoke(foundLobbies);
    }

    public void JoinLobby(ulong lobbyId)
    {
        CSteamID steamLobbyId = new(lobbyId);
        SteamAPICall_t call = SteamMatchmaking.JoinLobby(steamLobbyId);
        _lobbyEnter = CallResult<LobbyEnter_t>.Create(OnLobbyEntered);
        _lobbyEnter.Set(call);
    }

    public ulong GetCurrentLobbyID()
    {
        return _currentLobbyId.m_SteamID;
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t param)
    {
        JoinLobby(param.m_steamIDLobby.m_SteamID);
    }

    private void OnLobbyEntered(LobbyEnter_t param, bool bIOFailure)
    {
        if (param.m_EChatRoomEnterResponse != (uint) EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess) return;

        _currentLobbyId = new CSteamID(param.m_ulSteamIDLobby);
        OnConnectionStatusMessage?.Invoke("joined lobby checking host");

        if (SteamMatchmaking.GetLobbyOwner(_currentLobbyId) == SteamUser.GetSteamID())
        {
            if (_listenSocket == HSteamListenSocket.Invalid) CreateP2PHost();
        }
        else
        {
            string hostAddress = SteamMatchmaking.GetLobbyData(_currentLobbyId, "HostAddress");
            if (ulong.TryParse(hostAddress, out ulong hostId))
            {
                ConnectToPeer(hostId);
            }
        }
    }

    public void ConnectToPeer(ulong hostSteamId, int virtualPort = 0)
    {
        if (!SteamAPI.IsSteamRunning())
        {
            GD.PrintErr("steam not running");
            return;
        }
        
        GD.Print($"attempting to join steamid {hostSteamId}");

        SteamNetworkingIdentity identity = new();
        identity.SetSteamID(new CSteamID(hostSteamId));

        HSteamNetConnection conn = SteamNetworkingSockets.ConnectP2P(ref identity, virtualPort, 0, null);
        _connections.Add(conn);
    }

    private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t param)
    {
        CSteamID clientSteamId = param.m_info.m_identityRemote.GetSteamID();

        switch (param.m_info.m_eState)
        {
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                GD.Print($"Connection request from {clientSteamId}");

                if (_listenSocket != HSteamListenSocket.Invalid)
                {
                    var res = SteamNetworkingSockets.AcceptConnection(param.m_hConn);
                    if (res == EResult.k_EResultOK)
                    {
                        GD.Print("connx accepted");
                    }
                    else
                    {
                        GD.PrintErr($"connx accept failed {res}");
                    }
                }

                break;

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                GD.Print($"connected to {clientSteamId}");

                if (_listenSocket != HSteamListenSocket.Invalid)
                {
                    byte assignedId = _nextNetworkId++;
                    _connToNetId[param.m_hConn] = assignedId;

                    GD.Print($"host: assigning networkdid {assignedId} to steamid {clientSteamId}");

                    SendHandshake(param.m_hConn, assignedId);

                    foreach (var player in _playerList.Values)
                    {
                        SendSpawnTo(param.m_hConn, player.NetworkId, player.SteamId, player.GlobalPosition);
                    }

                    //SpawnPlayer(assignedId, clientSteamId, Vector3.Zero, isLocal: false);
                    BroadcastSpawn(assignedId, clientSteamId, Vector3.Zero);
                }
                OnPlayerConnected?.Invoke(clientSteamId);
                OnConnectionStatusMessage?.Invoke($"player connected: {clientSteamId}");
                break;

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                GD.Print($"connx closed reason: {param.m_info.m_eEndReason}");
                if (_connToNetId.TryGetValue(param.m_hConn, out byte netId))
                {
                    RemovePlayer(netId);
                    _connToNetId.Remove(param.m_hConn);
                }
                CloseConnection(param.m_hConn);

                OnPlayerDisconnected?.Invoke(clientSteamId);
                OnConnectionStatusMessage?.Invoke($"player disconnected {clientSteamId}");
                break;
        }
    }

    public void StartGameAsHost()
    {
        _localNetworkId = 0;
       // SpawnPlayer(0, SteamUser.GetSteamID(), new Vector3(0, 2, 0), isLocal: true);
    }
    
    private void SpawnPlayer(byte netId, CSteamID steamId, Vector3 pos, bool isLocal)
    {
        if (_playerList.ContainsKey(netId)) return;

        var p = _playerScene.Instantiate<NetworkedPlayer>();
        p.Name = $"Player_{netId}";
        p.NetworkId = netId;
        p.SteamId = steamId;
        p.IsLocalPlayer = isLocal;
        p.Position = pos;

        _spawnParent.AddChild(p);
        _playerList.Add(netId, p);
        
        GD.Print($"spawned player with netid {netId} (local: {isLocal}");
    }

    private void RemovePlayer(byte netId)
    {
        if (_playerList.TryGetValue(netId, out NetworkedPlayer p))
        {
            p.QueueFree();
            _playerList.Remove(netId);
        }
    }


    public void SendBytesTo(HSteamNetConnection conn, byte[] data,
        int flags = Constants.k_nSteamNetworkingSend_Reliable)
    {
        if (conn == HSteamNetConnection.Invalid) return;
        
        GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
        IntPtr dataPtr = pinnedArray.AddrOfPinnedObject();

        SteamNetworkingSockets.SendMessageToConnection(
            conn, 
            dataPtr, 
            (uint) data.Length, 
            flags, 
            out long _);

        pinnedArray.Free();
        
    }

    public void SendBytesToAll(byte[] data, int flags = Constants.k_nSteamNetworkingSend_Reliable)
    {
        foreach (HSteamNetConnection conn in _connections)
        {
            SendBytesTo(conn, data, flags);
        }
    }


    private void PollIncomingMessages()
    {
        for (int i = _connections.Count - 1; i >= 0; --i)
        {
            var conn = _connections[i];
            if (conn == HSteamNetConnection.Invalid) continue;

            IntPtr[] msgBuffer = new IntPtr[KMaxMessagesPerPoll];

            int numMsgs = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, msgBuffer, KMaxMessagesPerPoll);
            
            for (int j = 0; j < numMsgs; j++)
            {
                try
                {
                    ProcessMessage(msgBuffer[j]);
                }
                catch (Exception e)
                {
                    GD.PrintErr($"failed to process packet: {e.Message}");
                }
                finally
                {
                    SteamNetworkingMessage_t.Release(msgBuffer[j]);
                }
            }
        }
    }

    private void ProcessMessage(IntPtr msgPtr)
    {
        var netMsg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msgPtr);

        byte[] data = new byte[netMsg.m_cbSize];
        Marshal.Copy(netMsg.m_pData, data, 0, netMsg.m_cbSize);

        CSteamID senderSteamId = netMsg.m_identityPeer.GetSteamID();

        using MemoryStream ms = new(data);
        using BinaryReader reader = new(ms);

        PacketType type = (PacketType) reader.ReadByte();


        switch (type)
        {
            case PacketType.Handshake:
                _localNetworkId = reader.ReadByte();
                GD.Print($"client: i have been assigned netidd: {_localNetworkId}");
                    // SpawnPlayer(_localNetworkId, SteamUser.GetSteamID(), Vector3.Zero, true);
                break;
            
            case PacketType.SpawnPlayer:
                byte spawnNetId = reader.ReadByte();
                ulong spawnSteamId = reader.ReadUInt64();
                Vector3 spawnPos = reader.ReadVector3();

                if (spawnNetId != _localNetworkId && !_playerList.ContainsKey(spawnNetId))
                {
                    //SpawnPlayer(spawnNetId, (CSteamID)spawnSteamId, spawnPos, false);
                }

                break;
            case PacketType.PlayerUpdate:
                byte updateNetId = reader.ReadByte();
                Vector3 newPos = reader.ReadVector3();
                Vector3 newRot = reader.ReadVector3();

                if (_playerList.TryGetValue(updateNetId, out NetworkedPlayer p))
                {
                    if (!p.IsLocalPlayer)
                    {
                        p.UpdateRemoteState(newPos, newRot);
                    }
                }

                if (_listenSocket != HSteamListenSocket.Invalid)
                {
                    SendBytesToAll(data, Constants.k_nSteamNetworkingSend_Unreliable);
                }

                break;
            case PacketType.ClassSelected:
                //class selection stuff
                break;
            case PacketType.AbilityCast:
                byte playerNetId = reader.ReadByte();
                string abilityName = reader.ReadString();
                HandleAbilityCast(playerNetId, abilityName);
                break;
                
        }
        
    }

    private void SendHandshake(HSteamNetConnection target, byte assignedId)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.Handshake);
        writer.Write(assignedId);
        SendBytesTo(target, ms.ToArray(), Constants.k_nSteamNetworkingSend_Reliable);
    }

    private void HandleAbilityCast(byte netId, string abilityName)
    {
        if (_playerList.ContainsKey(netId))
        {
            NetworkedPlayer playerNode = _playerList[netId];
            playerNode.GetNode<AbilityController>("AbilityController").ExecuteVisualsOnly(abilityName);
        }
    }

    private void BroadcastSpawn(byte netId, CSteamID steamId, Vector3 pos)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.SpawnPlayer);
        writer.Write(netId);
        writer.Write(steamId.m_SteamID);
        writer.Write(pos);
        SendBytesToAll(ms.ToArray(), Constants.k_nSteamNetworkingSend_Reliable);
    }

    private void SendSpawnTo(HSteamNetConnection target, byte netId, CSteamID steamId, Vector3 pos)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.SpawnPlayer);
        writer.Write(netId);
        writer.Write(steamId.m_SteamID);
        writer.Write(pos);
        SendBytesTo(target, ms.ToArray(), Constants.k_nSteamNetworkingSend_Reliable);
    }

    public void CloseConnection(HSteamNetConnection conn)
    {
        if (_connections.Contains(conn))
        {
            SteamNetworkingSockets.CloseConnection(conn, 0, "Closing", false);
            _connections.Remove(conn);
            GD.Print($"connx {conn.m_HSteamNetConnection} removed.");
        }
    }

    public void ShutdownAll()
    {
        foreach (var conn in _connections)
        {
            SteamNetworkingSockets.CloseConnection(conn, 0, "shutting down", false);
        }

        _connections.Clear();

        if (_listenSocket != HSteamListenSocket.Invalid)
        {
            SteamNetworkingSockets.CloseListenSocket(_listenSocket);
            _listenSocket = HSteamListenSocket.Invalid;
        }
    }
    
    
    public override void _ExitTree()
    {
        ShutdownAll();

        SteamAPI.Shutdown();

    }


}

public struct LobbyInfo
{
    public ulong LobbyId;
    public string Name;
    public int PlayerCount;
    public int MaxPlayers;
}