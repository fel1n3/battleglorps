using Godot;
using System.IO;
using BattleGlorps;
using BattleGlorps.Core.Autoloads;

public partial class NetworkedInputManager : Node3D
{
    private NetworkedPlayer _localPlayer;
    private Camera3D _camera;

    public void Initialize(NetworkedPlayer player, Camera3D cam)
    {
        _localPlayer = player;
        _camera = cam;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if(_localPlayer == null || _camera == null) return;
        
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                HandleRightClick();
            }
        }
    }


    private void HandleRightClick()
    {
        var mousePos = GetViewport().GetMousePosition();
        var from = _camera.ProjectRayOrigin(mousePos);
        var to = from + _camera.ProjectRayNormal(mousePos) * 1000f;

        var space = _localPlayer.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;

        var result = space.IntersectRay(query);

        if (result.Count > 0)
        {
            Node collider = (Node) result["collider"];
            Vector3 hitPos = (Vector3) result["position"];

            SendMovePacket(hitPos);
            
        }
    }

    private void SendMovePacket(Vector3 targetPos)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        
        writer.Write((byte)PacketType.MoveCommand);
        writer.Write(_localPlayer.NetworkId);
        writer.Write(targetPos);

        if (SteamManager.Instance.Connection.IsHost)
        {
            _localPlayer.Server_SetMoveTarget(targetPos);
        }
        else
        {
            SteamManager.Instance.Connection.SendToPeer(
                SteamManager.Instance.Lobby.CurrentLobbyId,
                ms.ToArray());
        }
        
        
    }
}
