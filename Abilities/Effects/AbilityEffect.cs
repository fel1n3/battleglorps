using Godot;

namespace BattleGlorps.Classes;

[GlobalClass]
public abstract partial class AbilityEffect : Resource
{
    public abstract void ApplyEffect(Node3D caster, bool isAuthoritative);
}