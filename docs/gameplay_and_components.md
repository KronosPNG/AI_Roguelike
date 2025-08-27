# Gameplay System and Component Specification

**Author:** Luigi Turco
**Version:** 0.1
**Date:** 2025-08-27

## Player Controller

### 1. Overview

- **Purpose**: Allowing control of the playable character movements and actions  

### 2. Entities & Data Model
#### Player System
**Entity**: PlayerController  
**Parameters**: 
 - **Character Data**
    - `Health`: Current health points of the character;
    - `MaxHealth`: Maximum health capacity;
    - `DamageReduction`: Damage reduction modifier applied to incoming damage;
  
 - **Movement Data**
    - `BaseSpeed`: Normal movement speed in pixels per second;
    - `SpeedModifier`: Multiplier applied to base speed for temporary speed changes;
    - `DodgeSpeed`: Movement speed during dodge maneuvers;
    - `_dodgeDirection`: Direction vector for dodge movement;
  
 - **Input and Timing**
      - `DodgeInputLeniency`: Time window in seconds to accept dodge direction input after dodge button press;
      - `_dodgeInputTimer`: Timer tracking remaining time for dodge input leniency;
  
 - **Facing and Orientation**
    - `_facing`: Current facing direction vector;
    - `_lastHorizontalFacing`: Last horizontal direction for sprite flipping;
  
 - **State Management**
    - `_state`: Current player state;
    - `_prevState`: Previous state for detecting state changes;

 - **Node References** 
    - `_anim`: Reference to animated sprite component;
  
### 3. State Machine

The player controller implements a state machine with the following states:

- **Idle**: Player is stationary with no input
- **Walking**: Player is moving based on directional input
- **DodgePrep**: Brief preparation phase after dodge input is detected
- **Dodge**: Active dodge movement with increased speed

### 4. Core Mechanics

#### Movement System
- Uses `CharacterBody2D` physics for collision detection
- Movement speed calculated as `BaseSpeed * SpeedModifier`
- Supports 8-directional movement with normalized input vectors

#### Dodge System
- Two-phase dodge: preparation and execution
- Input leniency allows direction input shortly after dodge button press
- Falls back to current facing direction if no movement input provided
- Significantly increased movement speed during dodge execution

#### Animation System
- Automatic sprite flipping based on horizontal movement direction
- State-based animation selection (idle, walking, dodge)
- Animation completion signals trigger state transitions

### 5. Input Handling

- **Movement**: Arrow keys or WASD for directional input
- **Dodge**: Dedicated dodge button with direction sampling
- **Input Buffering**: Brief window to accept movement input after dodge activation

### 6. Method Interfaces

#### Core Methods
- **`_Ready()`**: Initializes the player controller, sets up sprite references and connects animation signals
- **`_PhysicsProcess(double delta)`**: Main update loop handling input, state transitions, movement, and animation updates

#### Input Methods
- **`ReadDirection()`**: Returns normalized input vector from movement controls
- **`MovementJustPressed()`**: Checks if any movement key was pressed this frame, returns direction vector

#### State Management Methods
- **`HandleStateTransitions(Vector2 input)`**: Manages state machine transitions based on current state and input
- **`TransitionToState(PlayerState next)`**: Safely transitions between player states
- **`OnEnterState(PlayerState s)`**: Handles initialization when entering a new state

#### Movement Methods
- **`UpdateFacing(Vector2 inputDir)`**: Updates facing direction and horizontal facing memory
- **`ApplyMovementByState(double delta, Vector2 input)`**: Applies physics movement based on current state

#### Animation Methods
- **`UpdateAnimationIfNeeded()`**: Updates sprite animation when state or facing changes
- **`OnAnimationFinished()`**: Handles animation completion events, particularly for dodge state transitions

### 7. Events

#### Animation Events
- **`AnimationFinished`**: Triggered when any animation completes
  - Connected to `OnAnimationFinished()` method
  - Used primarily for dodge animation completion to transition back to movement states

#### State Change Events
- **State Transitions**: Internal state changes trigger appropriate animation and movement updates
  - Idle ↔ Walking: Based on movement input presence
  - Any State → DodgePrep: On dodge input
  - DodgePrep → Dodge: On movement input or timer expiration
  - Dodge → Idle/Walking: On dodge animation completion

## Melee Weapon System

### 1. Overview

- **Purpose**: Handles melee weapon attacks with arc-based hitboxes, timing systems, and damage application

### 2. Entities & Data Model
#### Melee Weapon System
**Entity**: MeleeWeapon  
**Parameters**: 
 - **Weapon Identity**
    - `WeaponName`: Display name of the weapon;
    - `Description`: Descriptive text for the weapon;
  
 - **Attack Timing**
    - `LightCooldown`: Cooldown duration between light attacks;
    - `HeavyCooldown`: Cooldown duration between heavy attacks;
    - `LightWindup`: Delay before light attack becomes active;
    - `LightActive`: Duration of light attack hit window;
    - `HeavyWindup`: Delay before heavy attack becomes active;
    - `HeavyActive`: Duration of heavy attack hit window;
  
 - **Damage Properties**
    - `LightDamage`: Damage value for light attacks;
    - `HeavyDamage`: Damage value for heavy attacks;
    - `AutoApplyDamage`: Whether weapon automatically applies damage to hit targets;
    - `EnemyDamageMethodName`: Method name to call on hit entities for damage application;
  
 - **Light Attack Hitbox**
    - `LightInnerRadius`: Inner radius of light attack arc;
    - `LightOuterRadius`: Outer radius of light attack arc;
    - `LightAngleDeg`: Angular width of light attack arc in degrees;
    - `LightArcCenterOffsetDeg`: Rotational offset for light attack arc center;
  
 - **Heavy Attack Hitbox**
    - `HeavyInnerRadius`: Inner radius of heavy attack arc;
    - `HeavyOuterRadius`: Outer radius of heavy attack arc;
    - `HeavyAngleDeg`: Angular width of heavy attack arc in degrees;
    - `HeavyArcCenterOffsetDeg`: Rotational offset for heavy attack arc center;
  
 - **Sweep Animation**
    - `LightSweepEnabled`: Whether light attacks use progressive hitbox reveal;
    - `LightSweepFromStartEdge`: Direction of light attack sweep progression;
    - `LightSweepStepDeg`: Angular increment for light attack sweep steps;
    - `LightSweepStepDelay`: Time delay between light attack sweep steps;
    - `HeavySweepEnabled`: Whether heavy attacks use progressive hitbox reveal;
    - `HeavySweepFromStartEdge`: Direction of heavy attack sweep progression;
    - `HeavySweepStepDeg`: Angular increment for heavy attack sweep steps;
    - `HeavySweepStepDelay`: Time delay between heavy attack sweep steps;
  
 - **State Management**
    - `_state`: Current weapon state (Ready, Windup, Active);
    - `_isCurrentAttackHeavy`: Tracks whether current attack is heavy type;
    - `_lightCooldownTimer`: Remaining cooldown time for light attacks;
    - `_heavyCooldownTimer`: Remaining cooldown time for heavy attacks;
    - `_isSweeping`: Whether sweep animation is currently active;
  
 - **Hit Detection**
    - `_pendingHitTarget`: Stored target position when attack was initiated;
    - `_alreadyHit`: Collection of entities already hit during current attack;
  
 - **Node References**
    - `_anim`: Reference to animated sprite component;
    - `_hitArea`: Reference to hit detection area;
    - `_hitAreaShape`: Reference to collision polygon for hit detection;

### 3. State Machine

The weapon system implements a state machine with the following states:

- **Ready**: Weapon is idle and can start new attacks
- **Windup**: Attack initiated, preparing for active phase
- **Active**: Hit detection enabled, can damage entities

### 4. Core Mechanics

#### Attack System
- Two attack types: light and heavy with different properties
- Each attack type has independent cooldown timers
- Target direction captured at attack initiation to prevent mid-attack aiming changes
- Automatic state progression through windup and active phases

#### Hitbox Generation
- Arc-shaped (crescent) hitboxes defined by inner/outer radius and angle
- Dynamic polygon generation based on attack parameters
- Configurable arc center offset for asymmetric attack patterns

#### Sweep Animation
- Progressive hitbox reveal for visual attack progression
- Configurable sweep direction (start-to-end or end-to-start)
- Adjustable step size and timing for smooth animation
- Can be disabled for instant full-area attacks

#### Damage Application
- Prevents multiple hits on same entity during single attack
- Configurable automatic damage application or signal-based notification
- Flexible damage method calling with fallback options

### 5. Method Interfaces

#### Public Attack API
- **`AttackLight(Vector2 mouseGlobalPos)`**: Initiates light attack toward target position
- **`AttackHeavy(Vector2 mouseGlobalPos)`**: Initiates heavy attack toward target position

#### State Management Methods
- **`CanStartAttack(bool isHeavy)`**: Checks if attack can be initiated based on state and cooldowns
- **`StartAttackSequence(bool isHeavyAttack)`**: Manages complete attack sequence timing
- **`OpenHitWindow(bool isHeavy)`**: Activates hit detection and builds collision geometry
- **`CloseHitWindow(bool isHeavy)`**: Deactivates hit detection and cleans up collision
- **`ResetWeaponState(bool isHeavy)`**: Returns weapon to ready state after attack completion

#### Geometry Methods
- **`BuildAndApplyCrescent(bool isHeavy)`**: Constructs and applies hitbox collision polygon
- **`BuildCrescentPolygon(...)`**: Generates arc-shaped polygon vertices
- **`SweepCrescent(...)`**: Manages progressive hitbox reveal animation
- **`AutoCloseHitWindowAfter(float secs, bool isHeavy)`**: Automatic hit window closure timing

#### Collision Methods
- **`OnBodyEntered(Node body)`**: Handles entity collision during active hit window

### 6. Events

#### Attack Events
- **`AttackStarted(string attackName)`**: Emitted when attack sequence begins
- **`AttackEnded(string attackName)`**: Emitted when attack sequence completes
- **`EntityHit(Node2D entity, int damage)`**: Emitted when entity enters hit area

#### Collision Events
- **`BodyEntered`**: Area2D signal for entity collision detection
  - Connected to `OnBodyEntered()` method
  - Only active during weapon Active state

