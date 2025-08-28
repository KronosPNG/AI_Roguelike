using Godot;
using System;

public partial class Main : Node2D
{
	private PackedScene beanScene;
	private PackedScene swordScene;
	
	public override void _Ready()
	{
		// Load scenes
		beanScene = ResourceLoader.Load<PackedScene>("res://scenes/bean.tscn");
		swordScene = ResourceLoader.Load<PackedScene>("res://scenes/sword.tscn");
		
		// Instantiate Bean
		PlayerController beanInstance = beanScene.Instantiate<PlayerController>();
		beanInstance.Position = new Vector2(200, 150); // Center position
		AddChild(beanInstance);
		
		// Wait for Bean to be ready, then spawn sword in hand
		CallDeferred(nameof(SpawnSwordInBeanHand), beanInstance);
	}
	
	private void SpawnSwordInBeanHand(PlayerController bean)
	{
		// Get the Hand node from Bean
		Node2D handNode = bean.GetNodeOrNull<Node2D>("Hand");
		if (handNode == null)
		{
			GD.PrintErr("Main: Could not find Hand node in Bean");
			return;
		}
		
		// Instantiate the sword
		Node swordInstance = swordScene.Instantiate();
		
		// Add sword to the Hand node
		handNode.AddChild(swordInstance);
		
		GD.Print("Sword spawned in Bean's hand!");
	}
}
