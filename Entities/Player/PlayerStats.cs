using System;
using Godot;

namespace BattleGlorps.Entities.Player;

public partial class PlayerStats : Node
{
    public int Strength;
    public int Intelligence;
    public int Agility;

    public int MaxHealth => 100 + (Strength * 10);
    public float MovementSpeed => 5.0f + (Agility * 0.1f);
    
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    public event Action<int, int> OnHealthChanged; // current, max
    public event Action OnDied;

    public void Initialize(ClassData data)
    {
        Strength = data.BaseStrength;
        Intelligence = data.BaseIntelligence;
        Agility = data.BaseAgility;

        CurrentHealth = MaxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        GD.Print($"[playerstats] took {damage} damage. health {CurrentHealth}/{MaxHealth}");

        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

        if (IsDead)
        {
            GD.Print($"player died");
            OnDied?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void Respawn()
    {
        CurrentHealth = MaxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        GD.Print($"respawned with {CurrentHealth} health");
    }
}
