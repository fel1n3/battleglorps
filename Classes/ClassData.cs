using Godot;
using System;

public partial class ClassData : Resource
{
    [Export] public string ClassName { get; set; } = "Class";
    [Export] public Texture2D Portrait { get; set; }

    [ExportCategory("Base Stats")]
    [Export] public float MaxHealth { get; set; } = 100f;
    [Export] public float HealthRegen { get; set; } = 1f;
    [Export] public float MaxMana { get; set; } = 100f;
    [Export] public float ManaRegen { get; set; } = 1f;

    [ExportCategory("Combat Stats")]
    [Export] public float AttackDamage { get; set; } = 10f;
    [Export] public float AttackSpeed { get; set; } = 1.0f;
    [Export] public float AttackRange { get; set; } = 2f;
    [Export] public float Armour { get; set; } = 5f;
    [Export] public float MagicResist { get; set; } = 5f;

    [ExportCategory("Movement")] 
    [Export] public float MoveSpeed { get; set; } = 5f;

    [ExportCategory("Visual")] 
    [Export] public Color ClassColor { get; set; } = Colors.White;

    [Export] public Vector3 ModelScale { get; set; } = Vector3.One;

}

public partial class Ability : Resource
{
    [Export] public string AbilityName { get; set; }
    [Export] public string Description { get; set; }
    [Export] public Texture2D Icon { get; set; }
    [Export] public float Cooldown { get; set; }
    [Export] public float ManaCost { get; set; }
    [Export] public float CastRange { get; set; }

    public float CurrentCooldown { get; set; } = 0f;
    
   // public virtual bool CanCast(PlayerClass playerClass){
   //     return CurrentCooldown <= 0 && playerClass.
   // }
}
