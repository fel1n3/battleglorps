using Godot;
using System;
using System.Collections.Generic;
using Steamworks;

public partial class NetworkGameManager : Node
{
    [Export] public PackedScene PlayerScene { get; set; }
    [Export] public Node3D SpawnParent { get; set; }

    private SteamNetworkManager _networkManager;
    private Dictionary<int, NetworkedPlayer> _playerList = new Dictionary<int, NetworkedPlayer>();
    private NetworkedPlayer _localPlayer;

    public override void _Ready()
    {
        _networkManager = SteamNetworkManager.Instance;

        if (_networkManager != null)
        {
            _networkManager.PlayerConnected += OnPlayerConnected;
            _networkManager.PlayerDisconnected += OnPlayerDisconnected;
            _networkManager.ServerCreated += OnServerCreated;
            _networkManager.JoinedServer += OnJoinedServer;
        }

        if (SpawnParent == null)
        {
            SpawnParent = GetNode<Node3D>("/root/Main");
        }
    }

    public override void _ExitTree()
    {
        if (_networkManager != null)
        {
            _networkManager.PlayerConnected -= OnPlayerConnected;
            _networkManager.PlayerDisconnected -= OnPlayerDisconnected;
            _networkManager.ServerCreated -= OnServerCreated;
            _networkManager.JoinedServer -= OnJoinedServer;
        }
    }

    private void OnServerCreated()
    {
        GD.Print("server created");
        SpawnLocalPlayer(0);
    }

    private void OnJoinedServer()
    {
        GD.Print("joined server");
    }

    private void OnPlayerConnected(int playerId, CSteamID steamId)
    {
        GD.Print($"player {playerId} connected {steamId}");

        if (steamId == _networkManager.LocalSteamId)
        {
            if (_localPlayer == null)
            {
                SpawnLocalPlayer(playerId);
            }
        }
        else
        {
            SpawnRemotePlayer(playerId, steamId);

            if (_networkManager.IsServer)
            {
                SendAllPlayerSpawns(steamId);
            }
        }
    }

    private void OnPlayerDisconnected(int playerId)
    {
        GD.Print($"player {playerId} disconnected");

        if (_playerList.TryGetValue(playerId, out NetworkedPlayer player))
        {
            player.QueueFree();
            _playerList.Remove(playerId);
        }
    }

    private void SpawnLocalPlayer(int playerId)
    {
        if (PlayerScene == null)
        {
            GD.PrintErr("player scene not assigned");
            return;
        }

        NetworkedPlayer playerInstance = PlayerScene.Instantiate<NetworkedPlayer>();
        playerInstance.IsLocalPlayer = true;
        playerInstance.NetworkId = playerId;
        playerInstance.OwnerSteamId = _networkManager.LocalSteamId;
        playerInstance.Position = GetSpawnPosition(playerId);

        SpawnParent.AddChild(playerInstance);

        _localPlayer = playerInstance;
        _playerList[playerId] = playerInstance;
        
        GD.Print($"local player spawned at player id {playerId}");
        UpdateCameraTarget(playerInstance);
        UpdateInputManager(playerInstance);
    }

    private void SpawnRemotePlayer(int playerId, CSteamID steamId)
    {
        if (PlayerScene == null) return;

        var playerInstance = PlayerScene.Instantiate<NetworkedPlayer>();
        playerInstance.IsLocalPlayer = false;
        playerInstance.NetworkId = playerId;
        playerInstance.OwnerSteamId = steamId;
        playerInstance.Position = GetSpawnPosition(playerId);

        if (playerInstance.ClassData != null)
        {
            playerInstance.ClassData.ClassColor = GetPlayerColor(playerId);
        }


        SpawnParent.AddChild(playerInstance);
        _playerList[playerId] = playerInstance;
        
        GD.Print($"rmeote player spawned for {playerId}");
    }

    private void SendAllPlayerSpawns(CSteamID newPlayerSteamId)
    {
        foreach (var kvp in _playerList)
        {
            int playerId = kvp.Key;
            NetworkedPlayer player = kvp.Value;

            if (player.OwnerSteamId == newPlayerSteamId)
                continue;
            
            SendPlayerSpawn(newPlayerSteamId,playerId, player.OwnerSteamId, player.GlobalPosition);
        }
    }

    private void SendPlayerSpawn(CSteamID targetSteamId, int playerId, CSteamID ownerSteamId, Vector3 position)
    {
        byte[] data = new byte[1 + 4 + 8 + 12];
        int offset = 0;

        data[offset++] = (byte) NetworkMessageType.SpawnClass;
        BitConverter.GetBytes(playerId).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(ownerSteamId.m_SteamID).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(position.X).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(position.Y).CopyTo(data, offset); offset += 4;
        BitConverter.GetBytes(position.Z).CopyTo(data, offset);

        _networkManager.SendGameDataToPlayer(_networkManager.GetPlayerId(targetSteamId), data);
    }

    public void HandleGameData(CSteamID senderId, byte[] data)
    {
        if (data.Length < 5) return;

        NetworkMessageType messageType = (NetworkMessageType) data[0];
        int networkId = BitConverter.ToInt32(data, 1);

        byte[] payload = new byte[data.Length - 5];
        Array.Copy(data, 5, payload, 0, payload.Length);

        if (!_playerList.TryGetValue(networkId, out NetworkedPlayer player))
        {
            GD.PrintErr($"player not found for network id {networkId}");
            return;
        }

        switch (messageType)
        {
            case NetworkMessageType.PositionUpdate:
                player.ReceivePositionUpdate(payload);
                break;
            case NetworkMessageType.MovementCommand:
                player.ReceiveMovementCommand(payload);
                break;
            case NetworkMessageType.SpawnClass:
                //handle spawn mesage
                break;
        }
    }

    private Vector3 GetSpawnPosition(int playerId)
    {
        float angle = playerId * (Mathf.Pi * 2 / 4);
        float radius = 10f;
        return new Vector3(
            Mathf.Cos(angle) * radius,
            1f,
            Mathf.Sin(angle) * radius);
    }

    private Color GetPlayerColor(int playerId)
    {
        Color[] colors = new Color[]
        {
            Colors.Red,
            Colors.Blue,
            Colors.Green,
            Colors.Yellow,
            Colors.Purple,
            Colors.Orange
       } ;
            
            return colors[playerId & colors.Length];
    }

    private void UpdateCameraTarget(NetworkedPlayer player)
    {
        var cameraController = GetNodeOrNull<CameraController>("/root/Main/CameraController");
        if (cameraController != null)
        {
            cameraController.GlobalPosition = player.GlobalPosition;
        }
    }

    private void UpdateInputManager(NetworkedPlayer player)
    {
        var inputManager = GetNodeOrNull<NetworkedInputManager>("/root/Main/InputManager");
        if (inputManager != null)
        {
            inputManager.SetLocalPlayer(player);
        }
    }

    public NetworkedPlayer GetLocalPlayer()
    {
        return _localPlayer;
    }

    public NetworkedPlayer GetPlayer(int playerId)
    {
        return _playerList.GetValueOrDefault(playerId);
    }
}
