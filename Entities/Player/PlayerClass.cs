using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerClass : CharacterBody3D
{
    [Export] public float Speed = 8.0f;
    [Export] public float RotationSpeed = 10.0f;
    
    protected NavigationAgent3D _navAgent;
    
    public override void _Ready()
    {
        _navAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
        _navAgent.PathDesiredDistance = 0.5f;
        _navAgent.TargetDesiredDistance = 0.5f;
    }

    public void SetMoveTarget(Vector3 targetPos)
    {
        _navAgent.TargetPosition = targetPos;
    }

    public virtual void ProcessMovement(double delta)
    {
        if (_navAgent.IsNavigationFinished())
        {
            Velocity = Vector3.Zero;
            return;
        }

        Vector3 nextPathPos = _navAgent.GetNextPathPosition();
        Vector3 direction = (nextPathPos - GlobalPosition).Normalized();

        if (direction != Vector3.Zero)
        {
            float angle = Mathf.Atan2(direction.X, direction.Z);
            Vector3 newRot = Rotation;
            newRot.Y = Mathf.LerpAngle(Rotation.Y, angle, RotationSpeed * (float) delta);
            Rotation = newRot;
        }

        Velocity = direction * Speed;
        MoveAndSlide();
    }

}
