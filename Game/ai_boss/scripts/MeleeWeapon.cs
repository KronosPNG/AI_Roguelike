using Godot;
using System;

public partial class MeleeWeapon : WeaponBase
{

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
    
    //---- Node References ----
    protected Area2D _hitArea; // Hit area for the weapon
    protected CollisionPolygon2D _hitAreaShape; // Hit area shape for the weapon

    public override void _Ready()
    {   
        base._Ready();

        _hitArea = GetNodeOrNull<Area2D>("HitArea");
        _hitAreaShape = GetNodeOrNull<CollisionPolygon2D>("HitArea/CollisionPolygon2D");

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

    // -------------------------
    // Public attack API 
    // They set up aim, play animation and set state to windup.
    // -------------------------

    public override void AttackLight(Vector2 mouseGlobalPos)
    {
        Attack(mouseGlobalPos, false);
    }

    public override void AttackHeavy(Vector2 mouseGlobalPos)
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

    // -------------------------
    // AnimationPlayer Call Method targets
    // - OpenHitWindow(true/false)  // call on the exact frame the weapon visually hits
    // - CloseHitWindow(true/false) // optional: call at the end of active frames to stop hits early
    // The code will also proceed with fallback timings if these are not called.
    // -------------------------

    // Start the hit window
    public override void OpenHitWindow(bool isHeavy)
    {
        if (_state == WeaponState.Active)
            return; // already open
        
        _state = WeaponState.Active;
        _isCurrentAttackHeavy = isHeavy;
        _alreadyHit.Clear(); // Clear the list of already hit targets if it wasn't already cleared

        bool facingAtStart = _facingLeft;

        // Build the crescent collision polygon local to HitArea
        BuildAndApplyCrescent(isHeavy, facingAtStart);

        // enable monitoring so physics bodies entering will raise BodyEntered
        if (_hitArea != null) _hitArea.Monitoring = true;

        // schedule end of active window if animation didn't call CloseHitWindow
        float activeDuration = isHeavy ? HeavyActive : LightActive;
        _ = AutoCloseHitWindowAfter(activeDuration, isHeavy);
    }

    // Close the hit window
    public override void CloseHitWindow(bool isHeavy)
    {
        GD.Print($"CloseHitWindow called: isHeavy={isHeavy}, currentState={_state}");

        // disable area monitoring
        if (_hitArea != null)
        {
            _hitArea.Monitoring = false;

            // stop any sweep immediately
            _isSweeping = false;

            // clear polygon so it won't collide afterwards
            _hitAreaShape.Polygon = new Vector2[0];
        }
        GD.Print("MeleeWeapon cleanup done, calling base.CloseHitWindow");

        base.CloseHitWindow(isHeavy);
        GD.Print("base.CloseHitWindow completed");
    }

    // -------------------------
    // Crescent polygon builder for CollisionPolygon2D
    // -------------------------
    private void BuildAndApplyCrescent(bool isHeavy, bool facingLeftAtStart)
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

        float effectiveArcCenterOffsetDeg = facingLeftAtStart ? -arcCenterOffsetDeg : arcCenterOffsetDeg;

        float angleRad = Mathf.DegToRad(angleDeg);
        float centerAngle = dirAngle + Mathf.DegToRad(effectiveArcCenterOffsetDeg);

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

        bool effectiveSweepFromStart = sweepFromStart ^ facingLeftAtStart;

        // If sweep enabled, start asynchronous sweep task (cancellable)
        _ = SweepCrescent(originLocal, innerR, outerR, startAngle, endAngle, angleDeg, effectiveSweepFromStart, sweepStepDeg, sweepStepDelay);
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