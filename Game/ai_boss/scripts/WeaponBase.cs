using System;
using System.Collections.Generic;
using Godot;

public partial class WeaponBase : Node2D
{
    // ---- Node References ----
    protected AnimatedSprite2D _anim;
    public Node2D OwnerCharacter { get; private set; }

    //---- Signals ----
    [Signal] public delegate void AttackStartedEventHandler(string attackName); // Emitted when an attack starts
    [Signal] public delegate void EntityHitEventHandler(Node2D entity, int damage); // Emitted when an entity is hit
    [Signal] public delegate void AttackEndedEventHandler(string attackName); // Emitted when an attack ends
    [Signal] public delegate void EquippedEventHandler();
    [Signal] public delegate void UnequippedEventHandler();

    //---- Non-Mechanical Properties ----
    [Export] public string WeaponName = "Weapon";
    [Export] public string Description = "A basic weapon.";

    //---- Mechanical Properties ----
    [Export] public float LightCooldown = 0f; // Cooldown time for light attacks
    [Export] public float HeavyCooldown = 0f; // Cooldown time for heavy attacks

    [Export] public float LightWindup = 0f; // Delay before active frames
    [Export] public float LightActive = 0.25f; // How long the hitbox is active

    [Export] public float HeavyWindup = .1f;
    [Export] public float HeavyActive = .75f;

    [Export] public float LightDamage = 0f; // Damage dealt by light attacks
    [Export] public float HeavyDamage = 0f;

    protected float _lightCooldownTimer = 0f; // Cooldown timer for light attacks
    protected float _heavyCooldownTimer = 0f; // Cooldown timer for heavy attacks

    // -- Hitbox properties --
    // Light attack hitbox
    [Export] public float LightInnerRadius = 18f;
    [Export] public float LightOuterRadius = 28f;
    [Export] public float LightAngleDeg = 70f; // Angle of the light attack arc
    [Export] public float LightArcCenterOffsetDeg = 0; // Center offset for light attack arc

    // Heavy attack hitbox
    [Export] public float HeavyInnerRadius = 24f;
    [Export] public float HeavyOuterRadius = 44f;
    [Export] public float HeavyAngleDeg = 120f;
    [Export] public float HeavyArcCenterOffsetDeg = 0;

    // ----- States -----
    protected enum WeaponState { Ready, Windup, Active }
    protected WeaponState _state = WeaponState.Ready;
    // Track which attack is currently in progress (used by OpenHitWindow and CloseHitWindow)
    protected bool _isCurrentAttackHeavy;

    // ---- Damage Application Settings ----
    // If false, only the signal will be emitted and other systems should subscribe.
    [Export] public bool AutoApplyDamage = false;
    [Export] public string EnemyDamageMethodName = "ApplyDamage"; // name of method to call on enemies (if AutoApplyDamage)

    // Stores aim when the attack button was pressed (keeps damage decoupled from mouse movement during animation)
    protected Vector2 _pendingHitTarget = Vector2.Zero;

    // Track already hit bodies during the current Active window
    protected HashSet<Node> _alreadyHit = new HashSet<Node>();


    // facing direction of the mouse relative to the player
    protected bool _facingLeft = false;

    public override void _Ready()
    {
        _anim = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_anim == null)
        {
            GD.PrintErr($"{WeaponName}: could not find AnimatedSprite2D node 'AnimatedSprite2D'");
            return;
        }

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
    public virtual void AttackLight(Vector2 target)
    {
        // Logic for light attack
    }

    public virtual void AttackHeavy(Vector2 target)
    {
        // Logic for heavy attack
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
    protected virtual async System.Threading.Tasks.Task StartAttackSequence(bool isHeavyAttack)
    {
        // set cooldown immediately so player can't spam
        if (!isHeavyAttack) _lightCooldownTimer = LightCooldown;
        else _heavyCooldownTimer = HeavyCooldown;

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
        float windup = isHeavyAttack ? HeavyWindup : LightWindup;
        await ToSignal(GetTree().CreateTimer(windup), "timeout");

        // If animation already opened the window and changed state, don't forcibly open again.
        if (_state == WeaponState.Windup)
        {
            OpenHitWindow(isHeavyAttack); // string needed for AnimationPlayer compatibility (call from code too)
        }
    }

    // Start the hit window
    public virtual void OpenHitWindow(bool isHeavy)
    {
        if (_state == WeaponState.Active) return; // already open
        _state = WeaponState.Active;
        _isCurrentAttackHeavy = isHeavy;
        _alreadyHit.Clear();

        // schedule end of active window if animation didn't call CloseHitWindow
        float activeDuration = isHeavy ? HeavyActive : LightActive;
        _ = AutoCloseHitWindowAfter(activeDuration, isHeavy);
    }

    // Auto-close the hit window after a delay
    protected virtual async System.Threading.Tasks.Task AutoCloseHitWindowAfter(float secs, bool isHeavy)
    {
        await ToSignal(GetTree().CreateTimer(secs), "timeout");
        // Only close if still active for this attack kind
        if (_state == WeaponState.Active && _isCurrentAttackHeavy == isHeavy)
            CloseHitWindow(isHeavy);
    }

    public virtual void CloseHitWindow(bool isHeavy)
    {
        GD.Print($"WeaponBase.CloseHitWindow called: isHeavy={isHeavy}");
        ResetWeaponState(isHeavy);
    }

    // Reset the weapon state and hit detection
    protected virtual void ResetWeaponState(bool isHeavy)
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
}
