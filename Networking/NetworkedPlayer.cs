using Godot;
using BattleGlorps.Classes;
using BattleGlorps.Core.Autoloads;
using BattleGlorps.Entities.Player;
using Steamworks;

public partial class NetworkedPlayer : PlayerClass
{
    public byte NetworkId;
    public CSteamID SteamId;
    public bool IsLocalPlayer;

    private Vector3 _targetServerPos;
    private float _lerpSpeed = 15.0f;

    [Export] private Node3D _modelParent;
    [Export] private PlayerStats _stats;
    [Export] private AbilityManager _abilityManager;
    
    public override void _Ready()
    {
        base._Ready();

        if (IsLocalPlayer)
        {
            var camController = new CameraController();
            GetTree().Root.AddChild(camController);
            camController.SetTarget(this);

            var input = new NetworkedInputManager();
            AddChild(input);
            input.Initialize(this, GetViewport().GetCamera3D());
        }
        
        SetPhysicsProcess(true);
    }

    public void InitializeClass(ClassData data)
    {
        _stats.Initialize(data);
        Speed = _stats.MovementSpeed;

        foreach (Node child in _modelParent.GetChildren())
        {
            child.QueueFree();
        }

        if (data.ModelScene != null)
        {
            Node3D modelInstance = data.ModelScene.Instantiate<Node3D>();
            _modelParent.AddChild(modelInstance);
        }

        _abilityManager.InitializeAbilities(data);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (SteamManager.Instance.Connection.IsHost)
        {
            ProcessMovement(delta);
            
        }
        else
        {
            GlobalPosition = GlobalPosition.Lerp(_targetServerPos, _lerpSpeed * (float) delta);
        }
    }

    public void Server_SetMoveTarget(Vector3 target)
    {
        if (!SteamManager.Instance.Connection.IsHost) return;
        SetMoveTarget(target);
    }

    public void UpdateRemoteState(Vector3 pos, Vector3 rot)
    {
        _targetServerPos = pos;

        Rotation = new Vector3(0, rot.Y, 0);
        
    }
    
}