using Godot;

namespace BattleGlorps.Classes;

[GlobalClass]
public partial class SpawnProjectileEffect : AbilityEffect
{
    [Export] public PackedScene ProjectileScene;
    [Export] public float Speed = 20.0f;
    [Export] public int Damage = 10;
    [Export] public float OffsetForward = 1.5f;

    public override void ApplyEffect(Node3D caster)
    {
        //if NOT caster  is server return;
        if (ProjectileScene == null) return;

        var proj = ProjectileScene.Instantiate<GenericProjectile>();

        proj.Position = caster.GlobalPosition + (-caster.GlobalTransform.Basis.Z * OffsetForward);
        proj.Rotation = caster.GlobalRotation;

        proj.Speed = Speed;
        proj.Damage = Damage;
       // proj.ShooterId = net id of caster;

       caster.GetTree().Root.AddChild(proj);
    }
}