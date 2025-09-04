using Godot;

public partial class Entity : CharacterBody2D, IEntity
{
	// ---- Node references ----
	protected AnimatedSprite2D _sprite;
	protected CollisionShape2D _collisionShape;
	protected Area2D _hitArea;
	protected NavigationAgent2D _navAgent;

	// ---- Health properties ----
	public float CurrentHealth { get; private set; }
	[Export] public float MaxHealth { get; private set; }
	[Export] public bool IsInvulnerable { get; private set; }
	public bool IsAlive => CurrentHealth > 0;

	// ---- Movement properties ----
	[Export] public float BaseSpeed { get; private set; } = 200f;
	[Export] public float DetectionRange { get; private set; } = 300f;
	[Export] public float AttackRange { get; private set; } = 50f;
	protected Vector2 _facingDirection = Vector2.Right;
	protected sbyte _lastHorizontalFacing = 1;

	// ---- State Machine ----
	public enum EntityState
	{
		Idle,
		Wandering,
		Chasing,
		Attacking,
		Hit,
		Dying,
		Dead
	}

	protected EntityState _currentState = EntityState.Idle;
	protected EntityState _previousState = EntityState.Idle;

	// ---- Timers ----
	protected float _stateTimer = 0f;
	protected float _hitStunDuration = 0.5f;
	protected float _wanderTimer = 0f;
	protected float _wanderCooldown = 2f;

	// ---- AI Properties ----
	protected Node2D _target;
	protected Vector2 _wanderDirection = Vector2.Zero;
	protected Vector2 _lastKnownTargetPosition = Vector2.Zero;

	// ---- Signals ----
	[Signal] public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);
	[Signal] public delegate void DiedEventHandler();
	[Signal] public delegate void StateChangedEventHandler(string newState);


	public override void _Ready()
	{
		InitializeNodes();
		InitializeEntity();

		if (_sprite != null)
			_sprite.AnimationFinished += OnAnimationFinished;

		CurrentHealth = MaxHealth;
		TransitionToState(EntityState.Idle);
	}

	protected virtual void InitializeNodes()
	{
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_collisionShape = GetNodeOrNull<CollisionShape2D>("PhysicalCollision");
		_hitArea = GetNodeOrNull<Area2D>("HitArea");
		_navAgent = GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D");

		if (_sprite == null) GD.PrintErr($"{Name}: Sprite node not found");
		if (_collisionShape == null) GD.PrintErr($"{Name}: PhysicalCollision node not found");
		if (_hitArea == null) GD.PrintErr($"{Name}: HitArea node not found");
		if (_navAgent == null) GD.PrintErr($"{Name}: NavigationAgent2D node not found");
	}

	protected virtual void InitializeEntity()
	{
		// Override in derived classes
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsAlive && _currentState != EntityState.Dead)
			return;

		UpdateTimers((float)delta);
		UpdateAI((float)delta);
		HandleStateTransitions((float)delta);
		ApplyMovementByState((float)delta);
		UpdateAnimationIfNeeded();
	}

	protected virtual void UpdateTimers(float delta)
	{
		_stateTimer += delta;
		_wanderTimer -= delta;
	}

	protected virtual void UpdateAI(float delta)
	{
		// Find target (usually player)
		if (_target == null)
			_target = FindTarget();

		// Update facing direction based on movement
		if (Velocity.LengthSquared() > 0.1f)
		{
			_facingDirection = Velocity.Normalized();
			if (!Mathf.IsEqualApprox(_facingDirection.X, 0))
				_lastHorizontalFacing = (sbyte)Mathf.Sign(_facingDirection.X);
		}
	}

	protected virtual Node2D FindTarget()
	{
		// Find player node - override in derived classes for different targeting logic
		return GetTree().GetFirstNodeInGroup("Player") as Node2D;
	}

	protected virtual void HandleStateTransitions(float delta)
	{
		switch (_currentState)
		{
			case EntityState.Idle:
				HandleIdleTransitions();
				break;
			case EntityState.Wandering:
				HandleWanderingTransitions();
				break;
			case EntityState.Chasing:
				HandleChasingTransitions();
				break;
			case EntityState.Attacking:
				HandleAttackingTransitions();
				break;
			case EntityState.Hit:
				HandleHitTransitions();
				break;
			case EntityState.Dying:
				HandleDyingTransitions();
				break;
			case EntityState.Dead:
				// Dead entities don't transition
				break;
		}
	}

	protected virtual void HandleIdleTransitions()
	{
		if (CanSeeTarget())
		{
			TransitionToState(EntityState.Chasing);
			return;
		}

		// Random wandering
		if (_wanderTimer <= 0 && GD.Randf() < 0.3f)
		{
			TransitionToState(EntityState.Wandering);
		}
	}

	protected virtual void HandleWanderingTransitions()
	{
		if (CanSeeTarget())
		{
			TransitionToState(EntityState.Chasing);
			return;
		}

		// Stop wandering after some time
		if (_stateTimer > 3f)
		{
			TransitionToState(EntityState.Idle);
		}
	}

	protected virtual void HandleChasingTransitions()
	{
		if (!CanSeeTarget())
		{
			// Lost target, return to idle after a moment
			if (_stateTimer > 2f)
				TransitionToState(EntityState.Idle);
			return;
		}

		// Close enough to attack
		if (IsInAttackRange())
		{
			TransitionToState(EntityState.Attacking);
		}
	}

	protected virtual void HandleAttackingTransitions()
	{
		// Attack animation will handle transition back via OnAnimationFinished
	}

	protected virtual void HandleHitTransitions()
	{
		if (_stateTimer >= _hitStunDuration)
		{
			if (CanSeeTarget())
				TransitionToState(EntityState.Chasing);
			else
				TransitionToState(EntityState.Idle);
		}
	}

	protected virtual bool CanSeeTarget()
	{
		if (_target == null || !IsInstanceValid(_target))
			return false;

		float distance = GlobalPosition.DistanceTo(_target.GlobalPosition);
		return distance <= DetectionRange;
	}

	protected virtual bool IsInAttackRange()
	{
		if (_target == null || !IsInstanceValid(_target))
			return false;

		float distance = GlobalPosition.DistanceTo(_target.GlobalPosition);
		return distance <= AttackRange;
	}

	protected virtual void HandleDyingTransitions()
	{
		// Death animation will handle transition to Dead via OnAnimationFinished
	}

	protected virtual void ApplyMovementByState(float delta)
	{
		switch (_currentState)
		{
			case EntityState.Idle:
				Velocity = Vector2.Zero;
				break;

			case EntityState.Wandering:
				Velocity = _wanderDirection * BaseSpeed * 0.5f;
				break;

			case EntityState.Chasing:
				ChaseTarget();
				break;

			case EntityState.Attacking:
				Velocity = Vector2.Zero; // Stop during attack
				break;

			case EntityState.Hit:
			case EntityState.Dying:
			case EntityState.Dead:
				Velocity = Vector2.Zero;
				break;
		}

		MoveAndSlide();
	}

	protected virtual void ChaseTarget()
	{
		if (_target == null || !IsInstanceValid(_target))
		{
			Velocity = Vector2.Zero;
			return;
		}

		// Use NavigationAgent2D if available
		if (_navAgent != null)
		{
			_navAgent.TargetPosition = _target.GlobalPosition;
			if (!_navAgent.IsNavigationFinished())
			{
				Vector2 direction = GlobalPosition.DirectionTo(_navAgent.GetNextPathPosition());
				Velocity = direction * BaseSpeed;
			}
			else
			{
				Velocity = Vector2.Zero;
			}
		}
		else
		{
			// Direct movement towards target
			Vector2 direction = GlobalPosition.DirectionTo(_target.GlobalPosition);
			Velocity = direction * BaseSpeed;
		}
	}

	protected virtual void TransitionToState(EntityState newState)
	{
		if (_currentState == newState) return;

		OnExitState(_currentState);
		_previousState = _currentState;
		_currentState = newState;
		_stateTimer = 0f;
		OnEnterState(newState);

		EmitSignal(SignalName.StateChanged, newState.ToString());
	}

	protected virtual void OnEnterState(EntityState state)
	{
		switch (state)
		{
			case EntityState.Idle:
				_wanderTimer = _wanderCooldown;
				break;

			case EntityState.Wandering:
				GenerateWanderDirection();
				_wanderTimer = _wanderCooldown;
				break;

			case EntityState.Chasing:
				if (_target != null)
					_lastKnownTargetPosition = _target.GlobalPosition;
				break;

			case EntityState.Attacking:
				PerformAttack();
				break;

			case EntityState.Hit:
				_sprite.Play(GetAnimationForState(state));
				break;

			case EntityState.Dying:
				if (_hitArea != null)
					_hitArea.Monitoring = false;
				break;
		}
	}

	protected virtual void OnExitState(EntityState state)
	{
		// Override in derived classes for state cleanup
	}

	protected virtual void GenerateWanderDirection()
	{
		_wanderDirection = new Vector2(
			GD.Randf() * 2 - 1,
			GD.Randf() * 2 - 1
		).Normalized();
	}

	protected virtual void PerformAttack()
	{
		// Override in derived classes for specific attack behavior
		GD.Print($"{Name} performs basic attack");

		// Deal damage to target if in range
		if (_target != null && IsInAttackRange())
		{
			if (_target.HasMethod("ApplyDamage"))
				_target.Call("ApplyDamage", 10f); // Basic damage
		}
	}

	protected virtual void UpdateAnimationIfNeeded()
	{
		if (_sprite == null) return;

		bool stateChanged = _currentState != _previousState;

		// Update sprite facing
		if (!Mathf.IsEqualApprox(Velocity.X, 0))
			_sprite.FlipH = Velocity.X < 0;
		else
			_sprite.FlipH = _lastHorizontalFacing < 0;

		// Set animation based on state
		string targetAnimation = GetAnimationForState(_currentState);

		if (stateChanged || _sprite.Animation != targetAnimation)
		{
			if (_sprite.SpriteFrames.HasAnimation(targetAnimation))
				_sprite.Play(targetAnimation);
		}
	}

	protected virtual string GetAnimationForState(EntityState state)
	{
		return state switch
		{
			EntityState.Idle => "idle",
			EntityState.Wandering => "walk",
			EntityState.Chasing => "walk",
			EntityState.Attacking => "attack",
			EntityState.Hit => "hit",
			EntityState.Dying => "die",
			EntityState.Dead => "die",
			_ => "idle"
		};
	}

	protected virtual void OnAnimationFinished()
	{
		string animName = _sprite.Animation;

		switch (_currentState)
		{
			case EntityState.Attacking:
				if (animName == "attack")
				{
					// Return to appropriate state after attack
					if (CanSeeTarget())
						TransitionToState(EntityState.Chasing);
					else
						TransitionToState(EntityState.Idle);
				}
				break;

			case EntityState.Dying:
				if (animName == "die")
				{
					TransitionToState(EntityState.Dead);
				}
				break;
		}
	}

	// ---- IEntity Implementation ----

	public void ApplyDamage(float amount)
	{
		if (!IsAlive) return;

		if (!IsInvulnerable) CurrentHealth -= amount;

		// Transition to Hit state instead of directly playing animation
		TransitionToState(EntityState.Hit);

		GD.Print($"Entity {Name} took {amount} damage. Current Health: {CurrentHealth}");

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
		GD.Print($"Entity {Name} has died.");

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
	
	// ---- Public Getters for AI customization ----
    public EntityState CurrentState => _currentState;
    public Node2D Target => _target;
    public Vector2 FacingDirection => _facingDirection;
}
