using BattleGlorps.Entities.Player;
using Godot;

namespace BattleGlorps.Classes;

public enum StatType
{
    None,
    Strength,
    Intelligence,
    Agility
}

[GlobalClass]
public partial class SpawnProjectileEffect : AbilityEffect
{
    [Export] public PackedScene ProjectileScene;
    
    [ExportGroup("Damage")]
    [Export] public int BaseDamage = 10;
    [Export(PropertyHint.Enum)] public StatType ScalingStat = StatType.Intelligence;
    [Export] public float ScalingFactor = 2.5f;
    [Export] public float Speed = 20.0f;
    
    [Export] public float OffsetForward = 1.5f;

    public override void ApplyEffect(Node3D caster)
    {
        //if NOT caster  is server return;
        if (ProjectileScene == null) return;

        int finalDamage = BaseDamage;

        var stats = caster.GetNodeOrNull<PlayerStats>("PlayerStats");
        if (stats != null)
        {
            switch (ScalingStat)
            {
                case StatType.Strength:
                    finalDamage += Mathf.RoundToInt(stats.Strength * ScalingFactor);
                    break;
                case StatType.Intelligence:
                    finalDamage += Mathf.RoundToInt(stats.Intelligence * ScalingFactor);
                    break;
                case StatType.Agility:
                    finalDamage += Mathf.RoundToInt(stats.Agility * ScalingFactor);
                    break;
            }
        }

        var proj = ProjectileScene.Instantiate<GenericProjectile>();

        proj.Position = caster.GlobalPosition + (-caster.GlobalTransform.Basis.Z * OffsetForward);
        proj.Rotation = caster.GlobalRotation;

        proj.Damage = finalDamage;
        proj.OwnerId = caster.Multiplayer.GetUniqueId();

       caster.GetTree().Root.AddChild(proj);
    }
}