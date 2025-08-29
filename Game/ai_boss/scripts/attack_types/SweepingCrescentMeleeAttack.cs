using Godot;
using System;
using static Weapon;

[GlobalClass]
public partial class SweepingCrescentMeleeAttack : CrescentMeleeAttack
{
    // Sweep control (make the crescent appear incrementally)
    [ExportGroup("Sweep Properties")]
    [Export] public bool SweepFromStartEdge = true; // true: grow start->end; false: grow end->start
    [Export] public float SweepStepDeg = 6f; // Step angle for attack sweep
    [Export] public float SweepStepDelay = 0.016f; // Delay between steps for attack sweep
    private bool _isSweeping = false;

    public override void Execute(Weapon weapon, Vector2 target, bool facingLeft)
    {
        base.Execute(weapon, target, facingLeft);
    }

    public override void Interrupt(Weapon weapon)
    {
        base.Interrupt(weapon);
        _isSweeping = false;
    }

    // Normalize end angle to be greater than start angle
    private float NormalizeEndGreater(float start, float end)
    {
        // make sure end >= start (work in continuous angle domain)
        while (end < start) end += Mathf.Tau; // TAU == 2π
        return end;
    }

    protected override void GenerateHitBox(Weapon weapon, Vector2 originLocal, float startAngle, float endAngle, bool facingLeft)
    {
        bool effectiveSweepFromStart = SweepFromStartEdge ^ facingLeft;

        _ = SweepCrescent(weapon, originLocal, startAngle, endAngle, effectiveSweepFromStart);
    }

    // Creates a "growing" ring sector to simulate sweeping motion
    private async System.Threading.Tasks.Task SweepCrescent(
        Weapon weapon,
        Vector2 originLocal,
        float startAngle,
        float endAngle,
        bool sweepFromStart)
    {
        // Cancel any previous sweep
        _isSweeping = true;

        // Normalize so endAngle >= startAngle (useful for sweeping across 0/2π)
        endAngle = NormalizeEndGreater(startAngle, endAngle);

        // Determine step in radians
        float stepRad = Mathf.DegToRad(Mathf.Max(1f, SweepStepDeg));
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
            Vector2[] poly = BuildCrescentPolygon(originLocal, InnerRadius, OuterRadius, currentStart, currentEnd, segments: Mathf.Max(6, (int)(AngleDeg / 5f)));
            weapon._hitAreaShape.Polygon = poly;

            // if we've reached final values, stop
            bool done = Mathf.Abs(currentEnd - targetEnd) < 0.0001f && Mathf.Abs(currentStart - targetStart) < 0.0001f;
            if (done) break;

            // wait for next step (exit early if hit window closed)
            await ToSignal(weapon.GetTree().CreateTimer(SweepStepDelay), "timeout");
            if ((Weapon.WeaponState)weapon._state != Weapon.WeaponState.Active || weapon._hitArea == null || !weapon._hitArea.Monitoring) break;
        }

        // Ensure final polygon applied (in case we exited early)
        if (_isSweeping)
        {
            Vector2[] finalPoly = BuildCrescentPolygon(originLocal, InnerRadius, OuterRadius, targetStart, targetEnd, segments: Mathf.Max(6, (int)(AngleDeg / 5f)));
            weapon._hitAreaShape.Polygon = finalPoly;
        }

        _isSweeping = false;
    }
}