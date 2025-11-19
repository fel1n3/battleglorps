using Godot;
using System;

public partial class PlayerController : CharacterBody3D
{
    [Export] public float MoveSpeed = 5.0f;
    [Export] public float RotationSpeed = 10.0f;
    [Export] public float StoppingDistance = 0.5f;

    private Vector3 _targetPosition;
    private bool _hasTarget = false;
    private Node3D _selectionIndicator;

    public override void _Ready()
    {
        _targetPosition = GlobalPosition;

        _selectionIndicator = new Node3D();
        AddChild(_selectionIndicator);

        var mesh = new MeshInstance3D();
        mesh.Mesh = new SphereMesh {Radius = 0.2f, Height = 0.4f};
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0, 1, 0, 0.5f);
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mesh.MaterialOverride = material;
        _selectionIndicator.AddChild(mesh);
        _selectionIndicator.Visible = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_hasTarget)
        {
            Vector3 direction = (_targetPosition - GlobalPosition);
            float distance = direction.Length();

            if (distance > StoppingDistance)
            {
                direction = direction.Normalized();

                if (direction.LengthSquared() > 0)
                {
                    Vector3 lookDir = new Vector3(direction.X, 0, direction.Z);
                    if (lookDir.LengthSquared() > 0)
                    {
                        Quaternion targetRotation = Quaternion.FromEuler(new Vector3(0, Mathf.Atan2(lookDir.X, lookDir.Z), 0));
                        Quaternion currentRotation = Quaternion.FromEuler(Rotation);
                        Rotation = currentRotation.Slerp(targetRotation, RotationSpeed *(float)delta).GetEuler();
                    }
                }

                Velocity = direction * MoveSpeed;
                MoveAndSlide();
            }
            else
            {
                _hasTarget = false;

                Velocity = Vector3.Zero;
                _selectionIndicator.Visible = false;

            }
        }
    }

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
        _hasTarget = true;

        _selectionIndicator.GlobalPosition = new Vector3(position.X, position.Y + 0.2f, position.Z);
        _selectionIndicator.Visible = true;
    }
}
