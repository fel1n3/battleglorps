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
    [Export] public int Agility { get; set; } = 10;
    [Export] public int Strength { get; set; } = 10;
    [Export] public int Intelligence { get; set; } = 10;

    [ExportCategory("Movement")] 
    [Export] public float RotationSpeed { get; set; } = 10.0f;
    [Export] public float StoppingDistance { get; set; } = 0.5f;

    [ExportGroup("Abilities")] [Export] public AbilityData[] Abilities { get; set; }

    public int MaxHealth => 100 + (Strength * 10);
    public float MoveSpeed => 5.0f + (Agility * 0.1f);
    

}

