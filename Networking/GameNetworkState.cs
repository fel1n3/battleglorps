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

    public override void _Ready()
    {
        _loadedClasses = LoadClasses("res://Resources/Classes");
    }

    public void StartGameAsHost()
    {
        _localNetId = 0;
        _playerClassSelections[0] = 0;
        SpawnPlayer(0, SteamUser.GetSteamID(), new Vector3(0, 2, 0), true);
    }

    public void OnPeerConnected(CSteamID peerId)
    {
        if(!SteamManager.Instance.Connection.IsHost) return;

        byte newId = _nextNetId++;
        GD.Print($"[Host] peer connected. assigning id {newId}");

        SendHandshake(peerId, newId);

        foreach (var kvp in _playerList)
        {
            SendSpawnTo(peerId, kvp.Key, kvp.Value.SteamId, kvp.Value.Position, _playerClassSelections[kvp.Key]);
        }
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
                break;
            case PacketType.SpawnPlayer:
                byte netId = reader.ReadByte();
                ulong steamId = reader.ReadUInt64();
                Vector3 pos = reader.ReadVector3();
                int classIdx = reader.ReadInt32();

                if (netId == _localNetId)
                {
                    //its me
                    SpawnPlayer(netId, (CSteamID) steamId, pos, true, classIdx);
                }
                else if (!_playerList.ContainsKey(netId))
                {
                    SpawnPlayer(netId, (CSteamID) steamId, pos, false, classIdx);
                }

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
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);

        writer.Write((byte) PacketType.ClassSelected);
        writer.Write(_localNetId);
        writer.Write(classIndex);

        if (SteamManager.Instance.Connection.IsHost)
        {
            _playerClassSelections[_localNetId] = classIndex;
            SteamManager.Instance.Connection.SendToAll(ms.ToArray());
        }
        else
        {
            SteamManager.Instance.Connection.SendToAll(ms.ToArray());
        }
    }

    public void RequestSpawn()
    {
        if (SteamManager.Instance.Connection.IsHost)
        {
            BroadcastSpawn(_localNetId, SteamUser.GetSteamID(), Vector3.Zero, _playerClassSelections[_localNetId]);
        }
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

    private void BroadcastSpawn(byte netId, CSteamID steamId, Vector3 pos, int classIdx)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.SpawnPlayer);
        writer.Write(netId);
        writer.Write(steamId.m_SteamID);
        writer.Write(pos);
        writer.Write(classIdx);
        SteamManager.Instance.Connection.SendToAll(ms.ToArray());
    }

    private void SendSpawnTo(CSteamID target, byte netid, CSteamID steamId, Vector3 pos, int classIdx)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.SpawnPlayer);
        writer.Write(netid);
        writer.Write(steamId.m_SteamID);
        writer.Write(pos);
        writer.Write(classIdx);
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