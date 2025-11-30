using System.Collections.Generic;
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
        foreach (var ability in _activeAbilities)
        {
            if (ability.AbilityToCast.AbilityName == abilityName)
            {
                if (ability.AbilityToCast.AbilityName == abilityName)
                {
                    ability.ExecuteVisualsOnly(abilityName);
                    return;
                }
            }
        }
    }
}