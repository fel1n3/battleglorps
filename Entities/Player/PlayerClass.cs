using Godot;

public partial class PlayerClass : CharacterBody3D
{
    [Export] public float Speed = 8.0f;
    [Export] public float RotationSpeed = 10.0f;
    
    protected NavigationAgent3D _navAgent;
    private bool _isActive = false;
    
    public override void _Ready()
    {
        _navAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
        _navAgent.PathDesiredDistance = 0.2f;
        _navAgent.TargetDesiredDistance = 0.2f;
        _navAgent.PathHeightOffset = -0.6f;
    }

    public void EnableNavigation(bool enable)
    {
        _isActive = enable;
        _navAgent.ProcessMode = enable ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
    }

    public void SetMoveTarget(Vector3 targetPos)
    {
        if (!_isActive) return;
        _navAgent.TargetPosition = targetPos;
    }

    public void ProcessMovement(double delta)
    {
        if (!_isActive || _navAgent.IsNavigationFinished())
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            return;
        }
        
        Vector3 currentPos = GlobalTransform.Origin;
        Vector3 nextPathPos = _navAgent.GetNextPathPosition();
        GD.Print($"{currentPos} -> {nextPathPos}");

        Vector3 direction = (nextPathPos - currentPos).Normalized();

        Vector3 newVelocity = direction * Speed;
        Velocity = newVelocity;
        MoveAndSlide();
    }

}
