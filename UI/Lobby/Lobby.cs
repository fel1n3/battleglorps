using Godot;
using System;
using System.Collections.Generic;

public partial class Lobby : Control
{
    [Export] private PackedScene _classCardPrefab;

    [Export] private GridContainer _grid;
    [Export] private Label _descriptionLabel;
    [Export] private Node3D _modelPivot;
    [Export] private Button _startButton;

    private List<ClassCard> _cards = [];
    private ClassData _selectedData;

    public override void _Ready()
    {
        var availableClasses = SteamNetworkManager.Instance.GetAllClasses();
        
        GenerateGrid(availableClasses);
       // _startButton.Pressed += OnStartGamePressed;
    }

    private void GenerateGrid(ClassData[] availableClasses)
    {
        foreach(Node child in _grid.GetChildren()) child.QueueFree();
        _cards.Clear();

        foreach(ClassData data in availableClasses)
        {
            ClassCard cardInstance = _classCardPrefab.Instantiate<ClassCard>();
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
