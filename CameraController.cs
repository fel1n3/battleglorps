using Godot;
using System;

public partial class CameraController : Node3D
{
    [Export] public float PanSpeed = 20.0f;
    [Export] public float EdgePanMargin = 50.0f;
    [Export] public float ZoomSpeed = 2.0f;
    [Export] public float MinZoom = 5.0f;
    [Export] public float MaxZoom = 30.0f;
    [Export] public float CameraAngle = 45.0f;
    [Export] public Vector2 MapBoundsMin = new Vector2(-50, -50);
    [Export] public Vector2 MapBoundsMax = new Vector2(50, 50);

    private Camera3D _camera;
    private float _currentZoom = 15.0f;
    private Vector3 _panVelocity = Vector3.Zero;

    public override void _Ready()
    {
        _camera = GetNodeOrNull<Camera3D>("Camera3D");
        if (_camera == null)
        {
            _camera = new Camera3D();
            AddChild(_camera);
        }

        UpdateCameraPosition();
    }

    public override void _Process(double delta)
    {
        HandleEdgePanning(delta);
        HandleKeyboardPanning(delta);

        Vector3 pos = GlobalPosition;
        pos.X = Mathf.Clamp(pos.X, MapBoundsMin.X, MapBoundsMax.X);
        pos.Z = Mathf.Clamp(pos.Z, MapBoundsMin.Y, MapBoundsMax.Y);
        GlobalPosition = pos;

        UpdateCameraPosition();
    }

    private void HandleEdgePanning(double delta)
    {
        if (!DisplayServer.WindowIsFocused())
        {
            _panVelocity = Vector3.Zero;
            return;
        }
        
        Vector2 mousePos = GetViewport().GetMousePosition();
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;

        _panVelocity = Vector3.Zero;

        if (mousePos.X < EdgePanMargin) 
            _panVelocity.X = 1;
        else if (mousePos.X > viewportSize.X - EdgePanMargin)
            _panVelocity.X = -1;

        if (mousePos.Y < EdgePanMargin)
            _panVelocity.Z = 1;
        else if (mousePos.Y > viewportSize.Y - EdgePanMargin)
            _panVelocity.Z = -1;

        if (_panVelocity.LengthSquared() > 0)
        {
            _panVelocity = _panVelocity.Normalized();
            GlobalPosition += _panVelocity * PanSpeed * (float) delta;
        }
    }

    private void HandleKeyboardPanning(double delta)
    {
        Vector3 input = Vector3.Zero;

        if (Input.IsActionPressed("ui_left"))
            input.X += 1;
        if (Input.IsActionPressed("ui_right"))
            input.X -= 1;
        if (Input.IsActionPressed("ui_up"))
            input.Z += 1;
        if (Input.IsActionPressed("ui_down"))
            input.Z -= 1;

        if (input.LengthSquared() > 0)
        {
            input = input.Normalized();
            GlobalPosition += input * PanSpeed * (float) delta;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
                _currentZoom = Mathf.Clamp(_currentZoom - ZoomSpeed, MinZoom, MaxZoom);
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
                _currentZoom = Mathf.Clamp(_currentZoom + ZoomSpeed, MinZoom, MaxZoom);
        }
    }

    private void UpdateCameraPosition()
    {
        if (_camera != null)
        {
            float angleRad = Mathf.DegToRad(CameraAngle);
            _camera.Position = new Vector3(0, _currentZoom * Mathf.Sin(angleRad), -_currentZoom * Mathf.Cos(angleRad));
            _camera.LookAt(GlobalPosition, Vector3.Up);
        }
    }
}
