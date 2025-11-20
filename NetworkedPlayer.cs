using Godot;
using System;
using Steamworks;

public partial class NetworkedPlayer : PlayerClass
{
    [Export] public bool IsLocalPlayer { get; set; } = true;
    public int NetworkId { get; set; } = -1;
    public CSteamID OwnerSteamId { get; set; }

    private Vector3 _lastSentPosition;
    private Vector3 _lastSendRotation;
    private float _networkSyncTimer = 0f;
    private const float NETWORK_SYNC_RATE = 0.05f;

    private Vector3 _targetNetworkPosition;
    private Vector3 _targetNetworkRotation;
    private float _interpolationSpeed = 10f;

    private SteamNetworkManager _networkManager;

    public override void _Ready()
    {
        base._Ready();
        
        _networkManager = SteamNetworkManager.Instance;

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
            _networkSyncTimer += (float) delta;
            if (_networkSyncTimer >= NETWORK_SYNC_RATE)
            {
                _networkSyncTimer = 0f;
                SendPositionUpdate();
            }
        }
        else
        {
            GlobalPosition = GlobalPosition.Lerp(_targetNetworkPosition, _interpolationSpeed * (float) delta);
            Rotation = Rotation.Lerp(_targetNetworkRotation, _interpolationSpeed * (float) delta);
        }
    }

    public override void SetTargetPosition(Vector3 position)
    {
        if (!IsLocalPlayer) return;
        base.SetTargetPosition(position);

        SendMovementCommand(position);
    }

    private void SendPositionUpdate()
    {
        if (_networkManager == null) return;

        if ((GlobalPosition - _lastSentPosition).Length() < 0.1f &&
            (Rotation - _lastSendRotation).Length() < 0.01f) return;

        _lastSentPosition = GlobalPosition;
        _lastSendRotation = Rotation;

        var data = SerializePositionUpdate();
        _networkManager.BroadcastGameData(data, Steamworks.EP2PSend.k_EP2PSendUnreliable);
    }

    private void SendMovementCommand(Vector3 targetPos)
    {
        if (_networkManager == null) return;

        var data = SerializeMovementCommand(targetPos);
        _networkManager.BroadcastGameData(data, Steamworks.EP2PSend.k_EP2PSendUnreliable);
    }

    public void ReceivePositionUpdate(byte[] data)
    {
        if (IsLocalPlayer) return;

        int o = 0;

        _targetNetworkPosition = new Vector3(
            Read(data, ref o),
            Read(data, ref o),
            Read(data, ref o)
        );

        _targetNetworkRotation = new Vector3(
            Read(data, ref o),
            Read(data, ref o),
            Read(data, ref o)
        );
    }

    public void ReceiveMovementCommand(byte[] data)
    {
        if (IsLocalPlayer) return;

        int o = 0;

        Vector3 targetPos = new Vector3(
            Read(data, ref o),
            Read(data, ref o),
            Read(data, ref o)
        );
        
        base.SetTargetPosition(targetPos);
    }

    private byte[] SerializePositionUpdate()
    {
        byte[] data = new byte[1 + 4 + 24];
        int offset = 0;

        data[offset++] = (byte)NetworkMessageType.PositionUpdate;
        BitConverter.GetBytes(NetworkId).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(GlobalPosition.X).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(GlobalPosition.Y).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(GlobalPosition.Z).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(Rotation.X).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(Rotation.Y).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(Rotation.Z).CopyTo(data, offset); 

        return data;
    }

    public byte[] SerializeMovementCommand(Vector3 targetPos)
    {
        byte[] data = new byte[1 + 4 + 12];
        int offset = 0;

        data[offset++] = (byte) NetworkMessageType.MovementCommand;
        BitConverter.GetBytes(NetworkId).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(targetPos.X).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(targetPos.Y).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(targetPos.Z).CopyTo(data, offset);

        return data;

    }

    private static float Read(byte[] data, ref int o)
    {
        float v = BitConverter.ToSingle(data, o);
        o += 4;
        return v;
    }
}

public enum NetworkMessageType : byte
{
    PositionUpdate = 0,
    MovementCommand = 1,
    SpawnClass = 4,
    DestroyClass = 5
}


