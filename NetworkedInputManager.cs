using Godot;
using System;
using Godot.Collections;

public partial class NetworkedInputManager : Node3D
{
    private CameraController _cameraController;
    private NetworkedPlayer _localPlayer;
    private Camera3D _camera;
    private bool _isRMBHeld = false;

    public override void _Ready()
    {
        _cameraController = GetNode<CameraController>("/root/Main/CameraController");
        _camera = GetViewport().GetCamera3D();
    }

    public void SetLocalPlayer(NetworkedPlayer player)
    {
        _localPlayer = player;
        GD.Print("local player set for input manager");
    }

    public override void _Process(double delta)
    {
        if (_localPlayer == null) return;
        
        if (_isRMBHeld)
        {
            Vector2 mousePos = GetViewport().GetMousePosition();
            Vector3? hitPos = GetMouseWorldPosition(mousePos);
            if (hitPos.HasValue)
            {
                _localPlayer.SetTargetPosition(hitPos.Value);
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_localPlayer == null) return;
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                _isRMBHeld = mouseButton.Pressed;

                if (mouseButton.Pressed)
                {
                    Vector3? hitPosition = GetMouseWorldPosition(mouseButton.Position);
                    if (hitPosition.HasValue)
                    {
                        _localPlayer.SetTargetPosition(hitPosition.Value);
                    }
                }
            }
        }
    }

    private Vector3? GetMouseWorldPosition(Vector2 mousePosition)
    {
        if (_camera == null) return null;

        Vector3 from = _camera.ProjectRayOrigin(mousePosition);
        Vector3 to = from + _camera.ProjectRayNormal(mousePosition) * 1000;

        PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;

        Dictionary result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            return (Vector3) result["position"];
        }

        return null;
    }
}
