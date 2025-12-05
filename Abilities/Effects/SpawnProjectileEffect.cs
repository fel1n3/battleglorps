using BattleGlorps.Core.Autoloads;
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

    public override void ApplyEffect(Node3D caster, bool isAuthoritative)
    {
        if (!isAuthoritative) return;
        if (!SteamManager.Instance.Connection.IsHost) return;
        
        if (ProjectileScene == null) return;

        int finalDamage = BaseDamage;
        
        
        if (caster is not NetworkedPlayer player)
        {
            player = caster.GetParent()?.GetParent() as NetworkedPlayer;
        }

        if (player == null) return;

        PlayerStats stats = caster.GetNodeOrNull<PlayerStats>("PlayerStats");
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

        Vector3 spawnOffset = -player.GlobalTransform.Basis.Z * OffsetForward;
        Vector3 spawnPosition = player.GlobalPosition + spawnOffset;
        Vector3 spawnRotation = player.GlobalRotation;

        proj.TopLevel = true;

        proj.Position = spawnPosition;
        proj.Rotation = spawnRotation;
        proj.Damage = finalDamage;
        proj.Speed = Speed;
        proj.OwnerId = player.NetworkId;

        caster.GetTree().Root.AddChild(proj);
    }
}