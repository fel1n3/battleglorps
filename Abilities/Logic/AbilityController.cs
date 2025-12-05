using System.IO;
using BattleGlorps.Core.Autoloads;
using Godot;

namespace BattleGlorps.Classes;

public partial class AbilityController : Node3D
{
    [Export] public AbilityData AbilityToCast;

    private Timer _cooldownTimer;
    private NetworkedPlayer _ownerPlayer;

    public override void _Ready()
    {
        _cooldownTimer = new Timer();
        _cooldownTimer.OneShot = true;
        AddChild(_cooldownTimer);
        
        _ownerPlayer = GetParent()?.GetParent() as NetworkedPlayer;
        
    }

    public void TryCast()
    {
        if (!_cooldownTimer.IsStopped()) return;
        if (_ownerPlayer is not {IsLocalPlayer: true}) return;
        
        SendCastPacket();

        _cooldownTimer.WaitTime = AbilityToCast.Cooldown;
        _cooldownTimer.Start();

        ExecuteEffects(authoritative: SteamManager.Instance.Connection.IsHost);
    }

    private void SendCastPacket()
    {
        if (_ownerPlayer == null) return;
        
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write((byte) PacketType.AbilityCast);
        writer.Write(_ownerPlayer.NetworkId);
        writer.Write(AbilityToCast.AbilityName);

        SteamManager.Instance.Connection.SendToAll(ms.ToArray());
    }

    public void ExecuteVisualsOnly()
    {
        ExecuteEffects(authoritative: false);
    }

    private void ExecuteEffects(bool authoritative)
    {
        if (AbilityToCast?.Effects == null) return;

        foreach (var effect in AbilityToCast.Effects)
        {
            effect.ApplyEffect(this, authoritative);
        }
    }
}