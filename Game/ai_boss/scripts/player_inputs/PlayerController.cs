using Godot;
using System;

public partial class PlayerController : CharacterBody2D
{
	//---- Node References ----
	private AnimatedSprite2D _sprite;
	public Weapon _equippedWeapon;
	public PackedScene _equippedWeaponScene; // Store the PackedScene for weapon swapping
	private Node2D _handNode;

	//---- Character Data ----
	public byte Health { get; set; } = 100;
	private const byte MaxHealth = 100;
	public float DamageReduction { get; set; } = 0f;

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
	private enum PlayerState { Idle, Walking, DodgePrep, Dodge, Attacking, Charging }
	private PlayerState _state = PlayerState.Idle;
	private PlayerState _prevState = PlayerState.Idle; // for detecting state changes
	private bool _isChargingAttack = false;
	private bool _isHeavyCharge = false;


	public override void _Ready()
	{
		_sprite = GetNodeOrNull<AnimatedSprite2D>("PlayerSprite");
		if (_sprite == null)
		{
			GD.PrintErr("Bean: could not find AnimatedSprite2D node 'PlayerSprite'");
			return;
		}

		_handNode = GetNodeOrNull<Node2D>("Hand");
		if (_handNode == null)
		{
			GD.PrintErr("Bean: could not find Hand node");
			return;
		}

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

		// Handle combat input (only when not dodging)
		HandleCombatInput();

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

			case PlayerState.Attacking:
				// We leave attacking only when the weapon signals attack ended
				// This is handled by weapon signals connected in EquipWeapon
				break;

			case PlayerState.Charging:
				// Allow dodging while charging - this cancels the charge
				if (Input.IsActionJustPressed("dodge"))
				{
					GD.Print("Dodge input while charging - cancelling charge");
					_equippedWeapon.CancelCharge();
					_isChargingAttack = false;
					_dodgeInputTimer = DodgeInputLeniency;
					TransitionToState(PlayerState.DodgePrep);
				}
				break;
		}
	}

	// --- Combat Input Handling -----------------
	private void HandleCombatInput()
	{
		// Don't allow attacks while dodging, in dodge preparation, or already attacking
		if (_state == PlayerState.Dodge || _state == PlayerState.DodgePrep)
		{
			if (Input.IsActionJustPressed("light_attack") || Input.IsActionJustPressed("heavy_attack"))
			{
				GD.Print($"Attack input blocked - Player state: {_state}");
			}

			return;
		}

		// Handle charging state
		if (_state == PlayerState.Charging)
		{
			HandleChargingInput();
			return;
		}

		if (_state == PlayerState.Attacking)
			return;

		// Handle light attack input
		if (Input.IsActionJustPressed("light_attack"))
		{
			GD.Print("Light attack input received");
			if (!_equippedWeapon.CanStartAttack(false)) return;

			// Check if weapon has a chargeable light attack
			if (_equippedWeapon.HasChargeableAttack(false))
			{
				GD.Print("- Transitioning to Charging");
				StartChargingAttack(false);
			}
			else
			{
				GD.Print("- Transitioning to Attacking");
				TransitionToState(PlayerState.Attacking);
				OnLightAttack();
			}
		}
		// Handle heavy attack input  
		else if (Input.IsActionJustPressed("heavy_attack"))
		{
			GD.Print("Heavy attack input received");
			if (!_equippedWeapon.CanStartAttack(true)) return;
			;

			// Check if this weapon has a chargeable heavy attack
			if (_equippedWeapon.HasChargeableAttack(true))
			{
				GD.Print("- Transitioning to Charging");
				StartChargingAttack(true);
			}
			else
			{
				GD.Print("- Transitioning to Attacking");
				TransitionToState(PlayerState.Attacking);
				OnHeavyAttack();
			}
		}
	}

	private void HandleChargingInput()
	{
		bool lightPressed = Input.IsActionPressed("light_attack");
		bool heavyPressed = Input.IsActionPressed("heavy_attack");

		// Check if the relevant button is still held
		bool shouldContinueCharging = _isHeavyCharge ? heavyPressed : lightPressed;

		if (shouldContinueCharging)
		{
			// Continue charging - weapon handles the charging logic
			_equippedWeapon.UpdateCharge((float)GetProcessDeltaTime());
		}
		else
		{
			// Button released - execute or cancel the charged attack
			if (_equippedWeapon.CanReleaseCharge())
			{
				GD.Print("Charge released successfully");
				TransitionToState(PlayerState.Attacking);
				ExecuteChargedAttack(_isHeavyCharge);
			}
			else
			{
				// Charge was too short, cancel and return to appropriate state
				GD.Print("Charge too short, cancelling charge");

				_equippedWeapon.CancelCharge();
				Vector2 currentInput = ReadDirection();
				TransitionToState(currentInput.Length() > 0 ? PlayerState.Walking : PlayerState.Idle);
			}
		}
	}

	private void StartChargingAttack(bool isHeavy)
	{
		_isChargingAttack = true;
		_isHeavyCharge = isHeavy;
		TransitionToState(PlayerState.Charging);
		_equippedWeapon.StartCharge(GetGlobalMousePosition(), isHeavy);
	}

	private void ExecuteChargedAttack(bool isHeavy)
	{
		if (isHeavy)
			_equippedWeapon.ExecuteChargedHeavy(GetGlobalMousePosition());
		else
			_equippedWeapon.ExecuteChargedLight(GetGlobalMousePosition());
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
				GD.Print("Entering Dodge state");
				// Set the sprite's flip based on direction
				_sprite.FlipH = _dodgeDirection.X < 0 || _lastHorizontalFacing < 0;
				_sprite.Play("dodge");
				break;
			case PlayerState.Walking:
				GD.Print("Entering Walking state");
				// animation will be set in UpdateAnimationIfNeeded()
				break;
			case PlayerState.Idle:
				GD.Print("Entering Idle state");
				// animation set later
				break;
			case PlayerState.Attacking:
				GD.Print("Entering Attacking state");
				// animation will be triggered by weapon	
				break;
			case PlayerState.Charging:
				GD.Print("Entering Charging state");
				// animation will be triggered by weapon
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

			case PlayerState.Attacking:
			// allow movement while attacking (at reduced speed or full speed)
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

			case PlayerState.Charging:
				// allow movement while charging (at reduced speed or full speed)
				float chargeMoveModifier = 0.5f; // e.g., half speed while charging
				Velocity = ReadDirection() * BaseSpeed * chargeMoveModifier;
				MoveAndSlide();
				break;
		}
	}

	// --- Animation ------------------------------
	private void UpdateAnimationIfNeeded()
	{
		// Only update visuals when state changed OR we need to update facing while walking/idle
		bool stateChanged = _state != _prevState;

		// If we are in dodge or attacking, don't let other animations override
		if (_state == PlayerState.Dodge || _state == PlayerState.Attacking)
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
		else if (_state == PlayerState.Charging)
		{
			// While charging, play walking animation if moving, idle if stationary
			Vector2 currentInput = ReadDirection();
			_sprite.FlipH = _lastHorizontalFacing < 0;

			if (currentInput.Length() > 0)
			{
				if (_sprite.Animation != "walking")
					_sprite.Play("walking");
			}
			else
			{
				if (_sprite.Animation != "idle")
					_sprite.Play("idle");
			}
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

	public void EquipWeapon(PackedScene weaponScene)
	{
		// If we reach this point, we have a new weapon to equip
		if (_equippedWeapon != null)
		{
			// Disconnect signals
			_equippedWeapon.AttackStarted -= OnWeaponAttackStarted;
			_equippedWeapon.AttackEnded -= OnWeaponAttackEnded;

			_equippedWeapon.Unequip();
			_equippedWeapon.QueueFree();
			_equippedWeapon = null;
		}

		if (weaponScene == null)
		{
			_equippedWeaponScene = null;
			return;
		}

		// Store the PackedScene reference
		_equippedWeaponScene = weaponScene;

		var weaponInstance = weaponScene.Instantiate() as Weapon;
		if (weaponInstance == null) return;

		_handNode.AddChild(weaponInstance);

		CallDeferred(nameof(CallEquipDeferred), weaponInstance);
	}

	private void CallEquipDeferred(Weapon weapon)
	{
		weapon.Equip(this);
		_equippedWeapon = weapon;

		// Connect to weapon signals
		weapon.AttackStarted += OnWeaponAttackStarted;
		weapon.AttackEnded += OnWeaponAttackEnded;
	}

	private void OnLightAttack()
	{
		_equippedWeapon.AttackLight(GetGlobalMousePosition());
	}

	private void OnHeavyAttack()
	{
		_equippedWeapon.AttackHeavy(GetGlobalMousePosition());
	}

	// --- Weapon Signal Handlers ---------------
	private void OnWeaponAttackStarted(string attackName)
	{
		GD.Print($"Weapon attack started: {attackName}");
		// Weapon attack has started, ensure we're in attacking state
		if (_state != PlayerState.Attacking)
		{
			GD.Print($"Player not in attacking state ({_state}), transitioning now");
			TransitionToState(PlayerState.Attacking);
		}
	}

	private void OnWeaponAttackEnded(string attackName)
	{
		GD.Print($"Weapon attack ended: {attackName}, current player state: {_state}");

		// Weapon attack has ended, return to appropriate state
		if (_state == PlayerState.Attacking)
		{
			Vector2 currentInput = ReadDirection();
			if (currentInput.Length() > 0)
			{
				GD.Print("Transitioning from Attacking to Walking");
				TransitionToState(PlayerState.Walking);
			}

			else
			{
				GD.Print("Transitioning from Attacking to Idle");
				TransitionToState(PlayerState.Idle);
			}

		}

		else
		{
			GD.Print($"Player was not in Attacking state when weapon ended attack (state: {_state})");
		}
	}

	public PackedScene GetCurrentWeaponScene() => _equippedWeaponScene;
}
