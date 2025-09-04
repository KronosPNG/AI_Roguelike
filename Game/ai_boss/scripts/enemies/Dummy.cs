using Godot;

public partial class Dummy : Entity, IEntity
{
	// ---- Visual properties ----
	[Export] public bool FlipSpriteHorizontally = false;

	public override void _Ready()
	{
		base._Ready();

		if (FlipSpriteHorizontally)
		{
			_sprite.FlipH = true;
		}
	}

	protected override void UpdateTimers(float delta)
	{
		_stateTimer += delta;
	}

	protected override void UpdateAI(float delta)
	{
		// Find target (usually player)
		if (_target == null)
			_target = FindTarget();
	}

	protected override void UpdateAnimationIfNeeded()
	{
		if (_sprite == null) return;

		// For chasing state, don't use the default animation system
		// The ChaseTarget method handles animations directly
		if (_currentState == EntityState.Chasing)
		{
			return; // Let ChaseTarget handle the looking animations
		}

		// For other states, use the default animation system
		string targetAnimation = GetAnimationForState(_currentState);

		if (_sprite.Animation != targetAnimation)
		{
			if (_sprite.SpriteFrames.HasAnimation(targetAnimation))
				_sprite.Play(targetAnimation);
		}
	}

	protected override void ApplyMovementByState(float delta)
	{
		if (_currentState == EntityState.Chasing)
		{
			ChaseTarget();
		}

	}

	protected override void HandleIdleTransitions()
	{
		if (CanSeeTarget())
		{
			TransitionToState(EntityState.Chasing);
		}
	}

	protected override void HandleChasingTransitions()
	{
		if (!CanSeeTarget())
		{
			// Lost target, return to idle after a moment
			if (_stateTimer > 2f)
				TransitionToState(EntityState.Idle);
			return;
		}
	}

	protected override void ChaseTarget()
	{
		// Dummy does not chase but follows with eyes
		if (_target != null && IsInstanceValid(_target))
		{
			Vector2 direction = GlobalPosition.DirectionTo(_target.GlobalPosition);
			string animationName = "look_";

			// if player is to the left of dummy
			// considering flip
			if ((direction.X < 0 && !FlipSpriteHorizontally) || (direction.X > 0 && FlipSpriteHorizontally))
			{
				_facingDirection = Vector2.Left;
				animationName += "left";
			}
			else
			{
				_facingDirection = Vector2.Right;
				animationName += "right";
			}

			if(direction.Y <= 0)
			{
				animationName += "_up";
			}
			else
			{
				animationName += "_down";
			}

			// Play the appropriate animation
			if (_sprite.SpriteFrames.HasAnimation(animationName))
			{
				_sprite.Play(animationName);
			}
		}
		return;
	}

	protected override void OnEnterState(EntityState state)
	{
		switch (state)
		{
			case EntityState.Chasing:
				if (_target != null)
					_lastKnownTargetPosition = _target.GlobalPosition;
				break;

			case EntityState.Hit:
				_sprite.Play(GetAnimationForState(state));
				break;
		}
	}

	protected override void GenerateWanderDirection()
	{
		return;
	}

	protected override void PerformAttack()
	{
		return;
	}
}
