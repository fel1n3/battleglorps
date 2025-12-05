using Steamworks;
using Godot;
using System.IO;

namespace BattleGlorps;

public enum PacketType : byte
{
    Handshake = 0,
    SessionSync = 1,
    GameStart = 2,
    PlayerUpdate = 3,
    ClassSelected = 4,
    AbilityCast = 5,
    MoveCommand = 6,
    ReadyStatus = 7,
    ChatMessage = 8,
}
public struct LobbyInfo
{
    public ulong LobbyId;
    public string Name;
    public int PlayerCount;
    public int MaxPlayers;
}

public class PlayerSession
{
    public byte NetworkId;
    public CSteamID SteamId;
    public int SelectedClassIndex;
    public string Name;
    public bool IsReady;
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