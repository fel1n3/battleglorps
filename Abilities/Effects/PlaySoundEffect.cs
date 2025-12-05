using Godot;

namespace BattleGlorps.Classes;

[GlobalClass]
public partial class PlaySoundEffect : AbilityEffect
{
    [Export] public AudioStream SoundClip;


    public override void ApplyEffect(Node3D caster, bool isAuthoritative)
    {
        var audioPlayer = new AudioStreamPlayer3D();
        caster.AddChild(audioPlayer);
        audioPlayer.Stream = SoundClip;
        audioPlayer.Position = Vector3.Zero;

        audioPlayer.Finished += audioPlayer.QueueFree;
        audioPlayer.Play();
    }
}