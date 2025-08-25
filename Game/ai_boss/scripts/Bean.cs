using Godot;
using System;

public partial class Bean : CharacterBody2D
{
	private AnimatedSprite2D _sprite;

	//---- Movement Data ----

	public float BaseSpeed { get; set; } = 500f; // normal speed
	public float SpeedModifier { get; set; } = 1f; // speed modifier
	public float DodgeSpeed { get; set; } = 1500f; // speed during dodge
	private Vector2 _dodgeDirection = Vector2.Zero;

	// direction leniency
	public float DodgeInputLeniency { get; set; } = 0.05f; // seconds to wait for input
	private double _dodgeInputTimer = 0; // timer for input leniency for dodging

	// facing direction
	private Vector2 _facing = Vector2.Right; // default facing direction
	private sbyte _lastHorizontalFacing = 1; // 1 = right, -1 = left

	//---- Player State ----
	private enum PlayerState { Idle, Walking, DodgePrep, Dodge }
	private PlayerState _state = PlayerState.Idle;
	private PlayerState _prevState = PlayerState.Idle; // for detecting state changes

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("PlayerSprite");
		_sprite.AnimationFinished += OnAnimationFinished; // Connect animation finished signal
	}

	public override void _PhysicsProcess(double delta)
	{
		// Read input direction
		Vector2 inputDir = ReadDirection();

		// Update facing direction
		UpdateFacing(inputDir);

		// State transitions (input-driven)
		HandleStateTransitions(inputDir);

		// Apply physics based on state (movement independent of animation)
		ApplyMovementByState(delta, inputDir);

		// Update animation if needed (state or facing changed)
		UpdateAnimationIfNeeded();
	}

	private Vector2 ReadDirection()
	{
		return Input.GetVector("move_left", "move_right", "move_up", "move_down"); // get already normalized direction vector
	}

	void UpdateFacing(Vector2 inputDir)
	{	
		_facing = inputDir;
			
		if (!Mathf.IsEqualApprox(_facing.X, 0))
			_lastHorizontalFacing = (sbyte)Mathf.Sign(_facing.X);
	}

	// Checks if any movement keys were just pressed
	private Vector2 MovementJustPressed()
	{
		if (Input.IsActionJustPressed("move_right")) return Vector2.Right;
		if (Input.IsActionJustPressed("move_left")) return Vector2.Left;
		if (Input.IsActionJustPressed("move_down")) return Vector2.Down;
		if (Input.IsActionJustPressed("move_up")) return Vector2.Up;
		return Vector2.Zero;

	}

	// --- State machine --------------------------
	private void HandleStateTransitions(Vector2 input)
	{
		switch (_state)
		{
			case PlayerState.Idle:
			case PlayerState.Walking:
				// Dodge starts on just-pressed regardless of whether there is movement
				if (Input.IsActionJustPressed("dodge"))
				{
					_dodgeInputTimer = DodgeInputLeniency;
					TransitionToState(PlayerState.DodgePrep);

				}
				else
				{
					// Normal movement -> walking vs idle
					if (input.Length() > 0)
						TransitionToState(PlayerState.Walking);
					else
						TransitionToState(PlayerState.Idle);
				}
				break;

			case PlayerState.DodgePrep:

				_dodgeInputTimer -= GetProcessDeltaTime();

				// If player pressed any movement key *this frame*, accept the (combined) held input immediately
				if (MovementJustPressed() != Vector2.Zero)
				{
					_dodgeDirection = ReadDirection();
					TransitionToState(PlayerState.Dodge);
					break;
				}

				 // otherwise, wait for leniency timer to expire and then fall back to held direction or facing
				if (_dodgeInputTimer <= 0)
				{
					// Sample input now so simultaneous presses are respected
					Vector2 dodgeInput = ReadDirection();

					if (dodgeInput == Vector2.Zero)
					{
						TransitionToState(PlayerState.Idle);
						break;
					}

					_dodgeDirection = dodgeInput;
					TransitionToState(PlayerState.Dodge);
				}
				break;

			case PlayerState.Dodge:
				// We leave dodge only when its animation finishes (handled in animation_finished)
				// so no transitions here. AnimationFinished -> EndDodge => sets Idle/Walking next.
				break;
		}
	}

	private void TransitionToState(PlayerState next)
	{
		if (_state == next) return;
		_prevState = _state;
		_state = next;
		OnEnterState(next);
	}

	private void OnEnterState(PlayerState s)
	{
		switch (s)
		{
			// play dodge animation; movement will be handled in ApplyMovementByState
			case PlayerState.Dodge:
				// Set the sprite's flip based on direction
				_sprite.FlipH = _dodgeDirection.X < 0 || _lastHorizontalFacing < 0;
				_sprite.Play("dodge");
				break;
			case PlayerState.Walking:
				// animation will be set in UpdateAnimationIfNeeded()
				break;
			case PlayerState.Idle:
				// animation set later
				break;
		}
	}

	// --- Physics & movement ---------------------
	private void ApplyMovementByState(double delta, Vector2 input)
	{
		switch (_state)
		{
			case PlayerState.Dodge:
				// move using dodge vector & speed
				Velocity = _dodgeDirection * DodgeSpeed;
				MoveAndSlide();
				break;

			case PlayerState.Walking:
				// move using input vector, speed and modifier
				Velocity = input * BaseSpeed * SpeedModifier;
				MoveAndSlide();
				break;

			case PlayerState.DodgePrep:
				// stop movement while preparing to dodge
				Velocity = Vector2.Zero;
				MoveAndSlide();
				break;

			case PlayerState.Idle:
				// stop movement
				Velocity = Vector2.Zero;
				MoveAndSlide();
				break;
		}
	}
	
	// --- Animation ------------------------------
	private void UpdateAnimationIfNeeded()
	{
		// Only update visuals when state changed OR we need to update facing while walking/idle
		bool stateChanged = _state != _prevState;

		// If we are in dodge, don't let other animations override; dodge animation will call OnAnimationFinished.
		if (_state == PlayerState.Dodge)
		{
			_prevState = _state;
			return;
		}

		// Determine animation for current state
		if (_state == PlayerState.Walking)
		{
			_sprite.FlipH = _lastHorizontalFacing < 0;
			if (stateChanged || _sprite.Animation != "walking")
				_sprite.Play("walking");
		}
		else if (_state == PlayerState.Idle)
		{
			// keep flip from last horizontal input
			if (stateChanged || _sprite.Animation != "idle")
				_sprite.Play("idle");
		}

		_prevState = _state;
	}

	// Called by AnimatedSprite2D when any animation completes
	private void OnAnimationFinished()
	{
		// Get the name of the finished animation
		var animName = _sprite.Animation;
		// If dodge finished, end dodge and transit to Idle/Walking based on current input
		if (animName == "dodge" && _state == PlayerState.Dodge)
		{
			// decide whether to be walking or idle after dodge
			Vector2 currentInput = ReadDirection();
			if (currentInput.Length() > 0)
				TransitionToState(PlayerState.Walking);
			else
				TransitionToState(PlayerState.Idle);
		}
	}
}
