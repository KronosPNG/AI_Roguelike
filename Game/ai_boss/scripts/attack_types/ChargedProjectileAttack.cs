using Godot;

[GlobalClass]
public partial class ChargedProjectileAttack : ChargedAttack, IAttack, IChargeable, IShootable
{
    [ExportGroup("Projectile Attack")]
    [Export] public ProjectileAttack ProjectileAttack { get; set; } // The projectile attack to use
    
    [ExportGroup("Charge-based Projectile Scaling")]
    [Export] public bool ScaleProjectileCountWithCharge = true;
    [Export] public int MaxProjectileCount = 5; // Maximum projectiles at full charge
    [Export] public bool ScaleSpeedWithCharge = true;
    [Export] public float MinProjectileSpeedMultiplier = 0.25f;
    [Export] public float MaxProjectileSpeedMultiplier = 1.0f;
    [Export] public bool ScaleProjectileLifeWithCharge = true;
    [Export] public float MinProjectileLifeMultiplier = 0.25f;
    [Export] public float MaxProjectileLifeMultiplier = 1f;
    [Export] public bool UseExponentialLifeScaling = true; // New: Enable exponential scaling
    [Export] public float LifeScalingExponent = 2.0f; // New: Higher values = more exponential (2.0 = quadratic, 3.0 = cubic, etc.)
    
    // Store original values to use as base for calculations
    private float _originalProjectileSpeed;
    private float _originalProjectileLifetime;
    private int _originalProjectileCount;
    private bool _originalValuesStored = false;

    protected override void ExecuteChargedAttack(Weapon weapon, Vector2 target, bool facingLeft, float chargedDamage)
    {
        if (ProjectileAttack == null)
        {
            GD.PrintErr("ChargedProjectileAttack: ProjectileAttack is null!");
            return;
        }

        // Store original values on first use
        if (!_originalValuesStored)
        {
            _originalProjectileSpeed = ProjectileAttack.ProjectileSpeed;
            _originalProjectileLifetime = ProjectileAttack.ProjectileLifetime;
            _originalProjectileCount = ProjectileAttack.ProjectileCount;
            _originalValuesStored = true;
        }

        if (weapon._anim != null)
        {
            // Play the attack animation - this should have animation calls to OpenHitWindow
            string animName = weapon._isCurrentAttackHeavy ? "heavy_attack" : "light_attack";
            weapon._anim.Play(animName);
        }

        // Use the charge ratio calculated by the parent class
        GD.Print($"[ChargedProjectileAttack] Using charge ratio from parent: {chargeRatio}, charge time: {_currentChargeTime}");

        // Update projectile attack properties based on charge
        ConfigureProjectileAttackForCharge(chargedDamage, chargeRatio);

        // Execute the projectile attack using composition
        ProjectileAttack.Execute(weapon, target, facingLeft);

        GD.Print($"ChargedProjectileAttack executed with {ProjectileAttack.ProjectileCount} projectiles at {ProjectileAttack.ProjectileSpeed} speed, lifetime: {ProjectileAttack.ProjectileLifetime}");

        // Restore original values immediately after execution
        RestoreOriginalValues();

        weapon.OpenHitWindow(weapon._isCurrentAttackHeavy);
    }
    
    private void RestoreOriginalValues()
    {
        if (!_originalValuesStored) return;
        
        ProjectileAttack.ProjectileSpeed = _originalProjectileSpeed;
        ProjectileAttack.ProjectileLifetime = _originalProjectileLifetime;
        ProjectileAttack.ProjectileCount = _originalProjectileCount;
        // Note: We don't restore damage as it should be recalculated each time
    }

    private void ConfigureProjectileAttackForCharge(float chargedDamage, float chargeRatio)
    {
        GD.Print($"[ChargedProjectileAttack] Configuring with charge ratio: {chargeRatio}");
        
        // Set the charged damage
        ProjectileAttack.Damage = chargedDamage;

        // Scale projectile count based on charge if enabled
        if (ScaleProjectileCountWithCharge)
        {
            int scaledCount = Mathf.RoundToInt(Mathf.Lerp(1, MaxProjectileCount, chargeRatio));
            ProjectileAttack.ProjectileCount = Mathf.Clamp(scaledCount, 1, MaxProjectileCount);
            GD.Print($"[ChargedProjectileAttack] Scaled projectile count: {ProjectileAttack.ProjectileCount} (from {_originalProjectileCount})");
        }
        else
        {
            ProjectileAttack.ProjectileCount = _originalProjectileCount;
        }

        // Scale projectile speed based on charge if enabled (use original speed as base)
        if (ScaleSpeedWithCharge)
        {
            float speedMultiplier = Mathf.Lerp(MinProjectileSpeedMultiplier, MaxProjectileSpeedMultiplier, chargeRatio);
            ProjectileAttack.ProjectileSpeed = _originalProjectileSpeed * speedMultiplier;
            GD.Print($"[ChargedProjectileAttack] Scaled projectile speed: {ProjectileAttack.ProjectileSpeed} (original: {_originalProjectileSpeed}, multiplier: {speedMultiplier})");
        }
        else
        {
            ProjectileAttack.ProjectileSpeed = _originalProjectileSpeed;
        }

        // Scale projectile lifetime based on charge if enabled (use original lifetime as base)
        if (ScaleProjectileLifeWithCharge)
        {
            float lifeMultiplier;
            
            if (UseExponentialLifeScaling)
            {
                // Exponential scaling: pow(chargeRatio, exponent)
                // This makes low charge values much more punishing
                float exponentialRatio = Mathf.Pow(chargeRatio, LifeScalingExponent);
                lifeMultiplier = Mathf.Lerp(MinProjectileLifeMultiplier, MaxProjectileLifeMultiplier, exponentialRatio);
                GD.Print($"[ChargedProjectileAttack] Exponential life scaling - chargeRatio: {chargeRatio}, exponentialRatio: {exponentialRatio}, exponent: {LifeScalingExponent}");
            }
            else
            {
                // Linear scaling (original behavior)
                lifeMultiplier = Mathf.Lerp(MinProjectileLifeMultiplier, MaxProjectileLifeMultiplier, chargeRatio);
                GD.Print($"[ChargedProjectileAttack] Linear life scaling - chargeRatio: {chargeRatio}");
            }
            
            ProjectileAttack.ProjectileLifetime = _originalProjectileLifetime * lifeMultiplier;
            GD.Print($"[ChargedProjectileAttack] Scaled projectile lifetime: {ProjectileAttack.ProjectileLifetime} (original: {_originalProjectileLifetime}, multiplier: {lifeMultiplier})");
        }
        else
        {
            ProjectileAttack.ProjectileLifetime = _originalProjectileLifetime;
            GD.Print($"[ChargedProjectileAttack] Using original projectile lifetime: {ProjectileAttack.ProjectileLifetime}");
        }
    }
    
    // Implement IShootable interface by delegating to the exported projectile attack
    public void SpawnProjectile(Weapon weapon, Vector2 spawnPosition, Vector2 baseDirection, int projectileIndex)
    {
        ProjectileAttack?.SpawnProjectile(weapon, spawnPosition, baseDirection, projectileIndex);
    }
    
    // Override Interrupt to handle both charging and projectile cleanup
    public override void Interrupt(Weapon weapon)
    {
        base.Interrupt(weapon); // Handle charging interruption
        ProjectileAttack?.Interrupt(weapon); // Handle projectile interruption if needed
    }
}