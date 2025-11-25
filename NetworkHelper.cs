namespace BattleGlorps;

using Godot;
using System.IO;

public enum PacketType : byte
{
    Handshake = 0,
    SpawnPlayer = 1,
    PlayerUpdate = 2,
    ClassSelected = 3,
    GameStart = 4,
    AbilityCast=5,
}

public static class NetworkHelper
{

    public static void Write(this BinaryWriter writer, Vector3 vec)
    {
        writer.Write(vec.X);
        writer.Write(vec.Y);
        writer.Write(vec.Z);
    }

    public static Vector3 ReadVector3(this BinaryReader reader)
    {
        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
    
}