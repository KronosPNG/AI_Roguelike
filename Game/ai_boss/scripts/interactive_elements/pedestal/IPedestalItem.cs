using Godot;

// Interface for any item that can be placed on pedestals
public interface IPedestalItem
{
    string GetItemName();
    string GetItemDescription();
    AnimatedSprite2D GetDisplaySprite(); // Return the sprite node to copy from
    Vector2 GetDisplayScale(); // Return the scale to use for pedestal display
    bool CanSwapWith(IPedestalItem otherItem); // Can this item be swapped with another?
    void OnPickedUp(PlayerController player); // What happens when player takes this item
}