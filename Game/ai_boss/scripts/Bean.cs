using Godot;
using System;

public partial class Bean : CharacterBody2D
{
	private AnimatedSprite2D _sprite;

	//---- Movement Data ----

	// dodging

	public float BaseSpeed { get; set; } = 500f; // normal speed
	public float SpeedModifier { get; set; } = 1f; // speed modifier
	public float DodgeSpeed { get; set; } = 1500f;    // speed during dodge
	public float DodgeDuration { get; set; } = 0.15f; // seconds
	private float DodgeCooldown { get; set; } = 1f; // seconds
	private double _lastDodgeTime = -999;
	private Vector2 _dodgeDirection = Vector2.Right;

	// direction leniency
	public float DodgeInputLeniency { get; set; } = 0.1f; // seconds to wait for input
	private double _dodgeInputTimer = 0;
	private bool _waitingForDodgeDirection = false;

	// facing direction
	private Vector2 _facing = Vector2.Right; // default facing direction
	private int _lastHorizontalFacing = 1; // 1 = right, -1 = left

	//---- Player State ----
	private enum PlayerState { Idle, Walking, DodgePrep, Dodge }
	private PlayerState _state = PlayerState.Idle;
	private PlayerState _prevState = PlayerState.Idle; // for detecting state changes

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("PlayerSprite");
		_sprite.AnimationFinished += OnAnimationFinished;
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
		Vector2 inputDir = Vector2.Zero;

		if (Input.IsActionPressed("move_right")) inputDir.X += 1;
		if (Input.IsActionPressed("move_left")) inputDir.X -= 1;
		if (Input.IsActionPressed("move_down")) inputDir.Y += 1;
		if (Input.IsActionPressed("move_up")) inputDir.Y -= 1;

		return inputDir.Normalized();
	}

	void UpdateFacing(Vector2 inputDir)
	{
		if (!Mathf.IsEqualApprox(inputDir.X, 0f))
			_facing = new Vector2(inputDir.X, 0); // force pure horizontal

		else if (!Mathf.IsEqualApprox(inputDir.Y, 0f))
			_facing = new Vector2(0, inputDir.Y); // force pure vertical

		else if (inputDir.Length() > 0)
			_facing = inputDir.Normalized();
			
		if (!Mathf.IsEqualApprox(_facing.X, 0))
			_lastHorizontalFacing = Mathf.Sign(_facing.X);
	}


	// --- State machine --------------------------
	private void HandleStateTransitions(Vector2 input)
	{
		switch (_state)
		{
			case PlayerState.Idle:
			case PlayerState.Walking:
				// Dodge starts on just-pressed regardless of whether there is movement
				if (Input.IsActionJustPressed("dodge") &&
					Time.GetTicksMsec() / 1000.0 - _lastDodgeTime >= DodgeCooldown)
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

				// Sample input now so simultaneous presses are respected
				Vector2 dodgeInput = ReadDirection();

				if (dodgeInput != Vector2.Zero || _dodgeInputTimer <= 0)
				{
					if (dodgeInput == Vector2.Zero)
						dodgeInput = _facing; // fallback to last facing

					if (dodgeInput == Vector2.Zero)
						dodgeInput = Vector2.Right;

					_dodgeDirection = dodgeInput.Normalized();
					_lastDodgeTime = Time.GetTicksMsec() / 1000.0; // update last dodge time
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
			case PlayerState.Dodge:
				// play dodge animation; movement will be handled in ApplyMovementByState
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
				// You can still call MoveAndSlide if needed for other physics interactions:
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
