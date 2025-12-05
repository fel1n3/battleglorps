using BattleGlorps.Classes;
using BattleGlorps.Entities.Player;
using Godot;

namespace BattleGlorps.UI.HUD;

public partial class GameHUD : Control
{
    [Export] private ProgressBar _healthBar;
    [Export] private Label _healthText;
    [Export] private Label _classNameLabel;
    [Export] private HBoxContainer _abilityContainer;

    private NetworkedPlayer _player;
    private AbilitySlot[] _abilitySlots = new AbilitySlot[4];

    public void Initialize(NetworkedPlayer player)
    {
        _player = player;

        var stats = player.GetNodeOrNull<PlayerStats>("PlayerStats");
        if (stats != null)
        {
            stats.OnHealthChanged += UpdateHealth;
            UpdateHealth(stats.CurrentHealth, stats.MaxHealth);
        }

        if (_abilityContainer != null)
        {
            var children = _abilityContainer.GetChildren();
            for (int i = 0; i < Mathf.Min(children.Count, 4); i++)
            {
                _abilitySlots[i] = children[i] as AbilitySlot;
                
            }
        }

        SetupAbilitySlots();
    }

    private void SetupAbilitySlots()
    {
        var abilityManager = _player?.GetNodeOrNull<AbilityManager>("AbilityManager");
        if(abilityManager == null) return;

        var abilities = abilityManager.GetChildren();

        for (int i = 0; i < 4; i++)
        {
            if (_abilitySlots[i] != null)
            {
                AbilityController controller = abilityManager.GetAbilityByIndex(i);
                _abilitySlots[i].Initialize(controller, i + 1);
            }
        }
    }

    private void UpdateHealth(int current, int max)
    {
        if (_healthBar != null)
        {
            _healthBar.MaxValue = max;
            _healthBar.Value = current;
        }

        if (_healthText != null)
        {
            _healthText.Text = $"{current} / {max}";
        }
    }

    public override void _Process(double delta)
    {
        foreach (AbilitySlot slot in _abilitySlots)
        {
            slot?.Update();
        }
    }

    public override void _ExitTree()
    {
        var stats = _player?.GetNodeOrNull<PlayerStats>("PlayerStats");
        if (stats != null)
        {
            stats.OnHealthChanged -= UpdateHealth;
        }
    }
}