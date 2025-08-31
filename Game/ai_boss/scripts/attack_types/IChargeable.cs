using Godot;

public interface IChargeable : IAttack
{
    float GetChargedDamage(float chargeTime);
    void StartCharging(Weapon weapon);
    void UpdateCharge(Weapon weapon, float delta);
    bool CanReleaseCharge();
    float getCurrentChargeTime();
    float getMaxChargeTime();

}