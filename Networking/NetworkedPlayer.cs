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

    private Node3D _modelParent;
    private PlayerStats _stats;
    private AbilityManager _abilityManager;

    public void Initialize(byte netId, CSteamID steamId, bool isLocal, ClassData classData)
    {
        NetworkId = netId;
        SteamId = steamId;
        IsLocalPlayer = isLocal;
        Name = $"Player_{netId}";

        _modelParent = GetNodeOrNull<Node3D>("ModelParent");
        if (_modelParent == null)
        {
            _modelParent = new Node3D();
            _modelParent.Name = "ModelParent";
            AddChild(_modelParent);
        }

        _stats = GetNodeOrNull<PlayerStats>("PlayerStats");
        if (_stats == null)
        {
            _stats = new PlayerStats();
            _stats.Name = "PlayerStats";
            AddChild(_stats);
        }

        _abilityManager = GetNodeOrNull<AbilityManager>("AbilityManager");
        if (_abilityManager == null)
        {
            _abilityManager = new AbilityManager();
            _abilityManager.Name = "AbilityManager";
            AddChild(_abilityManager);
        }
            
        bool isHost = SteamManager.Instance.Connection.IsHost;
        EnableNavigation(isHost);
        SetPhysicsProcess(true);

        ApplyClassData(classData);
        if (IsLocalPlayer)
        {
            GD.Print($"[Player {NetworkId}] Initializing local controls...");

            CameraController camController = new CameraController();
            GetTree().Root.AddChild(camController);

            NetworkedInputManager input = new();
            AddChild(input);
            input.Initialize(this, camController.GetCamera());
        }
    }

    private void ApplyClassData(ClassData data)
    {
        if (data == null) return;

        _stats.Initialize(data);
        Speed = _stats.MovementSpeed;

        foreach (Node child in _modelParent.GetChildren()) child.QueueFree();
        if (data.ModelScene != null)
        {
            var model = data.ModelScene.Instantiate<Node3D>();
            _modelParent.AddChild(model);
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