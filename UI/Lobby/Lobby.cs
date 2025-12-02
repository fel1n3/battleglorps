using Godot;
using System.Collections.Generic;
using System.Linq;
using BattleGlorps;
using BattleGlorps.Core.Autoloads;

public partial class Lobby : Control
{
    [Export] private PackedScene _playerSlotPrefab;
    [Export] private Control _playerSlotsContainer;
    [Export] private ClassSelectModal _selectModal;
    [Export] private Button _changeClassButton;
    [Export] private Button _readyButton;
    [Export] private Button _startButton;

    private List<LobbyPlayerSlot> _slots = [];
    private ClassData[] _allClasses;

    public override void _Ready()
    {
        var availableClasses = SteamManager.Instance.GameState.GetAllClasses();
        for (int i = 0; i < 2; i++)
        {
            LobbyPlayerSlot slot = _playerSlotPrefab.Instantiate<LobbyPlayerSlot>();
            _playerSlotsContainer.AddChild(slot);
            _slots.Add(slot);
        }

        _changeClassButton.Pressed += () => _selectModal.Visible = true;
        _readyButton.Pressed += OnReadyPressed;
        _startButton.Pressed += () => SteamManager.Instance.GameState.RequestStartGame();

        SteamManager.Instance.GameState.OnSessionUpdated += RefreshUI;

        RefreshUI();
    }

    private void RefreshUI()
    {
        var sessions = SteamManager.Instance.GameState.GetAllSessions();
        
        foreach(var slot in _slots) slot.ResetSlot();

        int i = 0; foreach (var session in sessions.Values)
        {
            if (i >= _slots.Count) break;

            var slot = _slots[i];

            slot.UpdateInfo(session.Name, session.IsReady);

            if (session.SelectedClassIndex < _allClasses.Length)
            {
                var classData = _allClasses[session.SelectedClassIndex];
                slot.SetModel(classData.ModelScene);
            }

            i++;
        }

        _startButton.Visible = SteamManager.Instance.Connection.IsHost;
        _startButton.Disabled = !CheckAllReady(sessions);
    }

    private bool CheckAllReady(Dictionary<byte, PlayerSession> sessions) => sessions.Count != 0 && sessions.Values.All(s => s.IsReady);

    private void OnClassPicked(int index)
    {
        SteamManager.Instance.GameState.SelectClass(index);
    }

    private void OnReadyPressed()
    {
        SteamManager.Instance.GameState.ToggleReady();
    }

    private void OnStartPressed()
    {
        SteamManager.Instance.GameState.RequestStartGame();
    }
}
