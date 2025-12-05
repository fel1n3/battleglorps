using System.Collections;
using BattleGlorps.Core.Autoloads;
using Godot;
using Steamworks;

namespace BattleGlorps.Classes;

public partial class GenericProjectile : Area3D
{
    public float Speed = 20.0f;
    public int Damage = 10;
    public float LifeTime = 5.0f;
    public byte OwnerId;
    
    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!SteamManager.Instance.Connection.IsHost) return;
        
        float dt = (float) delta;

        Vector3 moveStep = -GlobalTransform.Basis.Z * Speed * dt;
        GlobalPosition += moveStep;
        
        //if server
        LifeTime -= dt;
        if (LifeTime <= 0)
        {
            QueueFree();
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!SteamManager.Instance.Connection.IsHost) return;

        if (body is NetworkedPlayer player)
        {
            if (player.NetworkId == OwnerId) return;
            GD.Print($"proj hit player {player.NetworkId} for {Damage} dmg");

            player.Server_TakeDamage(Damage, OwnerId);
            QueueFree();
        }
        else
        {  
            GD.Print($"proj hit environment {body.Name}");
            QueueFree();
        }
    }
}