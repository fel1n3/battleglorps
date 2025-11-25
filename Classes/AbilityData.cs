using Godot;
using Godot.Collections;

namespace BattleGlorps.Classes;

[GlobalClass]
public abstract partial class AbilityData : Resource
{
    [Export] public string AbilityName { get; set; }
    [Export(PropertyHint.MultilineText)] public string Tooltip { get; set; }
    [Export] public Texture2D Icon { get; set; } = new PlaceholderTexture2D();

    [ExportGroup("Balancing")] [Export] public float Cooldown { get; set; }

    [Export] public Array<AbilityEffect> Effects { get; set; }

}