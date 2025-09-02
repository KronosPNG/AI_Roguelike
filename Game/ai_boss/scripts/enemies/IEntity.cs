using Godot;

public interface IEntity
{
    // Health and status properties
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsAlive { get; }
    bool IsInvulnerable { get; }

    // Core damage and health system
    void ApplyDamage(float amount);
    void Heal(float amount);
    void Die();
    
    // Status effects and damage types
    void ApplyStatusEffect(StatusEffectType effectType, float duration, float intensity = 1.0f);
    void RemoveStatusEffect(StatusEffectType effectType);
    bool HasStatusEffect(StatusEffectType effectType);
    
    // Damage modifiers
    bool CanTakeDamageFrom(Node2D attacker);
    
    // Visual and audio feedback
    void ShowDamageNumber(float damage);
    void PlayHitEffect(Vector2 hitPosition);
    void PlayDeathEffect();
}
