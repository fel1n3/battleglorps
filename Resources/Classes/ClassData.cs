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
    [Export] public PackedScene ModelScene { get; set; }

    [ExportCategory("Base Stats")]
    [Export] public int BaseAgility { get; set; } = 10;
    [Export] public int BaseStrength { get; set; } = 10;
    [Export] public int BaseIntelligence { get; set; } = 10;

    [ExportCategory("Movement")] 
    [Export] public float RotationSpeed { get; set; } = 10.0f;
    [Export] public float StoppingDistance { get; set; } = 0.5f;

    [ExportGroup("Abilities")] [Export] public AbilityData[] Abilities { get; set; }

    public int MaxHealth => 100 + (BaseStrength * 10);
    public float MoveSpeed => 5.0f + (BaseAgility * 0.1f);
    

}

