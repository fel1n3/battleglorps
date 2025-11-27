using Godot;
using System;
using System.Collections.Generic;

public partial class Lobby : Control
{
    [Export] private PackedScene _classCardPrefab;
    [Export] private ClassData[] _availableClasses;

    [Export] private GridContainer _grid;
    [Export] private Label _descriptionLabel;
    [Export] private Node3D _modelPivot;
    [Export] private Button _startButton;

    private List<ClassCard> _cards = [];
    private ClassData _selectedData;

    public override void _Ready()
    {
        GenerateGrid();
       // _startButton.Pressed += OnStartGamePressed;
    }

    private void GenerateGrid()
    {
        foreach(Node child in _grid.GetChildren()) child.QueueFree();
        _cards.Clear();

        foreach(var data in _availableClasses)
        {
            var cardInstance = _classCardPrefab.Instantiate<ClassCard>();
            _grid.AddChild(cardInstance);

            cardInstance.Setup(data);

            cardInstance.ClassSelected += OnClassSelected;

            _cards.Add(cardInstance);
        }
    }

    private void OnClassSelected(ClassData data)
    {
        _selectedData = data;

       // _descriptionLabel.Text = data.Description;

       foreach (var card in _cards)
       {
           card.SetHighLight(true);
       }
    }
}
