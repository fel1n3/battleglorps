using Godot;
using System;
using System.Collections.Generic;
using Steamworks;

public partial class SteamNetworkManager : Node
{
    public static SteamNetworkManager Instance { get; private set; }

    private bool _isSteamInitialized = false;
    public CSteamID LocalSteamId { get; private set; }

    public bool IsServer { get; private set; } = false;
    public CSteamID HostSteamId { get; private set; }
    private Dictionary<CSteamID, int> _steamIdToPlayerId = new Dictionary<CSteamID, int>();
    private Dictionary<int, CSteamID> _playerIdToSteamId = new Dictionary<int, CSteamID>();
    private int _nextPlayerId = 1;

    private Callback<P2PSessionRequest_t> _p2pSessionRequest;
    private Callback<P2PSessionConnectFail_t> _p2pSessionConnectFail;

    public event Action<int, CSteamID> PlayerConnected;
    public event Action<int> PlayerDisconnected;
    public event Action ServerCreated;
    public event Action JoinedServer;
    public event Action<string> ConnectionFailed;

    public override void _Ready()
    {
        if (Instance != null)
        {
            QueueFree();
            return;
        }

        Instance = this;

        InitializeSteam();
    }

    private void InitializeSteam()
    {
        try
        {
            if (!SteamAPI.Init())
            {
                GD.PrintErr("failed to init steam api! steam not running?");
                return;
            }

            _isSteamInitialized = true;
            LocalSteamId = SteamUser.GetSteamID();

            GD.Print($"Steam initialized steam id: {LocalSteamId}");

            _p2pSessionRequest = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
            _p2pSessionConnectFail = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionConnectFail);
        }
        catch (Exception e)
        {
            GD.PrintErr($"steam init err: {e.Message}");
        }
    }

    public override void _Process(double delta)
    {
        if (!_isSteamInitialized) return;
        
        SteamAPI.RunCallbacks();
        ReceiveP2PPackets();
    }

    public override void _ExitTree()
    {
        if (!_isSteamInitialized) return;
        
        CloseAllConnections();
        SteamAPI.Shutdown();
    }

    public void CreateServer()
    {
        if (!_isSteamInitialized)
        {
            GD.PrintErr("steam not init");
            ConnectionFailed?.Invoke("steam not init");
            return;
        }

        IsServer = true;
        HostSteamId = LocalSteamId;

        _steamIdToPlayerId[LocalSteamId] = 0;
        _playerIdToSteamId[0] = LocalSteamId;
        
        GD.Print($"Server created! host steam id:{HostSteamId}");
        ServerCreated?.Invoke();
        PlayerConnected?.Invoke(0, LocalSteamId);
    }

    public void JoinServer(CSteamID hostSteamId)
    {
        if (!_isSteamInitialized)
        {
            GD.PrintErr("Steam not init");
            ConnectionFailed?.Invoke("steam not init");
            return;
        }

        IsServer = false;
        HostSteamId = hostSteamId;
        
        GD.Print($"Attempting to join server: {hostSteamId}");

        SendPacketToSteamId(hostSteamId, PacketType.ConnectionRequest, new byte[] { });
    }

    public void JoinServerByString(string steamIdString)
    {
        if (ulong.TryParse(steamIdString, out ulong steamId))
        {
            JoinServer(new CSteamID(steamId));
            
        }
        else
        {
            GD.PrintErr($"invalid steam id format: {steamIdString}");
            ConnectionFailed?.Invoke("invalid steam id");
        }
    }

    public void CreateLobby()
    {
        
    }

    private void OnP2PSessionRequest(P2PSessionRequest_t request)
    {
        CSteamID requesterId = request.m_steamIDRemote;
        GD.Print($"p2p request from: {requesterId}");

        SteamNetworking.AcceptP2PSessionWithUser(requesterId);

        if (!IsServer) return;
        
        int playerId = _nextPlayerId++;
        _steamIdToPlayerId[requesterId] = playerId;
        _playerIdToSteamId[playerId] = requesterId;
            
        GD.Print($"player {playerId} connected {requesterId}");

        SendPlayerIdToClient(requesterId, playerId);
        BroadcastPlayerJoined(playerId, requesterId);
        PlayerConnected?.Invoke(playerId, requesterId);
    }

    private void OnP2PSessionConnectFail(P2PSessionConnectFail_t failure)
    {
        GD.PrintErr($"p2p connx fail: {failure.m_eP2PSessionError}");
        ConnectionFailed?.Invoke($"p2p error {failure.m_eP2PSessionError}");
    }

    private void ReceiveP2PPackets()
    {
        while (SteamNetworking.IsP2PPacketAvailable(out uint packetSize))
        {
            byte[] data = new byte[packetSize];

            if (SteamNetworking.ReadP2PPacket(data, packetSize, out packetSize, out CSteamID senderId))
            {
                HandlePacket(senderId, data);
            }
        }
    }

    private void HandlePacket(CSteamID senderId, byte[] data)
    {
        if (data.Length < 1) return;

        PacketType packetType = (PacketType) data[0];
        byte[] payload = new byte[data.Length - 1];
        Array.Copy(data, 1, payload, 0, payload.Length);

        switch (packetType)
        {
            case PacketType.ConnectionRequest:
                break;
            
            case PacketType.AssignPlayerId:
                if (!IsServer)
                {
                    int playerId = BitConverter.ToInt32(payload, 0);
                    _steamIdToPlayerId[LocalSteamId] = playerId;
                    _playerIdToSteamId[playerId] = LocalSteamId;
                    GD.Print($"Assigned player id: {playerId}");
                    JoinedServer?.Invoke();
                }

                break;
            case PacketType.PlayerJoined:
                if (!IsServer && payload.Length >= 12)
                {
                    int playerId = BitConverter.ToInt32(payload, 0);
                    ulong steamId = BitConverter.ToUInt64(payload, 4);
                    CSteamID playerSteamId = new CSteamID(steamId);

                    _steamIdToPlayerId[playerSteamId] = playerId;
                    _playerIdToSteamId[playerId] = playerSteamId;
                    
                    GD.Print($"player {playerId} joined {playerSteamId}");
                    PlayerConnected?.Invoke(playerId, playerSteamId);
                }

                break;
            
            case PacketType.PlayerLeft:
                int leftPlayerId = BitConverter.ToInt32(payload, 0);
                HandlePlayerDisconnect(leftPlayerId);
                break;
            
            case PacketType.GameData:
                HandleGameData(senderId, payload);
                break;
            
        }
    }

    private void HandleGameData(CSteamID senderId, byte[] data)
    {
        ///TODO 
    }

    private void SendPacketToSteamId(CSteamID targetSteamId, PacketType packetType, byte[] data,
        EP2PSend sendType = EP2PSend.k_EP2PSendReliable)
    {
        byte[] packet = new byte[data.Length + 1];
        packet[0] = (byte) packetType;
        Array.Copy(data, 0, packet, 1, data.Length);

        if (!SteamNetworking.SendP2PPacket(targetSteamId, packet, (uint) packet.Length, sendType))
        {
            GD.PrintErr($"failed to send packet to {targetSteamId}");
        }
    }

    private void SendPlayerIdToClient(CSteamID clientSteamId, int playerId)
    {
        byte[] data = BitConverter.GetBytes(playerId);
        SendPacketToSteamId(clientSteamId, PacketType.AssignPlayerId, data);
    }

    private void BroadcastPlayerJoined(int playerId, CSteamID steamId)
    {
        byte[] data = new byte[12];
        BitConverter.GetBytes(playerId).CopyTo(data, 0);
        BitConverter.GetBytes(steamId.m_SteamID).CopyTo(data, 4);

        foreach (var kvp in _steamIdToPlayerId)
        {
            if (kvp.Key != LocalSteamId && kvp.Key != steamId)
            {
                SendPacketToSteamId(kvp.Key, PacketType.PlayerJoined, data);
            }
        }
    }

    public void BroadcastGameData(byte[] data, EP2PSend sendType = EP2PSend.k_EP2PSendUnreliable)
    {
        if (IsServer)
        {
            foreach (var steamId in _steamIdToPlayerId.Keys)
            {
                if (steamId != LocalSteamId)
                {
                    SendPacketToSteamId(steamId, PacketType.GameData, data, sendType);
                }
            }
        }
        else
        {
            SendPacketToSteamId(HostSteamId, PacketType.GameData, data, sendType);
        }
    }

    public void SendGameDataToPlayer(int playerId, byte[] data, EP2PSend sendType = EP2PSend.k_EP2PSendReliable)
    {
        if (_playerIdToSteamId.TryGetValue(playerId, out CSteamID steamId))
        {
            SendPacketToSteamId(steamId, PacketType.GameData, data, sendType);
        }
    }

    private void HandlePlayerDisconnect(int playerId)
    {
        if (_playerIdToSteamId.TryGetValue(playerId, out CSteamID steamId))
        {
            _steamIdToPlayerId.Remove(steamId);
            _playerIdToSteamId.Remove(playerId);

            SteamNetworking.CloseP2PSessionWithUser(steamId);
            
            GD.Print($"player {playerId} disconnected");
            PlayerDisconnected?.Invoke(playerId);
        }
    }

    public void DisconnectPlayer(int playerId)
    {
        if (!IsServer) return;

        if (!_playerIdToSteamId.TryGetValue(playerId, out CSteamID steamId)) return;
        
        byte[] data = BitConverter.GetBytes(playerId);
        SendPacketToSteamId(steamId, PacketType.PlayerLeft, data);
            
        HandlePlayerDisconnect(playerId);
    }

    private void CloseAllConnections()
    {
        foreach (CSteamID steamId in _steamIdToPlayerId.Keys)
        {
            SteamNetworking.CloseP2PSessionWithUser(steamId);
        }

        _steamIdToPlayerId.Clear();
        _playerIdToSteamId.Clear();
    }

    public int GetPlayerId(CSteamID steamId)
    {
        return _steamIdToPlayerId.GetValueOrDefault(steamId, -1);
    }

    public CSteamID GetSteamId(int playerId)
    {
        return _playerIdToSteamId.TryGetValue(playerId, out CSteamID id) ? id : CSteamID.Nil;
    }

    public string GetLocalSteamIdString()
    {
        return LocalSteamId.m_SteamID.ToString();
    }

    public Dictionary<int, CSteamID> GetAllPlayers()
    {
        return new Dictionary<int, CSteamID>(_playerIdToSteamId);
    }
}

public enum PacketType : byte
{
    ConnectionRequest = 0,
    AssignPlayerId = 1,
    PlayerJoined = 2,
    PlayerLeft = 3,
    GameData  = 4
}
