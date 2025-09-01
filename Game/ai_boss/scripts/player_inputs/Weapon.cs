using Godot;
using System;
using System.Collections.Generic;

public partial class Weapon : Node2D
{
	// ---- Node References ----
	public AnimatedSprite2D _anim;
	public Node2D OwnerCharacter { get; private set; }
	public Area2D _hitArea { get; set; } // Hit area for the weapon
	public CollisionPolygon2D _hitAreaShape { get; set; } // Hit area shape for the weapon

	//---- Signals ----
	[Signal] public delegate void AttackStartedEventHandler(string attackName); // Emitted when an attack starts
	[Signal] public delegate void EntityHitEventHandler(Node2D entity, int damage); // Emitted when an entity is hit
	[Signal] public delegate void AttackEndedEventHandler(string attackName); // Emitted when an attack ends

	// Signals for equipping/unequipping
	[Signal] public delegate void EquippedEventHandler();
	[Signal] public delegate void UnequippedEventHandler();

	// Signals for charging
    [Signal] public delegate void ChargeStartedEventHandler(string attackName);
    [Signal] public delegate void ChargeUpdatedEventHandler(float chargeLevel);
    [Signal] public delegate void ChargeReleasedEventHandler(string attackName);
    [Signal] public delegate void ChargeCancelledEventHandler(string attackName);


	//---- Non-Mechanical Properties ----
	[Export] public string WeaponName = "Weapon";
	[Export] public string Description = "A basic weapon.";

	//---- Attack Configuration ----
	[Export] public AttackBase LightAttackConfig;
	[Export] public AttackBase HeavyAttackConfig;

	// ----- States -----
	public enum WeaponState { Ready, Windup, Active }
	public WeaponState _state = WeaponState.Ready;
	// Track which attack is currently in progress (used by OpenHitWindow and CloseHitWindow)
	public bool _isCurrentAttackHeavy;
	// Stores aim when the attack button was pressed (keeps damage decoupled from mouse movement during animation)
	protected Vector2 _pendingHitTarget = Vector2.Zero;
	// Track already hit bodies during the current Active window
	protected HashSet<Node> _alreadyHit = new HashSet<Node>();
	// facing direction of the mouse relative to the player
	protected bool _facingLeft = false;
	private IChargeable _currentChargingAttack;
    private bool _isCharging = false;

	// ---- Timers ----
	protected float _lightCooldownTimer = 0f; // Cooldown timer for light attacks
	protected float _heavyCooldownTimer = 0f; // Cooldown timer for heavy attacks

	// ---- Damage Application Settings ----
	// If false, only the signal will be emitted and other systems should subscribe.
	[Export] public bool AutoApplyDamage = false;
	[Export] public string EnemyDamageMethodName = "ApplyDamage"; // name of method to call on enemies (if AutoApplyDamage)


	public override void _Ready()
	{
		base._Ready();

		_anim = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_hitArea = GetNodeOrNull<Area2D>("HitArea");
		_hitAreaShape = GetNodeOrNull<CollisionPolygon2D>("HitArea/CollisionPolygon2D");

		if (_anim == null)
		{
			GD.PrintErr("Sword: could not find AnimatedSprite2D node 'AnimatedSprite2D'");
			return;
		}

		if (_hitArea == null)
		{
			GD.PrintErr("Sword: could not find Area2D node 'HitArea'");
			return;
		}

		if (_hitAreaShape == null)
		{
			GD.PrintErr("Sword: could not find CollisionPolygon2D node 'CollisionPolygon2D'");
			return;
		}

		if (_hitArea != null)
		{
			// Disable monitoring by default; only enabled during Active window
			_hitArea.Monitoring = false;
			_hitArea.BodyEntered += OnBodyEntered;
		}

		// show hitboxes debug
		GetTree().SetDebugCollisionsHint(true);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_lightCooldownTimer > 0)
			_lightCooldownTimer = Math.Max(0, _lightCooldownTimer - (float)delta);

		if (_heavyCooldownTimer > 0)
			_heavyCooldownTimer = Math.Max(0, _heavyCooldownTimer - (float)delta);

		// Get mouse position for weapon rotation/facing
		Vector2 mousePos = GetGlobalMousePosition();

		// Calculate direction from sprite center to mouse
		Vector2 direction = mousePos - GlobalPosition;
		if (direction.LengthSquared() <= 0.000001f) direction = Vector2.Right;

		_facingLeft = direction.X < 0;

		_anim.FlipH = _facingLeft;

		if (_facingLeft)
			// Adjust rotation to face left
			_anim.Rotation = direction.Angle() + Mathf.Pi;
		else
			// Set rotation to face the mouse
			_anim.Rotation = direction.Angle();

		// Update charge state
		if (_isCharging)
		{
			UpdateCharge((float)delta);
		}
	}

	public virtual void Equip(Node2D owner)
	{
		// Logic for equipping the weapon
		OwnerCharacter = owner;
		EmitSignal(nameof(Equipped));
	}

	public virtual void Unequip()
	{
		// Logic for unequipping the weapon
		OwnerCharacter = null;
		EmitSignal(nameof(Unequipped));
	}

	// -------------------------
	// Public attack API 
	// They set up aim, play animation and set state to windup.
	// -------------------------

	public  void AttackLight(Vector2 mouseGlobalPos)
	{
		Attack(mouseGlobalPos, false);
	}

	public  void AttackHeavy(Vector2 mouseGlobalPos)
	{
		Attack(mouseGlobalPos, true);
	}
	
	// Internal attack method
	private void Attack(Vector2 mouseGlobalPos, bool isHeavy)
	{
		GD.Print($"Attack called: isHeavy={isHeavy}, weapon state={_state}, lightCooldown={_lightCooldownTimer}, heavyCooldown={_heavyCooldownTimer}");

		if (!CanStartAttack(isHeavy))
		{
			GD.Print($"Attack blocked by CanStartAttack: weapon state={_state}, lightCooldown={_lightCooldownTimer}, heavyCooldown={_heavyCooldownTimer}");
			return;
		}

		_pendingHitTarget = mouseGlobalPos; // Store the target position for the attack
		_isCurrentAttackHeavy = isHeavy;
		_ = StartAttackSequence(isHeavy);
	}
	
	// Check if the attack can be started
	// By checking the current state and cooldown timers
	public bool CanStartAttack(bool isHeavy)
	{
		GD.Print($"CanStartAttack check: isHeavy={isHeavy}, state={_state}, lightCooldown={_lightCooldownTimer}, heavyCooldown={_heavyCooldownTimer}");

		if (_state != WeaponState.Ready)
		{
			GD.Print($"Attack blocked: weapon state is {_state}, not Ready");
			return false;
		}
		if (!isHeavy && _lightCooldownTimer > 0f)
		{
			GD.Print($"Light attack blocked: cooldown timer {_lightCooldownTimer}");
			return false;
		}
		if (isHeavy && _heavyCooldownTimer > 0f)
		{
			GD.Print($"Heavy attack blocked: cooldown timer {_heavyCooldownTimer}");
			return false;
		}

		GD.Print("Attack allowed");
		return true;
	}
	
	// Master sequence control (windup -> rely on animation call -> idle)
	protected  async System.Threading.Tasks.Task StartAttackSequence(bool isHeavyAttack)
	{
		// set cooldown immediately so player can't spam
		if (!isHeavyAttack) _lightCooldownTimer = LightAttackConfig.Cooldown;
		else _heavyCooldownTimer = HeavyAttackConfig.Cooldown;

		_state = WeaponState.Windup;
		EmitSignal(nameof(AttackStarted), isHeavyAttack ? "heavy" : "light");

		// Play corresponding animation on the weapon's AnimationPlayer (animations must exist)
		if (_anim != null)
		{
			if (isHeavyAttack)
				_anim.Play("heavy_attack");
			else if (!isHeavyAttack)
				_anim.Play("light_attack");
		}

		// Fallback: if animation doesn't call OpenHitWindow, we open it after windup time.
		float windup = isHeavyAttack ? HeavyAttackConfig.Windup : LightAttackConfig.Windup;
		await ToSignal(GetTree().CreateTimer(windup), "timeout");

		// If animation already opened the window and changed state, don't forcibly open again.
		if (_state == WeaponState.Windup)
		{
			OpenHitWindow(isHeavyAttack); // string needed for AnimationPlayer compatibility (call from code too)
		}
	}

	// ---- Charging Logic ----
	public bool HasChargeableAttack(bool isHeavy)
	{
		var attackType = isHeavy ? HeavyAttackConfig : LightAttackConfig;
		return attackType is IChargeable;
	}

	// Start charging an attack
    public void StartCharge(Vector2 mouseGlobalPos, bool isHeavy)
    {
        if (_state != WeaponState.Ready) return;
        
        var config = isHeavy ? HeavyAttackConfig : LightAttackConfig;
        if (config is not ChargedAttack chargedAttack) return;

        _pendingHitTarget = mouseGlobalPos;
        _isCurrentAttackHeavy = isHeavy;
        _currentChargingAttack = chargedAttack;
        _isCharging = true;
        _state = WeaponState.Windup; // Use Windup state for charging

        // Start the charging process
        chargedAttack.StartCharging(this);
        EmitSignal(nameof(ChargeStarted), isHeavy ? "heavy" : "light");
    }

	// Update the charging process (called from player controller)
    public void UpdateCharge(float delta)
    {
        if (!_isCharging || _currentChargingAttack == null) return;
        
        _currentChargingAttack.UpdateCharge(this, delta);
        
        // Emit charge level updates for UI/feedback
        float chargeLevel = _currentChargingAttack.getCurrentChargeTime() / _currentChargingAttack.getMaxChargeTime();
        EmitSignal(nameof(ChargeUpdated), chargeLevel);
    }

	// Check if the charge can be released
    public bool CanReleaseCharge()
    {
        return _isCharging && _currentChargingAttack?.CanReleaseCharge() == true;
    }

	// Execute the charged attack
    public void ExecuteChargedLight(Vector2 mouseGlobalPos)
    {
		if (!_isCharging || _isCurrentAttackHeavy)
		{
			GD.Print("Charge not released!");
			return;
		}
		
        ExecuteChargedAttack(mouseGlobalPos);
    }

    public void ExecuteChargedHeavy(Vector2 mouseGlobalPos)
    {
        if (!_isCharging || !_isCurrentAttackHeavy) return;
        ExecuteChargedAttack(mouseGlobalPos);
    }

	private void ExecuteChargedAttack(Vector2 mouseGlobalPos)
    {
		if (_currentChargingAttack == null)
		{
			GD.PrintErr("No current charging attack to execute!");
			return;
		}

        _pendingHitTarget = mouseGlobalPos; // Update target in case mouse moved
        
        // Set cooldown
        if (_isCurrentAttackHeavy) 
            _heavyCooldownTimer = HeavyAttackConfig.Cooldown;
        else 
            _lightCooldownTimer = LightAttackConfig.Cooldown;

        // Emit signals
        EmitSignal(nameof(AttackStarted), _isCurrentAttackHeavy ? "heavy" : "light");
        EmitSignal(nameof(ChargeReleased), _isCurrentAttackHeavy ? "heavy" : "light");

        // Execute the charged attack
        bool facingAtStart = _facingLeft;

        GD.Print($"[Weapon]Executing charged attack: isHeavy={_isCurrentAttackHeavy}, target={_pendingHitTarget}, facingLeft={facingAtStart}");
        _currentChargingAttack.Execute(this, _pendingHitTarget, facingAtStart);
        
        // Clean up charging state
        _isCharging = false;
        _currentChargingAttack = null;
        
        // The attack execution will handle state transitions
    }

	// Cancel the current charge
    public void CancelCharge()
    {
        if (!_isCharging) return;
        
        string attackName = _isCurrentAttackHeavy ? "heavy" : "light";
        
        if (_currentChargingAttack != null)
        {
            _currentChargingAttack.Interrupt(this);
        }
        
        EmitSignal(nameof(ChargeCancelled), attackName);
        
        _isCharging = false;
        _currentChargingAttack = null;
        _state = WeaponState.Ready;
    }

	// -------------------------
	// AnimationPlayer Call Method targets
	// - OpenHitWindow(true/false)  // call on the exact frame the weapon visually hits
	// - CloseHitWindow(true/false) // optional: call at the end of active frames to stop hits early
	// The code will also proceed with fallback timings if these are not called.
	// -------------------------

	// Start the hit window
	public void OpenHitWindow(bool isHeavy)
	{
		if (_state == WeaponState.Active)
			return; // already open

		_state = WeaponState.Active;
		_isCurrentAttackHeavy = isHeavy;
		_alreadyHit.Clear(); // Clear the list of already hit targets if it wasn't already cleared

		bool facingAtStart = _facingLeft;

		if (isHeavy)
		{
			if (HeavyAttackConfig != null)
				HeavyAttackConfig.Execute(this, _pendingHitTarget, facingAtStart);
		}
		else
		{
			if (LightAttackConfig != null)
				LightAttackConfig.Execute(this, _pendingHitTarget, facingAtStart);
		}

		// schedule end of active window if animation didn't call CloseHitWindow
		float activeDuration = isHeavy ? HeavyAttackConfig.Active : LightAttackConfig.Active;
		_ = AutoInterrupt(activeDuration, isHeavy);
	}

	// Auto-close the hit window after a delay
	protected  async System.Threading.Tasks.Task AutoInterrupt(float secs, bool isHeavy)
	{
		await ToSignal(GetTree().CreateTimer(secs), "timeout");
		// Only close if still active for this attack kind
		if (_state == WeaponState.Active && _isCurrentAttackHeavy == isHeavy)
		{
			if(isHeavy)
				HeavyAttackConfig.Interrupt(this);
			else
				LightAttackConfig.Interrupt(this);
		}
	}

	public  void CloseHitWindow(bool isHeavy)
	{
		GD.Print($"WeaponBase.CloseHitWindow called: isHeavy={isHeavy}");
		ResetWeaponState(isHeavy);
	}

	// Reset the weapon state and hit detection
	public  void ResetWeaponState(bool isHeavy)
	{
		GD.Print($"WeaponBase.ResetWeaponState called: isHeavy={isHeavy}, current state={_state}");
		_state = WeaponState.Ready;
		_pendingHitTarget = Vector2.Zero;

		// Reset list of already hit targets 
		_alreadyHit.Clear();

		GD.Print($"About to emit AttackEnded signal for {(isHeavy ? "heavy" : "light")} attack");
		EmitSignal(nameof(AttackEnded), isHeavy ? "heavy" : "light");
		GD.Print($"AttackEnded signal emitted, weapon state is now {_state}");
	}

	// -------------------------
	// Collision handling
	// -------------------------
	private void OnBodyEntered(Node body)
	{
		// Called when a physics body enters the hit area while Monitoring=true.
		// We only act during Active state.
		if (_state != WeaponState.Active) return;
		if (body == null) return;
		if (_alreadyHit.Contains(body)) return;

		_alreadyHit.Add(body);

		float damage = _isCurrentAttackHeavy ? HeavyAttackConfig.Damage : LightAttackConfig.Damage;

		// Emit signal so other systems (ui, sfx, particles) can respond
		if (body is Node2D node2d)
			EmitSignal(nameof(EntityHit), node2d, damage);

		// Optionally auto-apply damage directly.
		if (AutoApplyDamage)
		{
			// Attempt to call configured method
			if (body.HasMethod(EnemyDamageMethodName))
			{
				body.Call(EnemyDamageMethodName, damage);
			}
			else
			{
				// fallback: try common names
				if (body.HasMethod("ApplyDamage")) body.Call("ApplyDamage", damage);
				else if (body.HasMethod("TakeDamage")) body.Call("TakeDamage", damage);
				else GD.Print($"Weapon: hit {body.Name} for {damage}, but no damage method found.");
			}
		}
	}
}
