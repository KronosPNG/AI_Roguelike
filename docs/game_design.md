# Gameplay & Interaction Document

**Project:** Adaptive Boss AI Thesis Game
**Author:** Luigi Turco
**Version:** 0.1
**Date:** 2025-08-24

---

## 1. High-Level Concept

A compact 2D roguelike-inspired game designed to test adaptive boss AI. The player progresses through four fixed rooms (Weapon, Armor, Heal, Spawning) before facing a dynamically adaptive Boss. Rewards in the first three rooms are randomized, while basic non-AI enemies provide light combat encounters. The Boss uses reinforcement learning to adapt its behavior and item loadout based on the player’s build and playstyle.

---

## 2. Core Gameplay Loop

1. **Start Game** → Player begins with a longsword and no armor.
2. **Room Progression:**

   * **Weapon Room:** Defeat basic enemies → choose one weapon reward.
   * **Armor Room:** Defeat basic enemies → choose one armor reward.
   * **Heal Room:** Defeat basic enemies → receive one consumable reward.
   * **Spawning Room:** Narrative/transition room (no combat, sets up boss).
3. **Boss Room:** Player faces the adaptive boss. Boss behavior and loadout change according to player build and past actions.
4. **End:** Victory → session summary. Defeat → retry.

---

## 3. Player Character Data

### Core Stats (base values)

* **Max HP:** 100
* **Move Speed:** 100% (base)
* **Attack Power:** depends on equipped weapon (see Combat System)
* **Defense:** provided by armor (see mitigation below)

### Health & Recovery

* **Potion (Instant Heal):** restores **25%** of Max HP instantly; **10s** personal cooldown.
* **Medkit (Regeneration):** restores **45%** of Max HP over **6s** (ticks every 0.5s); cannot stack (re‑use refreshes duration); **15s** personal cooldown.
* **Carry limit:** player has **1 consumable slot** with maximum **2** uses; taking a new consumable **replaces** the current one.
* **Overheal:** not allowed; HP clamps at Max HP.

### Armor & Mitigation

* **None:** speed 100%, **0%** damage reduction.
* **Light:** speed **+10%**, **0%** damage reduction.
* **Medium:** speed **±0%**, **10%** damage reduction.
* **Heavy:** speed **−15%**, **25%** damage reduction.
* **Status protection:** Medium reduces status durations by **10%**; Heavy by **25%**.

### Elements & Status Effects

* **Fire → Burned:** damage‑over‑time for **3.0s**, ticking every 0.5s for **0.5% Max HP** per tick (**\~3% total**). Re‑application **refreshes** duration.
* **Ice → Frozen:** **−40%** move speed for **2.5s**; re‑application **refreshes** duration (no stacking).
* **Physical:** no status by default.

### Equipment & Inventory Rules

* **Weapons:** Longsword (starter), Dagger, Bow, Magic Staff — **1 equipped**; Weapon Room allows **swap**.
* **Armor:** None (starter), Light, Medium, Heavy — **1 equipped**; Armor Room allows **swap**.
* **Consumable:** Potion **or** Medkit — **1 slot**; Heal Room grants one and replaces current if taken.

---

## 4. Player Actions

* **Movement:** 8‑directional (WASD).
* **Primary Attack:** weapon‑specific (melee swing, bow shot, staff cast).
* **Secondary Attack:** weapon‑specific heavy/charged variant; each weapon may have an internal cooldown.
* **Dodge (Dash):** short burst with **0.25s** i‑frames; **1s** cooldown; distance \~**3 tiles** (tunable).
* **Use Consumable:** immediate activation; respects personal cooldown and slot rules above.

**Notes**

* No ammo/mana systems in MVP; **cooldowns** regulate attack/ability frequency.
* Hitstun/knockback: light knockback on heavy/charged hits (tunable).
* Damage application supports elemental flags to trigger Burn/Frozen.

---

## 5. Enemy Design

### Basic Non-AI Enemies (rooms before boss)

* **Melee Minion:** runs at player, basic melee attack.
* **Ranged Minion:** stands at distance, fires simple projectiles.

Enemies are deliberately simple (scripted behavior, no adaptation). Their role is to provide challenge before rewards.

### Boss Enemy

* **Loadout:** weapon, armor, consumables chosen dynamically.
* **Behavior Variables:** dodging, aggression, trap avoidance, consumable use.
* **Adaptation:** Reinforcement learning (PPO in PyTorch) trained against archetypal player strategies.

---

## 6. Room Breakdown

* **Weapon Room:**

  * Contains 2–3 basic enemies.
  * On victory → choose one weapon reward (dagger, bow, staff; cannot re-pick longsword).

* **Armor Room:**

  * Contains 2–3 basic enemies.
  * On victory → choose one armor reward (light, medium, heavy).

* **Heal Room:**

  * Contains 2–3 basic enemies.
  * On victory → choose one consumable (potion or medkit).

* **Spawning Room:**

  * Narrative/atmospheric, no combat.
  * Prepares player for boss (visual buildup).

* **Boss Room:**
F
  * One adaptive boss.
  * End of run on victory or defeat.

---

## 7. Combat System Overview

* **Weapons:**

  * Longsword:
    * Primary attack: snap cut, medium damage, medium speed.
    * Secondary attack: pulling cut, high damage, slow speed.
  * Dagger:
    * Primary attack: cut, low damage, high speed.
    * Secondary attack: stab, high damage, medium speed.
  * Bow:
    * Primary attack: shoot, medium damage, speed variable based on charge.
    * Secondary attack: spread attack, medium damage, high speed, multiple projectiles in spread pattern.
  * Staff:
    * Primary attack: cast, low damage, low speed, multiple projectiles on the same line.
    * Secondary attack: explosion, very high damage, very low speed, area damage, higher cooldown.

* **Armor:** modifies movement speed and defense.

* **Status Effects:**

  * Burned → DoT (damage over time for 3–5s).
  * Frozen → 40% slower movement for 2–3s.

---

## 8. Progression & Replayability

* **Rewards are randomized** (which 2–3 items appear in each choice room, element type of weapons is randomised).
* **Boss behavior changes** between runs due to adaptive ML policy.
* **Short sessions** allow rapid iteration for testing and player adaptation.

---

## 9. Controls (Default)

* **Move:** WASD
* **Primary Attack:** Left Mouse / J
* **Secondary Attack:** Right Mouse / K
* **Dodge:** Spacebar / L
* **Use Consumable:** Q / H

---

## 10. Game Flow Diagram (textual)

Start → Weapon Room (fight → reward) → Armor Room (fight → reward) → Heal Room (fight → reward) → Spawning Room (transition) → Boss Room (adaptive fight) → End (victory/defeat)

---

## 11. Technical Assumptions

* **2D top-down** perspective.
* **Engine:** Godot.
* **ML Integration:** PyTorch (training) → ONNX export → in-engine inference.
* **Enemy AI:** simple scripted for normal enemies; DRL for boss.

---

## 12. Win/Lose Conditions

* **Win:** Defeat boss.
* **Lose:** Player HP reaches 0.

---

## 13. Out of Scope (for MVP)

* Procedural room generation.
* More than 4 weapons/armors.
* Multiplayer or co-op.
* Complex consumables or traps.

---

**End of GDD Draft**
