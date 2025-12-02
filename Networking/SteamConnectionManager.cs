using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Godot;
using Steamworks;

namespace BattleGlorps;

public partial class SteamConnectionManager : Node
{
    public event Action<CSteamID, byte[]> OnPacketReceived;
    public event Action<CSteamID> OnPeerConnected;
    public event Action<CSteamID> OnPeerDisconnected;


    public bool IsHost => _listenSocket != HSteamListenSocket.Invalid;

    private HSteamListenSocket _listenSocket = HSteamListenSocket.Invalid;
    private List<HSteamNetConnection> _connections = [];
    private Callback<SteamNetConnectionStatusChangedCallback_t> _statusCallback;
    private const int KMaxMessagesPerPoll = 32;

    private Dictionary<HSteamNetConnection, CSteamID> _connToId = new();

    public override void _Ready()
    {
        _statusCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);
    }

    public override void _Process(double delta)
    {
        foreach (HSteamNetConnection conn in _connections) ReceiveMessages(conn);
    }

    public void CreateP2PHost()
    {
        if (IsHost) return;
        _listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
    }

    public void ConnectToPeer(CSteamID steamId)
    {
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(steamId);
        var conn = SteamNetworkingSockets.ConnectP2P(ref identity, 0, 0, null);
        _connections.Add(conn);
    }

    public void SendToAll(byte[] data, int flags = Constants.k_nSteamNetworkingSend_Reliable)
    {
        foreach (HSteamNetConnection conn in _connections) SendPacket(conn, data, flags);
    }

    public void SendToPeer(CSteamID targetId, byte[] data, int flags = Constants.k_nSteamNetworkingSend_Reliable)
    {
        foreach (var kvp in _connToId.Where(kvp => kvp.Value == targetId))
        {
            SendPacket(kvp.Key, data, flags);
            return;
        }
    }

    public void SendPacket(HSteamNetConnection conn, byte[] data, int flags = Constants.k_nSteamNetworkingSend_Reliable)
    {
        GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            SteamNetworkingSockets.SendMessageToConnection(conn, pinnedArray.AddrOfPinnedObject(), (uint) data.Length,
                flags, out long _);

        }
        finally
        {
            pinnedArray.Free();
        }
    }

    private void ReceiveMessages(HSteamNetConnection conn)
    {
        IntPtr[] msgs = new IntPtr[32];
        int count = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, msgs, 32);

        for (int i = 0; i < count; i++)
        {
            var msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msgs[i]);

            byte[] data = new byte[msg.m_cbSize];
            Marshal.Copy(msg.m_pData, data, 0, msg.m_cbSize);
            
            OnPacketReceived?.Invoke(msg.m_identityPeer.GetSteamID(), data);
            SteamNetworkingMessage_t.Release(msgs[i]);
        }
    }

    private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t param)
    {
        var info = param.m_info;
        var remoteId = info.m_identityRemote.GetSteamID();
        
        if (info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
        {
            if (IsHost) SteamNetworkingSockets.AcceptConnection(param.m_hConn);
            
        }
        else if (info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
        {
            if (_connections.Contains(param.m_hConn)) return;
            
            _connections.Add(param.m_hConn);
            _connToId[param.m_hConn] = remoteId;
            OnPeerConnected?.Invoke(remoteId);
        }
        else if (info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer ||
                  info.m_eState == ESteamNetworkingConnectionState
                      .k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
        {
            if (!_connections.Contains(param.m_hConn)) return;
            SteamNetworkingSockets.CloseConnection(param.m_hConn, 0, "Bye", false);
            _connections.Remove(param.m_hConn);
            _connToId.Remove(param.m_hConn);
            OnPeerDisconnected?.Invoke(remoteId);
        }
    }
    
    
}