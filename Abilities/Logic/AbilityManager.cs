using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BattleGlorps.Classes;

public partial class AbilityManager : Node
{
    private List<AbilityController> _activeAbilities = new List<AbilityController>();

    public void InitializeAbilities(ClassData data)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }
        _activeAbilities.Clear();

        if (data.Abilities == null) return;

        foreach (var abilityData in data.Abilities)
        {
            if(abilityData == null) continue;

            var controller = new AbilityController();
            controller.Name = abilityData.AbilityName;
            controller.AbilityToCast = abilityData;
            
            AddChild(controller);
            _activeAbilities.Add(controller);
        }
    }

    public void CastAbilityByIndex(int index)
    {
        if (index >= 0 && index < _activeAbilities.Count)
        {
            _activeAbilities[index].TryCast();
        }
    }

    public void TriggerVisualsByName(string abilityName)
    {
        foreach (AbilityController ability in _activeAbilities.Where(ability => ability.AbilityToCast.AbilityName == abilityName))
        {
            ability.ExecuteVisualsOnly();
            return;
        }
    }

    public AbilityController GetAbilityByIndex(int index)
    {
        if (index >= 0 && index < _activeAbilities.Count)
        {
            return _activeAbilities[index];
        }

        return null;
    }

    public int GetAbilityCount() => _activeAbilities.Count;
}