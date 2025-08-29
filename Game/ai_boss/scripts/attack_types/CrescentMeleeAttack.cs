using Godot;
using System;

[GlobalClass]
public partial class CrescentMeleeAttack : AttackBase
{
    // Attack hitbox
    [ExportGroup("Crescent Properties")]
    [Export] public float InnerRadius = 18f;
    [Export] public float OuterRadius = 28f;
    [Export] public float AngleDeg = 70f; // Angle of the attack arc
    [Export] public float ArcCenterOffsetDeg = 0; // Center offset for  attack arc


    public override void Execute(Weapon weapon, Vector2 target, bool facingLeft)
    {

        Vector2 originLocal = Vector2.Zero;

        // use the saved aim direction (global) captured when attack started
        Vector2 originGlobal = weapon.GlobalPosition;
        Vector2 dirGlobal = target - originGlobal;

        // If the direction is too small, use a default direction
        if (dirGlobal.LengthSquared() <= 0.000001f) dirGlobal = Vector2.Right;
        dirGlobal = dirGlobal.Normalized();

        float dirAngle = dirGlobal.Angle();
        float effectiveArcCenterOffsetDeg = facingLeft ? -ArcCenterOffsetDeg : ArcCenterOffsetDeg;

        float angleRad = Mathf.DegToRad(AngleDeg);
        float centerAngle = dirAngle + Mathf.DegToRad(effectiveArcCenterOffsetDeg);

        // sector range
        float half = angleRad * 0.5f;
        float startAngle = centerAngle - half;
        float endAngle = centerAngle + half;

        if (weapon._hitArea != null) weapon._hitArea.Monitoring = true;
        GenerateHitBox(weapon, originLocal, startAngle, endAngle, facingLeft);
    
    }

    public override void Interrupt(Weapon weapon)
    {
        weapon._hitArea.Monitoring = false;
        weapon._hitAreaShape.Polygon = [];
        weapon.CloseHitWindow(false);

    }

    protected virtual void GenerateHitBox(Weapon weapon, Vector2 originLocal, float startAngle, float endAngle, bool facingLeft)
    {
        Vector2[] poly = BuildCrescentPolygon(originLocal, InnerRadius, OuterRadius, startAngle, endAngle, segments: Mathf.Max(6, (int)(AngleDeg / 5f)));
        weapon._hitAreaShape.Polygon = poly;
    }

    // Returns a Vector2[] polygon representing a ring-sector (crescent).
    // segments: number of points on the outer arc; inner arc will use the same count.
    protected Vector2[] BuildCrescentPolygon(
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
}