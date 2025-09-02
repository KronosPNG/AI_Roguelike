using Godot;

public partial class Dummy : StaticBody2D, IEntity
{  
	// ---- Node references ----
	private AnimatedSprite2D _sprite;
	private CollisionShape2D _collisionShape;
	private Area2D _hitArea;

	// ---- Health properties ----
	public float CurrentHealth { get; private set; }
	[Export] public float MaxHealth { get; private set; }
	[Export] public bool IsInvulnerable { get; private set; }
	public bool IsAlive => CurrentHealth > 0;

	// ---- Visual properties ----
	[Export] public bool FlipSpriteHorizontally = false;

	public override void _Ready()
	{
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_collisionShape = GetNodeOrNull<CollisionShape2D>("PhysicalCollision");
		_hitArea = GetNodeOrNull<Area2D>("HitArea");

		if (_sprite == null)
		{
			GD.PrintErr("Sprite node not found");
			return;
		}

		if (_collisionShape == null)
		{
			GD.PrintErr("PhysicalCollision node not found");
			return;
		}

		if (_hitArea == null)
		{
			GD.PrintErr("HitArea node not found");
			return;
		}

		if (FlipSpriteHorizontally)
		{
			_sprite.FlipH = true;
		}

		CurrentHealth = MaxHealth;
	}

	public void ApplyDamage(float amount)
	{
		if (!IsAlive) return;

		if (!IsInvulnerable) CurrentHealth -= amount;
		
		_sprite.Play("hit");
		_sprite.Frame = 1;

		GD.Print("Dummy took " + amount + " damage. Current Health: " + CurrentHealth);

		if (CurrentHealth <= 0)
		{
			CurrentHealth = 0;
			Die();
		}
	}

	public void ApplyStatusEffect(StatusEffectType effectType, float duration, float intensity = 1)
	{
		throw new System.NotImplementedException();
	}

	public bool CanTakeDamageFrom(Node2D attacker)
	{
		throw new System.NotImplementedException();
	}

	public void Die()
	{   
		GD.Print("Dummy has died.");

		CurrentHealth = 0;
		_hitArea.Monitoring = false;
		_sprite.Play("die");
	}

	public bool HasStatusEffect(StatusEffectType effectType)
	{
		throw new System.NotImplementedException();
	}

	public void Heal(float amount)
	{
		if (IsAlive)
		{
			CurrentHealth += amount;
			if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
		}
	}
	
	public void PlayDeathEffect()
	{
		throw new System.NotImplementedException();
	}

	public void PlayHitEffect(Vector2 hitPosition)
	{
		throw new System.NotImplementedException();
	}

	public void RemoveStatusEffect(StatusEffectType effectType)
	{
		throw new System.NotImplementedException();
	}

	public void ShowDamageNumber(float damage)
	{
		throw new System.NotImplementedException();
	}
}
