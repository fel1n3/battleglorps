using Godot;
using System;

public partial class Lobby : Control
{
    private LineEdit _steamIdInput;
    private Button _hostButton;
    private Button _joinButton;
    private Label statusLabel;

    private SteamNetworkManager _networkManager;

    public override void _Ready()
    {
        _networkManager = SteamNetworkManager.Instance;

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
        AddChild(vbox);

        statusLabel = new Label();
        statusLabel.Text = "glorping...";
        vbox.AddChild(statusLabel);

        _hostButton = new Button();
        _hostButton.Text = "host";
        _hostButton.Pressed += OnHostPressed;
        vbox.AddChild(_hostButton);

        _steamIdInput = new LineEdit();
        _steamIdInput.PlaceholderText = "enter host steam id :3";
        vbox.AddChild(_steamIdInput);

        _joinButton = new Button();
        _joinButton.Text = "join";
        _joinButton.Pressed += OnJoinPressed;
        vbox.AddChild(_joinButton);

        if (_networkManager != null)
        {
            var idLabel = new Label();
            idLabel.Text = $"your steam id: {_networkManager.GetLocalSteamIdString()}";
            vbox.AddChild(idLabel);

            _networkManager.ServerCreated += OnGameStarted;
            _networkManager.JoinedServer += OnGameStarted;
            _networkManager.ConnectionFailed += OnConnectionFailed;
        }
    }

    private void OnHostPressed()
    {
        statusLabel.Text = "creating server....";
        _networkManager.CreateServer();
    }

    private void OnJoinPressed()
    {
        string steamId = _steamIdInput.Text.Trim();
        if (string.IsNullOrEmpty(steamId))
        {
            statusLabel.Text = "please enter a steam id";
            return;
        }

        statusLabel.Text = "joining server";
        _networkManager.JoinServerByString(steamId);
    }

    private void OnGameStarted()
    {
        statusLabel.Text = "game started";
        Visible = false;
    }

    private void OnConnectionFailed(string reason)
    {
        statusLabel.Text = $"connx failed {reason}";
    }
}
