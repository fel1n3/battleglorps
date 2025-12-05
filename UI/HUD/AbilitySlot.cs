using BattleGlorps.Classes;
using Godot;

namespace BattleGlorps.UI.HUD;

public partial class AbilitySlot : PanelContainer
{
    [Export] private TextureRect _iconRect;
    [Export] private Label _keyLabel;
    [Export] private Label _cooldownLabel;
    [Export] private ColorRect _cooldownOverlay;

    private AbilityController _controller;
    private Timer _cooldownTimer;

    public void Initialize(AbilityController controller, int abilityIndex)
    {
        _controller = controller;

        if (_keyLabel != null)
        {
            string actionName = $"ability_{abilityIndex}";
            string keyText = GetKeybindText(actionName);
            _keyLabel.Text = keyText;
        }

        if (controller is {AbilityToCast: not null})
        {
            if (_iconRect != null && controller.AbilityToCast.Icon != null)
            {
                _iconRect.Texture = controller.AbilityToCast.Icon;
            }

            _cooldownTimer = controller.GetNodeOrNull<Timer>("Timer");
            
        }
        else
        {
            if (_iconRect != null)
            {
                _iconRect.Modulate = new Color(1, 1, 1, 0.3f);
            }
        }
    }

    private string GetKeybindText(string actionName)
    {
        if (!InputMap.HasAction(actionName))
        {
            return "?";
        }

        var events = InputMap.ActionGetEvents(actionName);
        if (events.Count == 0)
        {
            return "?";
        }

        InputEvent inputEvent = events[0];

        switch (inputEvent)
        {
            case InputEventKey keyEvent:
            {
                string keyName = OS.GetKeycodeString(keyEvent.PhysicalKeycode);
                if (string.IsNullOrEmpty(keyName))
                {
                    keyName = OS.GetKeycodeString(keyEvent.Keycode);
                }

                return keyName.ToUpper();
            }
            case InputEventMouseButton mouseEvent:
                return $"M{(int) mouseEvent.ButtonIndex}";
            case InputEventJoypadButton joyEvent:
                return $"J{joyEvent.ButtonIndex}";
            default:
                return "?";
        }
    }

    public void Update()
    {
        if (_cooldownTimer == null || _controller == null) return;

        if (_cooldownTimer.IsStopped())
        {
            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.Visible = false;
            }

            if (_cooldownLabel != null)
            {
                _cooldownLabel.Visible = false;
            }
        }
        else
        {
            float timeLeft = (float) _cooldownTimer.TimeLeft;
            float cooldownDuration = (float) _cooldownTimer.WaitTime;
            float progress = 1.0f - (timeLeft / cooldownDuration);

            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.Visible = true;
                _cooldownOverlay.Scale = new Vector2(1.0f, 1.0f - progress);
                _cooldownOverlay.PivotOffset = new Vector2(0, _cooldownOverlay.Size.Y);
            }

            if (_cooldownLabel != null)
            {
                _cooldownLabel.Visible = true;
                _cooldownLabel.Text = Mathf.Ceil(timeLeft).ToString();
            }
        }
    }

}