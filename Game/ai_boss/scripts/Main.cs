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
		swordScene = ResourceLoader.Load<PackedScene>("res://scenes/weapons/staff.tscn");
		
		// Instantiate Bean
		PlayerController beanInstance = beanScene.Instantiate<PlayerController>();
		beanInstance.Position = new Vector2(200, 150); // Center position
		AddChild(beanInstance);
		
		// Wait for Bean to be ready, then spawn sword in hand
		CallDeferred(nameof(SpawnSwordInBeanHand), beanInstance);
	}
	
	private void SpawnSwordInBeanHand(PlayerController bean)
	{
		bean.CallDeferred("EquipWeapon", swordScene);
	}
}
