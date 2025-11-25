using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerClass : CharacterBody3D
{
    [Export] public ClassData ClassData { get; set; }

    public float CurrentHealth { get; private set; }
    public float CurrentMana { get; private set; }

    private Vector3 _targetPosition;
    private bool _hasTarget = false;
    private Node3D _selectionIndicator;

    private MeshInstance3D _meshInstance;
    private Node3D _healthBarPivot;
    private ProgressBar _healthBar;
    public override void _Ready()
    {
        if (ClassData == null)
        {
            GD.PrintErr("No class assigned!");
            return;
        }

        CurrentHealth = ClassData.MaxHealth;
        CurrentMana = ClassData.MaxMana;
        _targetPosition = GlobalPosition;

        CreateSelectionIndicator();
        CreateHealthBar();
        SetupVisuals();
    }

    private void SetupVisuals()
    {
        _meshInstance = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (_meshInstance != null)
        {
            var material = new StandardMaterial3D();
            material.AlbedoColor = ClassData.ClassColor;
            _meshInstance.MaterialOverride = material;
            _meshInstance.Scale = ClassData.ModelScale;
        }
        
    }

    private void CreateHealthBar()
    {
        _healthBarPivot = new Node3D();
        _healthBarPivot.Position = new Vector3(0, 2.5f, 0);
        AddChild(_healthBarPivot);

        SubViewportContainer subViewport = new();
        subViewport.CustomMinimumSize = new Vector2(100, 15);

        SubViewport viewport = new();
        viewport.TransparentBg = true;
        viewport.Size = new Vector2I(100, 15);
        subViewport.AddChild(viewport);

        _healthBar = new ProgressBar();
        _healthBar.CustomMinimumSize = new Vector2(100, 15);
        _healthBar.MaxValue = ClassData.MaxHealth;
        _healthBar.Value = CurrentHealth;
        _healthBar.ShowPercentage = false;
        viewport.AddChild(_healthBar);

    }

    private void CreateSelectionIndicator()
    {
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

            if (distance > ClassData.StoppingDistance)
            {
                direction = direction.Normalized();

                if (direction.LengthSquared() > 0)
                {
                    Vector3 lookDir = new Vector3(direction.X, 0, direction.Z);
                    if (lookDir.LengthSquared() > 0)
                    {
                        Quaternion targetRotation = Quaternion.FromEuler(new Vector3(0, Mathf.Atan2(lookDir.X, lookDir.Z), 0));
                        Quaternion currentRotation = Quaternion.FromEuler(Rotation);
                        Rotation = currentRotation.Slerp(targetRotation, ClassData.RotationSpeed *(float)delta).GetEuler();
                    }
                }

                Velocity = direction * ClassData.MoveSpeed;
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

    public virtual void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
        _hasTarget = true;

        _selectionIndicator.GlobalPosition = new Vector3(position.X, position.Y + 0.2f, position.Z);
        _selectionIndicator.Visible = true;
    }
}
