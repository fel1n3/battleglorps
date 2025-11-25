using Godot;
using Steamworks;

namespace BattleGlorps.Classes;

public partial class GenericProjectile : Area3D
{
    public float Speed = 20.0f;
    public int Damage = 10;
    public float LifeTime = 5.0f;
    public long OwnerId;
    
    public override void _Ready()
    {
        TopLevel = true;
        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
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
        // if is not server, only server checks collision
        if (body.Name == OwnerId.ToString()) return;
        
        //TODO damage colission and stuff
        //body.TakeDamage(Damage);
        QueueFree();
    }
}