using BattleGlorps.Core.Autoloads;
using Godot;

namespace BattleGlorps;

public partial class GameLevel : Node3D
{
    [Export] private Node3D _spawnContainer;

    public override void _Ready()
    {
        SteamManager.Instance.GameState.SpawnAllPlayers(_spawnContainer);
    }
}