using Godot;
using System;

public interface IAttack
{
    void Execute(Weapon weapon, Vector2 target, bool facingLeft);
    void Interrupt(Weapon weapon);
}