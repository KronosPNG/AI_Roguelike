using Godot;

public interface IShootable
{
    void SpawnProjectile(Weapon weapon, Vector2 spawnPosition, Vector2 baseDirection, int projectileIndex);
    
}
