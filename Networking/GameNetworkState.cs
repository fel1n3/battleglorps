using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleGlorps.Core.Autoloads;
using Godot;
using Steamworks;

namespace BattleGlorps;

public partial class GameNetworkState : Node
{
    [Export] private PackedScene _playerScene;
    [Export] private Node3D _spawnParent;

    private byte _localNetId = 0;
    private byte _nextNetId = 1;
    private ClassData[] _loadedClasses;

    private Dictionary<byte, NetworkedPlayer> _playerList = new();
    private Dictionary<CSteamID, byte> _steamToNetId = new();
    private Dictionary<byte, int> _playerClassSelections = new(); //netid -> classindex
    private Dictionary<byte, PlayerSession> _sessions = new();
    private Dictionary<byte, NetworkedPlayer> _activeNodes = new();

    public override void _Ready()
    {
        _loadedClasses = LoadClasses("res://Resources/Classes");
    }

    public void StartGameAsHost()
    {
        _localNetId = 0;
        _playerClassSelections[0] = 0;
        //SpawnPlayer(0, SteamUser.GetSteamID(), new Vector3(0, 2, 0), true);
    }

    public void OnPeerConnected(CSteamID peerId)
    {
        if(!SteamManager.Instance.Connection.IsHost) return;

        byte newId = _nextNetId++;
        GD.Print($"[Host] peer connected. assigning id {newId}");
        if (!_sessions.ContainsKey(newId))
        {
            _sessions.Add(newId, new PlayerSession
            {
                NetworkId = newId,
                SteamId = peerId,
                Name = SteamFriends.GetFriendPersonaName(peerId),
                SelectedClassIndex = 0
            });
        }

        SendHandshake(peerId, newId);

        foreach (var session in _sessions.Values)
        {
            if (session.NetworkId == newId) continue;
            SendSessionDataTo(peerId, session);
        }

        BroadcastSessionData(_sessions[newId]);
    }

    public void OnPeerDisconnected(CSteamID peerId)
    {
        if (!_steamToNetId.TryGetValue(peerId, out byte netId)) return;
        if (!_playerList.TryGetValue(netId, out NetworkedPlayer value)) return;
        value.QueueFree();
        _playerList.Remove(netId);
        _steamToNetId.Remove(peerId);
        _playerClassSelections.Remove(netId);
    }

    public void ProcessPacket(CSteamID sender, byte[] data)
    {
        using MemoryStream ms = new(data);
        using BinaryReader reader = new(ms);
        PacketType type = (PacketType) reader.ReadByte();

        switch (type)
        {
            case PacketType.Handshake:
                _localNetId = reader.ReadByte();
                GD.Print($"[Client] got handshake. my id: {_localNetId}");

                if (!_sessions.ContainsKey(_localNetId))
                {
                    _sessions.Add(_localNetId, new PlayerSession
                    {
                        NetworkId = _localNetId,
                        SteamId = SteamUser.GetSteamID(),
                        Name = SteamFriends.GetPersonaName()
                    });
                }
                break;
            case PacketType.SessionSync:
                byte netId = reader.ReadByte();
                ulong steamId = reader.ReadUInt64();
                int classIdx = reader.ReadInt32();

                if (!_sessions.ContainsKey(netId))
                {
                    _sessions.Add(netId, new PlayerSession
                    {
                        NetworkId = netId,
                        SteamId = (CSteamID)steamId,
                        SelectedClassIndex = classIdx
                    });
                }
                else
                {
                    _sessions[netId].SelectedClassIndex = classIdx;
                }
                break;
            case PacketType.GameStart:
                string mapPath = reader.ReadString();
                LoadGameScene(mapPath);
                break;
            case PacketType.PlayerUpdate:
                byte updateId = reader.ReadByte();
                Vector3 newPos = reader.ReadVector3();
                Vector3 newRot = reader.ReadVector3();

                if (_playerList.TryGetValue(updateId, out var p))
                {
                    if(!p.IsLocalPlayer) p.UpdateRemoteState(newPos, newRot);
                }

                if (SteamManager.Instance.Connection.IsHost)
                    SteamManager.Instance.Connection.SendToAll(data, Constants.k_nSteamNetworkingSend_Unreliable);
                break;
            case PacketType.ClassSelected:
                byte pId = reader.ReadByte();
                int cIdx = reader.ReadInt32();

                _playerClassSelections[pId] = cIdx;
                GD.Print($"player {pId} selected class index {cIdx}");

                if (SteamManager.Instance.Connection.IsHost)
                    SteamManager.Instance.Connection.SendToAll(data);
                break;
            case PacketType.AbilityCast:
                byte casterId = reader.ReadByte();
                string abilityName = reader.ReadString();

                if (_playerList.TryGetValue(casterId, out var caster))
                {
                    caster.TriggerAbilityVisuals(abilityName);
                }

                if (SteamManager.Instance.Connection.IsHost) 
                    SteamManager.Instance.Connection.SendToAll(data);
                break;
        }
        
    }
    public void SelectClass(int classIndex)
    {
        if(_sessions.TryGetValue(_localNetId, out PlayerSession session))
        {
            session.SelectedClassIndex = classIndex;
        }

        BroadcastSessionData(_sessions[_localNetId]);
    }

    public void RequestStartGame()
    {
        if (!SteamManager.Instance.Connection.IsHost) return;

        string map = "res://main.tscn";

        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.GameStart);
        writer.Write(map);
        SteamManager.Instance.Connection.SendToAll(ms.ToArray());

        LoadGameScene(map);
    }

    private void LoadGameScene(string path)
    {
        GetTree().ChangeSceneToFile(path);
        
    }

    public void SpawnAllPlayers(Node3D rootSpawnNode)
    {
        GD.Print($"spawning {_sessions.Count} players.....");

        foreach (var session in _sessions.Values)
        {
            if (_activeNodes.ContainsKey(session.NetworkId)) continue;

            var p = _playerScene.Instantiate<NetworkedPlayer>();
            p.Name = $"Player_{session.NetworkId}";
            p.NetworkId = session.NetworkId;
            p.SteamId = session.SteamId;
            p.IsLocalPlayer = (session.NetworkId == _localNetId);

            p.Position = new Vector3(session.NetworkId * 2, 2, 0);

            rootSpawnNode.AddChild(p);
            _activeNodes.Add(session.NetworkId, p);

            if (session.SelectedClassIndex < _loadedClasses.Length)
            {
                p.InitializeClass(_loadedClasses[session.SelectedClassIndex]);
            }
        }
    }

    private void BroadcastSessionData(PlayerSession s)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.SessionSync);
        writer.Write(s.NetworkId);
        writer.Write(s.SteamId.m_SteamID);
        writer.Write(s.SelectedClassIndex);

        SteamManager.Instance.Connection.SendToAll(ms.ToArray());
    }

    private void SendSessionDataTo(CSteamID target, PlayerSession s)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.SessionSync);
        writer.Write(s.NetworkId);
        writer.Write(s.SteamId.m_SteamID);
        writer.Write(s.SelectedClassIndex);

        SteamManager.Instance.Connection.SendToPeer(target, ms.ToArray());
    }

    private void SpawnPlayer(byte netId, CSteamID steamId, Vector3 pos, bool isLocal, int classIdx = 0)
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
        _steamToNetId[steamId] = netId;

        if (classIdx >= 0 && classIdx < _loadedClasses.Length)
        {
            p.InitializeClass(_loadedClasses[classIdx]);
        }
    }

    private void SendHandshake(CSteamID target, byte netId)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.Handshake);
        writer.Write(netId);
        SteamManager.Instance.Connection.SendToPeer(target, ms.ToArray());
    }
    
    private ClassData[] LoadClasses(string path)
    {
        var list = new List<ClassData>();
        using var dir = DirAccess.Open(path);
        if (dir != null)
        {
            dir.ListDirBegin();
            string file = dir.GetNext();
            while (file != "")
            {
                if (!dir.CurrentIsDir() && file.EndsWith(".tres") || file.EndsWith(".tres.remap"))
                {
                    var res = ResourceLoader.Load<ClassData>(path + "/" + file.TrimSuffix(".remap"));
                    if (res != null) list.Add(res);
                }

                file = dir.GetNext();
            }
        }

        return list.OrderBy(c => c.ClassName).ToArray();
    }

    public ClassData[] GetAllClasses()
    {
        return _loadedClasses ?? System.Array.Empty<ClassData>();
    }
}