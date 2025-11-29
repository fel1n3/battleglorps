using Godot;
using Steamworks;

namespace BattleGlorps.Core.Autoloads;

public partial class SteamManager : Node
{
    public static SteamManager Instance { get; private set; }
    
    public SteamLobbyManager Lobby { get; private set; }
    public SteamConnectionManager Connection { get; private set; }
    public GameNetworkState GameState { get; private set; }

    public override void _Ready()
    {
        Instance = this;

        if (!SteamAPI.Init())
        {
            GD.PrintErr("steamapi init failed is steam running");
            return;
        }

        Lobby = new SteamLobbyManager();
        Connection = new SteamConnectionManager();
        GameState = new GameNetworkState();
        
        AddChild(GameState);
        AddChild(Lobby);
        AddChild(Connection);

        Connection.OnPacketReceived += GameState.ProcessPacket;
        Connection.OnPeerConnected += GameState.OnPeerConnected;
        Connection.OnPeerDisconnected += GameState.OnPeerDisconnected;
    }

    public override void _Process(double delta)
    {
        SteamAPI.RunCallbacks();
    }

    public override void _ExitTree()
    {
        SteamAPI.Shutdown();
        
    }
}