using Godot;
using System;
using System.IO;
using BattleGlorps;
using Steamworks;

public partial class NetworkedPlayer : PlayerClass
{
    public bool IsLocalPlayer { get; set; } = true;
    public byte NetworkId { get; set; }
    public CSteamID SteamId { get; set; }

    private Vector3 _targetNetworkPosition;
    private Vector3 _targetNetworkRotation;

    private float _interpolationSpeed = 10.0f;
    private double _sendTimer = 0;
    private const float NETWORK_SYNC_RATE = 0.05f;

    public override void _Ready()
    {
        base._Ready();

        if (!IsLocalPlayer)
        {
            _targetNetworkPosition = GlobalPosition;
            _targetNetworkRotation = Rotation;
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (IsLocalPlayer)
        {
            _sendTimer += delta;
            if (_sendTimer >= NETWORK_SYNC_RATE)
            {
                _sendTimer = 0;
                SendPositionUpdate();
            }
        }
        else
        {
            GlobalPosition = GlobalPosition.Lerp(_targetNetworkPosition, _interpolationSpeed * (float) delta);
            Rotation = Rotation.Lerp(_targetNetworkRotation, _interpolationSpeed * (float) delta);
        }
    }

    private void SendPositionUpdate()
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);

        writer.Write((byte) PacketType.PlayerUpdate);
        writer.Write(NetworkId);
        writer.Write(GlobalPosition);
        writer.Write(GlobalRotation);

        byte[] data = ms.ToArray();

        SteamNetworkManager.Instance.SendBytesToAll(data, Steamworks.Constants.k_nSteamNetworkingSend_Unreliable);
    }

    public void UpdateRemoteState(Vector3 newPos, Vector3 newRot)
    {
        _targetNetworkPosition = newPos;
        _targetNetworkRotation = newRot;
    }
    
}