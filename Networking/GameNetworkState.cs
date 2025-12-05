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
    public event Action OnSessionUpdated;
    
    private PackedScene _playerScene;

    private byte _localNetId = 0;
    private byte _nextNetId = 1;
    private ClassData[] _loadedClasses;

    private Dictionary<byte, NetworkedPlayer> _playerList = new();
    private Dictionary<CSteamID, byte> _steamToNetId = new();
    private Dictionary<byte, PlayerSession> _sessions = new();
    private Dictionary<byte, NetworkedPlayer> _activeNodes = new();

    public override void _Ready()
    {
        _loadedClasses = LoadClasses("res://Resources/Classes");
        _playerScene = GD.Load<PackedScene>("res://Entities/Player/player.tscn");
    }

    public void StartGameAsHost()
    {
        _localNetId = 0;
        _sessions.Add(0, new PlayerSession
        {
            NetworkId = 0,
            SteamId = SteamUser.GetSteamID(),
            Name = SteamFriends.GetPersonaName(),
            SelectedClassIndex = 0,
            IsReady = false
        });
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
                SelectedClassIndex = 0,
                IsReady = false
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
                bool rdy = reader.ReadBoolean();

                if (!_sessions.ContainsKey(netId))
                {
                    _sessions.Add(netId, new PlayerSession
                    {
                        NetworkId = netId,
                        SteamId = (CSteamID)steamId,
                        Name = SteamFriends.GetFriendPersonaName((CSteamID)steamId),
                        SelectedClassIndex = classIdx,
                        IsReady = rdy
                    });
                }
                else
                {
                    _sessions[netId].SelectedClassIndex = classIdx;
                    _sessions[netId].IsReady = rdy;
                }
                OnSessionUpdated?.Invoke();
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

                if (_sessions.TryGetValue(pId, out PlayerSession session))
                {
                    session.SelectedClassIndex = cIdx;
                    OnSessionUpdated?.Invoke();
                    
                    if (SteamManager.Instance.Connection.IsHost)
                        SteamManager.Instance.Connection.SendToAll(data);
                    
                }
                break;
            case PacketType.AbilityCast:
                byte casterId = reader.ReadByte();
                string abilityName = reader.ReadString();

                if (_playerList.TryGetValue(casterId, out var caster))
                {
                    if (!SteamManager.Instance.Connection.IsHost)
                    {
                        caster.TriggerAbilityVisuals(abilityName);
                    }
                }

                if (SteamManager.Instance.Connection.IsHost) 
                    SteamManager.Instance.Connection.SendToAll(data);
                break;
            case PacketType.MoveCommand:
                if(!SteamManager.Instance.Connection.IsHost) return;

                byte playerId = reader.ReadByte();
                Vector3 targetPos = reader.ReadVector3();
                GD.Print("move command packet received");
                if (_playerList.TryGetValue(playerId, out NetworkedPlayer playerNode))
                {
                    playerNode.Server_SetMoveTarget(targetPos);
                }

                break;
            case PacketType.ReadyStatus:
                byte rId = reader.ReadByte();
                bool isReady = reader.ReadBoolean();

                ProcessReadyStatus(rId, isReady);
                if (SteamManager.Instance.Connection.IsHost) 
                    SteamManager.Instance.Connection.SendToAll(data);
                break;
            case PacketType.DamagePlayer:
                byte targetId = reader.ReadByte();
                short damage = reader.ReadInt16();
                short newHealth = reader.ReadInt16();

                if (_playerList.TryGetValue(targetId, out var damagedPlayer))
                {
                    damagedPlayer.Client_ApplyDamageVisuals(damage, newHealth);
                }

                if (SteamManager.Instance.Connection.IsHost) SteamManager.Instance.Connection.SendToAll(data);
                break;
            case PacketType.PlayerDied:
                byte victimId = reader.ReadByte();
                byte killerId = reader.ReadByte();
                
                GD.Print($"player {victimId} killed by {killerId}");
                if (_playerList.TryGetValue(victimId, out var victim))
                {
                    if (!SteamManager.Instance.Connection.IsHost)
                    {
                        victim.Client_HandleDeath();
                        
                    }
                }

                if (SteamManager.Instance.Connection.IsHost) SteamManager.Instance.Connection.SendToAll(data);
                break;
                
                
        }
        
    }

    private void ProcessReadyStatus(byte id, bool ready)
    {
        if (_sessions.ContainsKey(id))
        {
            _sessions[id].IsReady = ready;
            OnSessionUpdated?.Invoke();
        }
    }
    public void SelectClass(int classIndex)
    {
        if(_sessions.TryGetValue(_localNetId, out PlayerSession session))
        {
            session.SelectedClassIndex = classIndex;
            OnSessionUpdated?.Invoke();
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

        foreach (PlayerSession session in _sessions.Values)
        {
            if (_activeNodes.ContainsKey(session.NetworkId)) continue;

            NetworkedPlayer p = _playerScene.Instantiate<NetworkedPlayer>();
            rootSpawnNode.AddChild(p);

            Vector3 spawnPos = new Vector3(session.NetworkId * 2, 0, 0);
            p.Position = spawnPos;

            bool isLocal = (session.NetworkId == _localNetId);
            ClassData cData = null;
            if (session.SelectedClassIndex >= 0 && session.SelectedClassIndex < _loadedClasses.Length)
            {
                cData = _loadedClasses[session.SelectedClassIndex];
            }

            p.Initialize(session.NetworkId, session.SteamId, isLocal, cData);

            _activeNodes.Add(session.NetworkId, p);
            _playerList.TryAdd(session.NetworkId, p);
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
        writer.Write(s.IsReady);

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
        writer.Write(s.IsReady);
        
        SteamManager.Instance.Connection.SendToPeer(target, ms.ToArray());
    }
    

    private void SendHandshake(CSteamID target, byte netId)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.Handshake);
        writer.Write(netId);
        SteamManager.Instance.Connection.SendToPeer(target, ms.ToArray());
    }

    public void ToggleReady()
    {
        if (!_sessions.ContainsKey(_localNetId)) return;

        bool newState = !_sessions[_localNetId].IsReady;

        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.ReadyStatus);
        writer.Write(_localNetId);
        writer.Write(newState);

        if (SteamManager.Instance.Connection.IsHost)
        {
            ProcessReadyStatus(_localNetId, newState);
        }

        SteamManager.Instance.Connection.SendToAll(ms.ToArray());
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

    public Dictionary<byte, PlayerSession> GetAllSessions() => _sessions;
}