using Godot;
using System;
using BattleGlorps.Classes;

[GlobalClass]
public partial class ClassData : Resource
{
    [ExportGroup("Identity")]
    [Export] public string ClassName { get; set; } = "Class";
    [Export] public Texture2D Icon { get; set; } = new PlaceholderTexture2D();
    [Export(PropertyHint.MultilineText)] public string Description { get; set; }
    [Export] public Color ClassColor { get; set; } = Colors.White;

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
    [Export] public float RotationSpeed { get; set; } = 10.0f;
    [Export] public float StoppingDistance { get; set; } = 0.5f;

    [ExportGroup("Abilities")] [Export] public AbilityData[] Abilities { get; set; }


    [Export] public Vector3 ModelScale { get; set; } = Vector3.One;

}

