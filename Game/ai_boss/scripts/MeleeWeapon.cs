using Godot;
using System;
using System.Collections.Generic;

public partial class MeleeWeapon : Node2D
{
    //---- Signals ----
    [Signal] public delegate void AttackStartedEventHandler(string attackName); // Emitted when an attack starts
    [Signal] public delegate void EntityHitEventHandler(Node2D entity, int damage); // Emitted when an entity is hit
    [Signal] public delegate void AttackEndedEventHandler(string attackName); // Emitted when an attack ends

    //---- Non-Mechanical Properties ----
    [Export] protected string WeaponName = "Weapon";
    [Export] protected string Description = "A basic melee weapon.";

    //---- Mechanical Properties ----
    [Export] public float LightCooldown = 0f; // Cooldown time for light attacks
    [Export] public float HeavyCooldown = 0f; // Cooldown time for heavy attacks

    [Export] public float LightWindup = 0f; // Delay before active frames
    [Export] public float LightActive = 0.25f; // How long the hitbox is active

    [Export] public float HeavyWindup = .1f;
    [Export] public float HeavyActive = .75f;

    [Export] public float LightDamage = 0f; // Damage dealt by light attacks
    [Export] public float HeavyDamage = 0f;

    // Hitbox properties
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

    // Sweep control (make the crescent appear incrementally)
    [Export] public bool LightSweepEnabled = true;
    [Export] public bool LightSweepFromStartEdge = true; // true: grow start->end; false: grow end->start
    [Export] public float LightSweepStepDeg = 6f; // Step angle for light attack sweep
    [Export] public float LightSweepStepDelay = 0.016f; // Delay between steps for light attack sweep

    [Export] public bool HeavySweepEnabled = true;
    [Export] public bool HeavySweepFromStartEdge = true;
    [Export] public float HeavySweepStepDeg = 6f;
    [Export] public float HeavySweepStepDelay = 0.016f;
    protected bool _isSweeping = false;

    // If false, only the signal will be emitted and other systems should subscribe.
    [Export] public bool AutoApplyDamage = false;
    [Export] public string EnemyDamageMethodName = "ApplyDamage"; // name of method to call on enemies (if AutoApplyDamage)

    // ----- States -----
    protected enum WeaponState { Ready, Windup, Active }
    protected WeaponState _state = WeaponState.Ready;
    // Track which attack is currently in progress (used by OpenHitWindow and CloseHitWindow)
    protected bool _isCurrentAttackHeavy;

    protected float _lightCooldownTimer = 0f; // Cooldown timer for light attacks
    protected float _heavyCooldownTimer = 0f; // Cooldown timer for heavy attacks

    protected AnimatedSprite2D _anim; 
    protected Area2D _hitArea; // Hit area for the weapon
    protected CollisionPolygon2D _hitAreaShape; // Hit area shape for the weapon

    // Stores aim when the attack button was pressed (keeps damage decoupled from mouse movement during animation)
    protected Vector2 _pendingHitTarget = Vector2.Zero;

    // Track already hit bodies during the current Active window
    protected HashSet<Node> _alreadyHit = new HashSet<Node>();

    public override void _Ready()
    {
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
            _lightCooldownTimer = Math.Min(0, _lightCooldownTimer - (float)delta);

        if (_heavyCooldownTimer > 0)
            _heavyCooldownTimer = Math.Min(0, _heavyCooldownTimer - (float)delta);

        // read mouse clicks (for testing - replace with Bean input handler calls)
		if (Input.IsActionJustPressed("light_attack"))
		{
			AttackLight(GetGlobalMousePosition());
		}
		else if (Input.IsActionJustPressed("heavy_attack"))
		{
			AttackHeavy(GetGlobalMousePosition());
		}

        // also for testing
		// Get mouse position in global coordinates
        Vector2 mousePos = GetGlobalMousePosition();
		
		// Calculate direction from sprite center to mouse
		Vector2 direction = mousePos - GlobalPosition;
		
		// Set rotation to face the mouse
		_anim.Rotation = direction.Angle();
    }

    // -------------------------
    // Public attack API 
    // They set up aim, play animation and set state to windup.
    // -------------------------

    public void AttackLight(Vector2 mouseGlobalPos)
    {
        Attack(mouseGlobalPos, false);
    }

    public void AttackHeavy(Vector2 mouseGlobalPos)
    {
        Attack(mouseGlobalPos, true);
    }

    // Internal attack method
    private void Attack(Vector2 mouseGlobalPos, bool isHeavy)
    {
        if (!CanStartAttack(isHeavy)) return;
        _pendingHitTarget = mouseGlobalPos; // Store the target position for the attack
        _isCurrentAttackHeavy = isHeavy; 
        _ = StartAttackSequence(isHeavy);

    }

    // Check if the attack can be started
    // By checking the current state and cooldown timers
    private bool CanStartAttack(bool isHeavy)
    {
        if (_state != WeaponState.Ready) return false;
        if (!isHeavy && _lightCooldownTimer > 0f) return false;
        if (isHeavy && _heavyCooldownTimer > 0f) return false;
        return true;
    }

    // Master sequence control (windup -> rely on animation call -> idle)
    private async System.Threading.Tasks.Task StartAttackSequence(bool isHeavyAttack)
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

    // -------------------------
    // AnimationPlayer Call Method targets
    // - OpenHitWindow(true/false)  // call on the exact frame the weapon visually hits
    // - CloseHitWindow(true/false) // optional: call at the end of active frames to stop hits early
    // The code will also proceed with fallback timings if these are not called.
    // -------------------------

    // Start the hit window
    public void OpenHitWindow(bool isHeavy)
    {
        if (_state == WeaponState.Active) return; // already open
        _state = WeaponState.Active;
        _isCurrentAttackHeavy = isHeavy;
        _alreadyHit.Clear(); // Clear the list of already hit targets if it wasn't already cleared

        // Build the crescent collision polygon local to HitArea
        BuildAndApplyCrescent(isHeavy);

        // enable monitoring so physics bodies entering will raise BodyEntered
        if (_hitArea != null) _hitArea.Monitoring = true;

        // schedule end of active window if animation didn't call CloseHitWindow
        float activeDuration = isHeavy ? HeavyActive : LightActive;
        _ = AutoCloseHitWindowAfter(activeDuration, isHeavy);
    }

    // Auto-close the hit window after a delay
    private async System.Threading.Tasks.Task AutoCloseHitWindowAfter(float secs, bool isHeavy)
    {
        await ToSignal(GetTree().CreateTimer(secs), "timeout");
        // Only close if still active for this attack kind
        if (_state == WeaponState.Active && _isCurrentAttackHeavy == isHeavy)
            CloseHitWindow(isHeavy);
    }

    // Close the hit window
    public void CloseHitWindow(bool isHeavy)
    {
        // disable area monitoring
        if (_hitArea != null)
        {
            _hitArea.Monitoring = false;

            // stop any sweep immediately
            _isSweeping = false;

            // clear polygon so it won't collide afterwards
            _hitAreaShape.Polygon = new Vector2[0];
        }

        ResetWeaponState(isHeavy);

    }

    // Reset the weapon state and hit detection
    private void ResetWeaponState(bool isHeavy)
    {
        _state = WeaponState.Ready;
        _pendingHitTarget = Vector2.Zero;

        // Reset list of already hit targets 
        _alreadyHit.Clear();
        
        EmitSignal(nameof(AttackEnded), isHeavy ? "heavy" : "light");
    }

    // -------------------------
    // Crescent polygon builder for CollisionPolygon2D
    // -------------------------
    private void BuildAndApplyCrescent(bool isHeavy)
    {
        if (_hitArea == null || _hitAreaShape == null) return;

        // compute local origin of hit area (we keep HitArea at weapon origin for simplicity)
        Vector2 originLocal = Vector2.Zero;

        // use the saved aim direction (global) captured when attack started
        Vector2 originGlobal = GlobalPosition;
        Vector2 targetGlobal = _pendingHitTarget == Vector2.Zero ? GetGlobalMousePosition() : _pendingHitTarget;
        Vector2 dirGlobal = targetGlobal - originGlobal;

        // If the direction is too small, use a default direction
        if (dirGlobal.LengthSquared() <= 0.000001f) dirGlobal = Vector2.Right;
        dirGlobal = dirGlobal.Normalized();
        float dirAngle = dirGlobal.Angle();

        // Compute inner/outer radius and angle offsets
        float innerR, outerR, angleDeg, arcCenterOffsetDeg;

        bool sweepEnabled;
        bool sweepFromStart;
        float sweepStepDeg;
        float sweepStepDelay;

        // Set parameters based on attack type
        if (!isHeavy)
        {
            innerR = LightInnerRadius; outerR = LightOuterRadius; angleDeg = LightAngleDeg;
            arcCenterOffsetDeg = LightArcCenterOffsetDeg;

            sweepEnabled = LightSweepEnabled;
            sweepFromStart = LightSweepFromStartEdge;
            sweepStepDeg = LightSweepStepDeg;
            sweepStepDelay = LightSweepStepDelay;
        }
        else
        {
            innerR = HeavyInnerRadius; outerR = HeavyOuterRadius; angleDeg = HeavyAngleDeg;
            arcCenterOffsetDeg = HeavyArcCenterOffsetDeg;

            sweepEnabled = HeavySweepEnabled;
            sweepFromStart = HeavySweepFromStartEdge;
            sweepStepDeg = HeavySweepStepDeg;
            sweepStepDelay = HeavySweepStepDelay;
        }

        float angleRad = Mathf.DegToRad(angleDeg);
        float centerAngle = dirAngle + Mathf.DegToRad(arcCenterOffsetDeg);

        // sector range
        float half = angleRad * 0.5f;
        float startAngle = centerAngle - half;
        float endAngle = centerAngle + half;

        // If sweeping is disabled, just apply final polygon:
        if (!sweepEnabled)
        {
            Vector2[] poly = BuildCrescentPolygon(originLocal, innerR, outerR, startAngle, endAngle, segments: Mathf.Max(6, (int)(angleDeg / 5f)));
            _hitAreaShape.Polygon = poly;
            return;
        }

        // If sweep enabled, start asynchronous sweep task (cancellable)
        _ = SweepCrescent(originLocal, innerR, outerR, startAngle, endAngle, angleDeg, sweepFromStart, sweepStepDeg, sweepStepDelay);
    }

    // Normalize end angle to be greater than start angle
    private float NormalizeEndGreater(float start, float end)
    {
        // make sure end >= start (work in continuous angle domain)
        while (end < start) end += Mathf.Tau; // TAU == 2π
        return end;
    }
    
    // Returns a Vector2[] polygon representing a ring-sector (crescent).
    // segments: number of points on the outer arc; inner arc will use the same count.
    private Vector2[] BuildCrescentPolygon(
        Vector2 originLocal,
        float innerR, float outerR,
        float startAngle,
        float endAngle,
        int segments = 12)
    {
        var arr = new Vector2[segments * 2 + 2];
        int idx = 0;

        // outer arc from start -> end
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / (float)segments;
            float a = Mathf.Lerp(startAngle, endAngle, t);
            Vector2 p = originLocal + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outerR;
            arr[idx++] = p;
        }
        // inner arc from end -> start (reverse)
        for (int i = segments; i >= 0; i--)
        {
            float t = (float)i / (float)segments;
            float a = Mathf.Lerp(startAngle, endAngle, t);
            Vector2 p = originLocal + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * innerR;
            arr[idx++] = p;
        }

        return arr;
    }

    // Creates a "growing" ring sector to simulate sweeping motion
    private async System.Threading.Tasks.Task SweepCrescent(
        Vector2 originLocal,
        float innerR,
        float outerR,
        float startAngle,
        float endAngle,
        float angleDeg,
        bool sweepFromStart,
        float sweepStepDeg,
        float sweepStepDelay)
    {
        // Cancel any previous sweep
        _isSweeping = true;

        // Normalize so endAngle >= startAngle (useful for sweeping across 0/2π)
        endAngle = NormalizeEndGreater(startAngle, endAngle);

        // Determine step in radians
        float stepRad = Mathf.DegToRad(Mathf.Max(1f, sweepStepDeg));
        float currentStart, currentEnd;

        if (sweepFromStart)
        {
            // start from start edge
            currentStart = startAngle;
            currentEnd = startAngle;
        }
        else
        {   
            // start from opposite angle
            currentStart = endAngle;
            currentEnd = endAngle;
        }

        // If endAngle passes multiple full rotations, clamp target to difference
        float targetStart = startAngle;
        float targetEnd = endAngle;

        // iterate until we reach the full sector (guard against infinite loop)
        int maxIterations = Mathf.CeilToInt(Mathf.Abs(targetEnd - targetStart) / stepRad) + 10;
        int iter = 0;

        // Sweep the crescent shape over time
        while (_isSweeping && iter < maxIterations)
        {
            iter++;

            if (sweepFromStart)
            {
                // grow end toward targetEnd
                currentEnd += stepRad;
                if (currentEnd > targetEnd) currentEnd = targetEnd;
            }
            else
            {
                // shrink start toward targetStart (moving backward)
                currentStart -= stepRad;
                // ensure currentStart is numerically <= targetStart (because targetStart may be smaller)
                if (currentStart < targetStart) currentStart = targetStart;
            }

            // Build polygon for current sector
            Vector2[] poly = BuildCrescentPolygon(originLocal, innerR, outerR, currentStart, currentEnd, segments: Mathf.Max(6, (int)(angleDeg / 5f)));
            _hitAreaShape.Polygon = poly;

            // if we've reached final values, stop
            bool done = Mathf.Abs(currentEnd - targetEnd) < 0.0001f && Mathf.Abs(currentStart - targetStart) < 0.0001f;
            if (done) break;

            // wait for next step (exit early if hit window closed)
            await ToSignal(GetTree().CreateTimer(sweepStepDelay), "timeout");
            if (_state != WeaponState.Active || _hitArea == null || !_hitArea.Monitoring) break;
        }

        // Ensure final polygon applied (in case we exited early)
        if (_isSweeping)
        {
            Vector2[] finalPoly = BuildCrescentPolygon(originLocal, innerR, outerR, targetStart, targetEnd, segments: Mathf.Max(6, (int)(angleDeg / 5f)));
            _hitAreaShape.Polygon = finalPoly;
        }

        _isSweeping = false;
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

        float damage = _isCurrentAttackHeavy ? HeavyDamage : LightDamage;

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