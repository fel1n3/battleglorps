using System.IO;
using BattleGlorps;
using Godot;
using BattleGlorps.Classes;
using BattleGlorps.Core.Autoloads;
using BattleGlorps.Entities.Player;
using BattleGlorps.UI.HUD;
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
    private GameHUD _hud;

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
        
        if (!IsLocalPlayer) return;
        
        GD.Print($"[Player {NetworkId}] Initializing local controls...");

        CameraController camController = new CameraController();
        GetTree().Root.AddChild(camController);

        NetworkedInputManager input = new();
        AddChild(input);
        input.Initialize(this, camController.GetCamera());

        SpawnHUD(classData);
    }

    private void SpawnHUD(ClassData classData)
    {
        var hudScene = GD.Load<PackedScene>("res://UI/HUD/GameHUD.tscn");
        if (hudScene == null) return;

        _hud = hudScene.Instantiate<GameHUD>();
        GetTree().Root.AddChild(_hud);

        CallDeferred(nameof(InitializeHUDDeferred), classData);
    }

    private void InitializeHUDDeferred(ClassData classData)
    {
        if (_hud != null)
        {
            _hud.Initialize(this);

            var classLabel = _hud.GetNode<Label>("HealthContainer/HealthVBox/ClassNameLabel");
            if (classLabel != null && classData != null)
            {
                classLabel.Text = classData.ClassName;
                classLabel.AddThemeColorOverride("font_color", classData.ClassColor);
            }
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

    public void Server_TakeDamage(int damage, byte attackerNetId)
    {
        if (!SteamManager.Instance.Connection.IsHost) return;
        if (_stats == null || _stats.IsDead) return;

        _stats.TakeDamage(damage);

        BroadcastDamagePacket(NetworkId, damage, _stats.CurrentHealth);

        if (_stats.IsDead)
        {
            BroadcastDeathPacket(NetworkId, attackerNetId);
            HandleDeath();
        }
    }

    private void HandleDeath()
    {
        SetPhysicsProcess(false);
        Visible = false;

        GetTree().CreateTimer(3.0).Timeout += () =>
        {
            _stats.Respawn();
            Visible = true;
            SetPhysicsProcess(true);

            Position = new Vector3(NetworkId * 2, 0, 0);
        };
    }

    private void BroadcastDamagePacket(byte targetNetId, int damage, int newHealth)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);

        writer.Write((byte) PacketType.DamagePlayer);
        writer.Write(targetNetId);
        writer.Write((short) damage);
        writer.Write((short) newHealth);

        SteamManager.Instance.Connection.SendToAll(ms.ToArray());
    }

    private void BroadcastDeathPacket(byte victimNetId, byte killerNetId)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);

        writer.Write((byte) PacketType.PlayerDied);
        writer.Write(victimNetId);
        writer.Write(killerNetId);
        
        SteamManager.Instance.Connection.SendToAll(ms.ToArray());
    }

    public void Client_ApplyDamageVisuals(int damage, int newHealth)
    {
        if (_stats == null) return;
        
        //update health for hud
        //authoritative
        GD.Print($"player {NetworkId} received damage {damage} new health {newHealth}");
        
        //TODO UI STUFF
    }

    public void Client_HandleDeath()
    {
        GD.Print($"player {NetworkId} died");

        Visible = false;
        SetPhysicsProcess(false);
    }

    public void UpdateRemoteState(Vector3 pos, Vector3 rot)
    {
        _targetServerPos = pos;

        Rotation = new Vector3(0, rot.Y, 0);
        
    }

    public void TriggerAbilityVisuals(string abilityName)
    {
        _abilityManager?.TriggerVisualsByName(abilityName);
    }
    
    public void CastAbility(int index)
    {
        _abilityManager?.CastAbilityByIndex(index);
    }

    public override void _ExitTree()
    {
        if (_hud == null) return;
        _hud.QueueFree();
        _hud = null;
    }
}