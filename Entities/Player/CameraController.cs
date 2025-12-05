using Godot;

public partial class CameraController : Node3D
{
    
    [Export] public float PanSpeed = 20.0f;
    [Export] public float EdgeMargin = 20.0f;
    [Export] public float MinZoom = 10.0f;
    [Export] public float MaxZoom = 50.0f;
    [Export] public float ZoomSpeed = 2.0f;
    [Export] public float CameraAngle = 45.0f;
    
    private Camera3D _camera;
    private Vector3 _panVelocity = Vector3.Zero;
    private float _currentZoom = 10.0f;

    public Camera3D GetCamera()
    {
        if (_camera != null) return _camera;
        
        _camera = GetNodeOrNull<Camera3D>("Camera3D");
        if (_camera != null) return _camera;
        
        _camera = new Camera3D();
        AddChild(_camera);
        return _camera;
    }

    public override void _Ready()
    {
        GetCamera();
        UpdateCameraPosition();
    }

    public override void _Process(double delta)
    {
        HandleEdgePanning(delta);
        HandleKeyboardPanning(delta);
        UpdateCameraPosition();
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

        if (mousePos.X < EdgeMargin) _panVelocity.X = 1;
        if (mousePos.X > viewportSize.X - EdgeMargin) _panVelocity.X = -1;
        if (mousePos.Y < EdgeMargin) _panVelocity.Z = 1;
        if (mousePos.Y > viewportSize.Y - EdgeMargin) _panVelocity.Z = -1;

        if (_panVelocity.LengthSquared() > 0)
        {
            _panVelocity = _panVelocity.Normalized();
            GlobalPosition += _panVelocity * PanSpeed * (float) delta;
        }
    }

    private void HandleKeyboardPanning(double delta)
    {
        Vector3 input = Vector3.Zero;

        if (Input.IsActionPressed("ui_left")) input.X -= 1;
        if (Input.IsActionPressed("ui_right")) input.X += 1;
        if (Input.IsActionPressed("ui_up")) input.Z -= 1;
        if (Input.IsActionPressed("ui_down")) input.Z += 1;

        if (input.LengthSquared() > 0)
        {
            input = input.Normalized();
            GlobalPosition += input * PanSpeed * (float) delta;
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
