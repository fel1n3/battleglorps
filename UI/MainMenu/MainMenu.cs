using Godot;
using System;
using System.Collections.Generic;
using BattleGlorps.Core.Autoloads;
using Steamworks;

public partial class MainMenu : Control
{
    [Export] private Button _hostButton;

    public override void _Ready()
    {
        
        _hostButton.Pressed += OnHostPressed;

        CallDeferred(nameof(SubscribeToEvents));

    }

    private void SubscribeToEvents()
    {
        if (SteamManager.Instance == null)
        {
            GD.PrintErr("steamnetworkmanager instance is null.");
            return;
        }

       // SteamNetworkManager.Instance.OnLobbyListUpdated += UpdateLobbyList;
       // SteamNetworkManager.Instance.OnConnectionStatusMessage += UpdateStatusText;

      //  SteamNetworkManager.Instance.OnPlayerConnected += OnPlayerConnected;
    }


    private void OnHostPressed()
    {
        SteamManager.Instance.Lobby.CreateLobby();
        
        SteamManager.Instance.GameState.StartGameAsHost();

        GetTree().ChangeSceneToFile("res://UI/Lobby/Lobby.tscn");
        //_mainMenuContainer.Visible = false;
        //_classSelect.Visible = true;

    }
/*
    private void OnCopyIdPressed()
    {
        ulong id = SteamNetworkManager.Instance.GetCurrentLobbyID();
        if (id > 0)
        {
            DisplayServer.ClipboardSet(id.ToString());
            _statusLabel.Text = "lobby id copied to clipboard";
        }
    }

    private void OnJoinByIdPressed()
    {
        string text = _lobbyIdInput.Text.Trim();
        if (ulong.TryParse(text, out ulong lobbyId))
        {
            _statusLabel.Text = $"joining lobby id: {lobbyId}...";
            SteamNetworkManager.Instance.JoinLobby(lobbyId);
            this.Visible = false;
        }
        else
        {
            _statusLabel.Text = "fart";
        }
    }

    private void OnRefreshPressed()
    {
        foreach(Node child in _lobbyListContainer.GetChildren()) child.QueueFree();

        _statusLabel.Text = "fetching lobbies...";
        SteamNetworkManager.Instance.GetLobbyList();
    }

    private void UpdateLobbyList(List<LobbyInfo> lobbies)
    {
        foreach (Node child in _lobbyListContainer.GetChildren())
        {
            child.QueueFree();
        }

        if (lobbies.Count == 0)
        {
            _statusLabel.Text = "no lobbies found";
            return;
        }

        _statusLabel.Text = $"Found {lobbies.Count} lobbies";

        foreach (var lobby in lobbies)
        {
            var lobbyButton = new Button();
            lobbyButton.Text = $"{lobby.Name} | Players: {lobby.PlayerCount}/{lobby.MaxPlayers}";
            lobbyButton.Alignment = HorizontalAlignment.Left;

            ulong targetId = lobby.LobbyId;

            lobbyButton.Pressed += () => OnJoinLobbyClicked(targetId);
            
            _lobbyListContainer.AddChild(lobbyButton);
        }
    }

    private void OnJoinLobbyClicked(ulong lobbyId)
    {
        _statusLabel.Text = $"joining lobby {lobbyId}...";
        SteamNetworkManager.Instance.JoinLobby(lobbyId);

        this.Visible = false;
    }

    private void UpdateStatusText(string message)
    {
        _statusLabel.Text = message;
    }

    private void OnPlayerConnected(CSteamID steamId)
    {
        //TODO
    }

    
    public override void _ExitTree()
    {
        if (SteamNetworkManager.Instance != null)
        {
            SteamNetworkManager.Instance.OnLobbyListUpdated -= UpdateLobbyList;
            SteamNetworkManager.Instance.OnConnectionStatusMessage -= UpdateStatusText;
            SteamNetworkManager.Instance.OnPlayerConnected -= OnPlayerConnected;
        }
    }*/
}