using System.IO;
using Godot;

namespace BattleGlorps.Classes;

public partial class AbilityController : Node3D
{
    [Export] public AbilityData AbilityToCast;

    private Timer _cooldownTimer;

    public override void _Ready()
    {
        _cooldownTimer = new Timer();
        _cooldownTimer.OneShot = true;
        AddChild(_cooldownTimer);
    }

    public void TryCast()
    {
        if (!_cooldownTimer.IsStopped()) return;
        
        ExecuteEffects();
        SendCastPacket();

        _cooldownTimer.WaitTime = AbilityToCast.Cooldown;
        _cooldownTimer.Start();
    }

    private void SendCastPacket()
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.AbilityCast);
        writer.Write(AbilityToCast.AbilityName);

        SteamNetworkManager.Instance.SendBytesToAll(ms.ToArray());
    }

    public void ExecuteVisualsOnly(string abilityName)
    {
        if (AbilityToCast.AbilityName == abilityName)
        {
            ExecuteEffects();
        }
    }

    private void ExecuteEffects()
    {
        if (AbilityToCast == null || AbilityToCast.Effects == null) return;

        foreach (var effect in AbilityToCast.Effects)
        {
            effect.ApplyEffect(this);
        }
    }
}