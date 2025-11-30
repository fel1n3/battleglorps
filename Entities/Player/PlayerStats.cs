using Godot;

namespace BattleGlorps.Entities.Player;

public partial class PlayerStats : Node
{
    public int Strength;
    public int Intelligence;
    public int Agility;

    public int MaxHealth => 100 + (Strength * 10);
    public float MovementSpeed => 5.0f + (Agility * 0.1f);

    public void Initialize(ClassData data)
    {
        Strength = data.BaseStrength;
        Intelligence = data.BaseIntelligence;
        Agility = data.BaseAgility;
    }
}
