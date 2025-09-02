using Godot;

public enum PedestalType
{
	Weapon,    // Swaps weapons
	Armor,     // Swaps armor pieces  
	Consumable // Swaps consumable items
}

public partial class Pedestal : StaticBody2D
{
	// ---- Exports ----
	[Export] public PackedScene ItemScene; // The item this pedestal offers
	[Export] public PedestalType PedestalType = PedestalType.Weapon;
	[Export] public float InteractRange = 80f;
	[Export] public string EmptyMessage = "Empty Pedestal";

	// ---- Node References ----
	private AnimatedSprite2D _pedestalSprite; // The pedestal base sprite
	private AnimatedSprite2D _itemSprite;     // The item display sprite
	private Label _interactLabel;
	private Area2D _interactArea;
	
	private bool _isPlayerInRange = false;
	private PlayerController _playerInRange = null;

	// ---- Item Info ----
	private string _itemName = "";
	private string _itemDescription = "";
	private float _time = 0f;

	public override void _Ready()
	{
		// Get node references
		_pedestalSprite = GetNodeOrNull<AnimatedSprite2D>("PedestalSprite");
		_interactLabel = GetNodeOrNull<Label>("InteractPrompt");
		_interactArea = GetNodeOrNull<Area2D>("InteractArea");
		_itemSprite = GetNodeOrNull<AnimatedSprite2D>("ItemSprite");


		if (_itemSprite == null)
			GD.PrintErr("ItemPedestal: Missing ItemSprite (AnimatedSprite2D)");
			
		if (_interactArea == null)
			GD.PrintErr("ItemPedestal: Missing InteractArea (Area2D)");

		// Setup interaction area
		if (_interactArea != null)
		{
			_interactArea.Monitoring = true;
			_interactArea.Monitorable = false;
			_interactArea.BodyEntered += OnPlayerEntered;
			_interactArea.BodyExited += OnPlayerExited;
		}

		// Setup item display
		SetupItemDisplay();

		// Hide interact prompt initially
		if (_interactLabel != null)
			_interactLabel.Visible = false;

		// Play pedestal animation if it exists
		if (_pedestalSprite != null && _pedestalSprite.SpriteFrames.HasAnimation("idle"))
			_pedestalSprite.Play("idle");
	}

	public override void _PhysicsProcess(double delta)
	{
		// Handle interact input
		if (_isPlayerInRange && Input.IsActionJustPressed("interact"))
		{
			HandleInteraction(_playerInRange);
		}

	}

	private void SetupItemDisplay()
	{
		if (ItemScene == null) 
		{
			ClearItemDisplay();
			return;
		}

		// Instantiate item temporarily to get its info
		var tempItem = ItemScene.Instantiate();
		
		// Add it to scene tree temporarily so _Ready() is called and nodes are initialized
		AddChild(tempItem);
		
		// Try to get item info through different interfaces
		if (tempItem is IPedestalItem pedestalItem)
		{
			_itemName = pedestalItem.GetItemName();
			_itemDescription = pedestalItem.GetItemDescription();
			
			var displaySprite = pedestalItem.GetDisplaySprite();
			if (displaySprite != null && _itemSprite != null)
			{
				// Copy sprite frames and animation
				_itemSprite.SpriteFrames = displaySprite.SpriteFrames;
				_itemSprite.Animation = displaySprite.Animation;
				
				// Use the pedestal-specific scale
				_itemSprite.Scale = pedestalItem.GetDisplayScale();
				
				// Position the sprite slightly above the pedestal
				_itemSprite.Position = new Vector2(0, -16*7/4);
				
				// Make it visible
				_itemSprite.Visible = true;
				
				// Play the idle animation if it exists
				if (_itemSprite.SpriteFrames.HasAnimation(_itemSprite.Animation))
					_itemSprite.Play(_itemSprite.Animation);
					
				GD.Print($"Displaying {_itemName} on pedestal with scale {_itemSprite.Scale}");
			}
			else
			{
				GD.PrintErr($"Failed to get display sprite for {_itemName}");
			}
		}
		else
		{
			GD.PrintErr($"Item {tempItem.Name} does not implement IPedestalItem interface");
		}

		// Clean up temp item
		tempItem.QueueFree();

		// Update interact prompt
		UpdateInteractPrompt();
	}

	private void OnPlayerEntered(Node2D body)
	{
		var player = body as PlayerController;
		if (player == null) return;

		_isPlayerInRange = true;
		_playerInRange = player;
		ShowInteractPrompt(true);
	}

	private void OnPlayerExited(Node2D body)
	{
		var player = body as PlayerController;
		if (player == null) return;

		_isPlayerInRange = false;
		_playerInRange = null;
		ShowInteractPrompt(false);
	}

	private void ShowInteractPrompt(bool show)
	{
		if (_interactLabel != null)
			_interactLabel.Visible = show;
	}

	private void UpdateInteractPrompt()
	{
		if (_interactLabel == null) return;

		if (ItemScene == null)
		{
			_interactLabel.Text = EmptyMessage;
			return;
		}

		string actionText = PedestalType switch
		{
			PedestalType.Weapon =>  "[E] Take Weapon",
			PedestalType.Armor => "[E] Take Armor", 
			PedestalType.Consumable => "[E] Take Item",
			_ => "[E] Interact"
		};

		_interactLabel.Text = $"{_itemName}\n{actionText}";
	}

	private void HandleInteraction(PlayerController player)
	{
		if (ItemScene == null || player == null) return;

		GD.Print($"Interacting with {PedestalType}: {_itemName}");

		switch (PedestalType)
		{
			case PedestalType.Weapon:
				HandleWeaponSwap(player);
				break;
			case PedestalType.Armor:
				HandleArmorSwap(player);
				break;
			case PedestalType.Consumable:
				HandleConsumableSwap(player);
				break;
		}
	}

	private void HandleWeaponSwap(PlayerController player)
	{
		// Get player's current weapon scene
		PackedScene playerWeaponScene = player._equippedWeaponScene;

		// Equip this pedestal's weapon to the player
		player.EquipWeapon(ItemScene);

		// Put player's old weapon on this pedestal (or make it empty)
		SetItem(playerWeaponScene);
	}

	private void HandleArmorSwap(PlayerController player)
	{
		// You'd implement armor swapping here
		// This depends on how you structure your armor system
		
		// For now, just a placeholder:
		GD.Print("Armor swapping not implemented yet");
		
		// Example structure:
		// var playerArmor = player.GetEquippedArmor(armorType);
		// player.EquipArmor(ItemScene);
		// SetItem(playerArmor?.GetScene());
	}

	private void HandleConsumableSwap(PlayerController player)
	{
		// Add to player inventory instead of swapping
		// You'd implement inventory system here
		
		GD.Print($"Player picked up consumable: {_itemName}");
		
		// Example:
		// player.AddToInventory(ItemScene);
		
		// Clear the pedestal after pickup
		SetItem(null);
	}

	private void ClearItemDisplay()
	{
		_itemName = EmptyMessage;
		_itemDescription = "";
		
		if (_itemSprite != null)
		{
			_itemSprite.Visible = false;
		}

		UpdateInteractPrompt();
	}

	// ---- Public Methods ----
	
	public void SetItem(PackedScene itemScene)
	{
		ItemScene = itemScene;
		
		if (itemScene != null)
		{
			if (_itemSprite != null)
				_itemSprite.Visible = true;
			SetupItemDisplay();
		}
		else
		{
			ClearItemDisplay();
		}
	}

	public void SetPedestalType(PedestalType type)
	{
		PedestalType = type;
		UpdateInteractPrompt();
	}
}
