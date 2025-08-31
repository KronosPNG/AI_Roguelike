using Godot;

[GlobalClass]
public partial class ProjectileAttack : AttackBase, IAttack, IShootable
{
    [ExportGroup("Projectile Properties")]
    [Export] public PackedScene ProjectileScene; // The projectile prefab/scene
    [Export] public float ProjectileSpeed = 300f;
    [Export] public float ProjectileLifetime = 5f; // How long the projectile lives
    [Export] public int ProjectileCount = 1; // Number of projectiles per attack
    [Export] public float SpreadAngleDeg = 0f; // Spread angle for multiple projectiles
    [Export] public Vector2 SpawnOffset = Vector2.Zero; // Offset from weapon position

    public override void Execute(Weapon weapon, Vector2 target, bool facingLeft)
    {
        if (ProjectileScene == null)
        {
            GD.PrintErr("ProjectileAttack: ProjectileScene is null!");
            return;
        }

        Vector2 originGlobal = weapon.GlobalPosition;
        Vector2 direction = (target - originGlobal).Normalized();

        // If direction is too small, use default
        if (direction.LengthSquared() <= 0.000001f)
            direction = facingLeft ? Vector2.Left : Vector2.Right;

        // Calculate spawn position with offset
        Vector2 effectiveOffset = facingLeft ? new Vector2(-SpawnOffset.X, SpawnOffset.Y) : SpawnOffset;
        Vector2 spawnPosition = originGlobal + effectiveOffset;

        // Spawn projectiles
        for (int i = 0; i < ProjectileCount; i++)
        {
            SpawnProjectile(weapon, spawnPosition, direction, i);
        }
    }

    public override void Interrupt(Weapon weapon)
    {
        weapon.CloseHitWindow(false);
    }

    public void SpawnProjectile(Weapon weapon, Vector2 spawnPosition, Vector2 baseDirection, int projectileIndex)
    {
        // Calculate spread angle for this projectile
        float spreadAngle = 0f;
        if (ProjectileCount > 1)
        {
            float totalSpread = Mathf.DegToRad(SpreadAngleDeg);
            float step = totalSpread / (ProjectileCount - 1);
            spreadAngle = -totalSpread * 0.5f + (step * projectileIndex);
        }

        // Apply spread to direction
        Vector2 projectileDirection = baseDirection.Rotated(spreadAngle);

        // Instance the projectile
        Node projectileInstance = ProjectileScene.Instantiate();
        
        if (projectileInstance is not Projectile projectile)
        {
            GD.PrintErr("ProjectileAttack: Instantiated scene is not a Projectile!");
            projectileInstance?.QueueFree();
            return;
        }

        // Add projectiles to scene tree
        weapon.GetTree().CurrentScene.AddChild(projectile);

        // Initialize the projectile
        projectile.Initialize(
            spawnPosition,
            projectileDirection,
            ProjectileSpeed,
            Damage,
            ProjectileLifetime,
            weapon.OwnerCharacter // Pass owner to avoid self-damage
        );
    }
}