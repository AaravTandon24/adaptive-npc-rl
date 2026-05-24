# Adaptive NPC RL Project - Implementation Summary

## Overview
This project is a Unity-based top-down shooter game enhanced with reinforcement learning capabilities for training adaptive non-player characters (NPCs). The system enables training enemy agents to learn optimal combat behaviors through gameplay interactions with the player, using Unity's ML-Agents framework.

## Core Game Mechanics

### Player System
- **Movement**: WASD controls for 2D movement, mouse aiming for directional shooting
- **Combat**: 
  - Standard single-shot firing mechanism
  - Power-up system enabling multi-shot (spread) firing capability
  - Health system with visual UI feedback
- **Life System**: 
  - Health-based survival system
  - Game over triggered when player health reaches zero
  - Health power-ups for recovery
  - Explosion effect on player death

### Enemy System
- **Basic Enemy Behavior** (in standard scripts):
  - Patrol/chase behavior based on distance to player
  - Shooting mechanics with configurable fire rates
  - Multiple enemy types (standard shooters, swarmers, etc.)
- **RL Training Enemies** (modified for training):
  - Specialized health systems that don't trigger immediate destruction
  - Configurable combat parameters for training scenarios
  - Integration points for ML-Agent control

### Game Management
- **Wave System**: Spawning mechanisms for enemy waves
- **Power-up System**: Random spawning of beneficial items (health, multi-shot)
- **Scoring**: Tracking of player performance metrics
- **Pause/Menu Systems**: Game pausing and navigation

## Reinforcement Learning Implementation

### RLTrainingManager (`RLTrainingManager.cs`)
The central component managing the RL training process:

**Key Features:**
- Manages training episodes between player and enemy without requiring scene reloads
- Tracks episode metrics:
  - Episode duration/time survived
  - Damage dealt by both player and enemy
  - Health percentages of both entities
- Automatic episode reset when:
  - Player health reaches zero
  - Enemy health reaches zero  
  - Time limit is exceeded
- Provides clean state reset for consistent training conditions
- Exposes APIs for reading normalized health values (0-1 range) for observation spaces

**Episode Management:**
1. Initialization: Caches starting positions and component references
2. Monitoring: Tracks time and checks termination conditions each frame
3. Termination: Ends episode when win/lose/timeout conditions met
4. Reset: Restores entities to initial states, clears projectiles, resets metrics

### Enemy Agent (`EnemyAgent.cs`)
ML-Agents implementation that wraps enemy behavior for RL training:

**Observation Space (Vector Sensor):**
- Enemy position (x, y)
- Player position (x, y) 
- Distance to player (scalar)
- Normalized enemy health (0-1)
- Normalized player health (0-1)

**Action Space (Continuous):**
- Two continuous actions representing movement:
  - Action [0]: Horizontal movement (-1 to 1)
  - Action [1]: Vertical movement (-1 to 1)
- Actions are mapped to velocity via Rigidbody2D

**Reward Function:**
- Small survival reward per timestep (encourages longevity)
- Penalties for taking damage (proportional to damage amount)
- Rewards for hitting player with projectiles
- Large reward for winning (killing player)
- Penalty for dying

**Behavior:**
- During training: Movement controlled by ML-Agent policy
- For testing: Heuristic mode allows keyboard control (WASD/arrow keys)
- Integrates with existing enemy health systems or uses internal fallback
- Communicates hit events to modify rewards when projectiles connect with player

### Specialized Health Systems
Modified health scripts for RL training that avoid automatic destruction:

**TestEnemyHealthScript (`TestEnemyHealthScript.cs`):**
- Standard health tracking (current/max)
- Damage application without triggering death events
- Explicit reset method for episode restoration
- Designed to work with RLTrainingManager which handles episode termination based on health thresholds

## Training Workflow

1. **Setup**: 
   - Player and enemy entities placed in scene
   - RLTrainingManager configured with references to both
   - EnemyAgent component attached to enemy with ML-Agents parameters

2. **Episode Start**:
   - RLTrainingManager initializes and begins first episode
   - EnemyAgent observes initial state through CollectObservations()
   - ML-Agent policy determines initial action

3. **Step Loop** (per physics update):
   - EnemyAgent receives actions from policy
   - Applies movement via Rigidbody2D velocity
   - Receives survival reward
   - Checks for damage taken and applies penalties
   - Monitors for terminal conditions (health ≤ 0)
   - Updates observations for next step

4. **Episode End**:
   - Triggered by: player death, enemy death, or timeout
   - RLTrainingManager records statistics
   - Entities reset to initial states
   - New episode begins automatically

5. **Training Integration**:
   - Compatible with Unity ML-Agents training workflow
   - Can be trained using PPO, SAC, or other RL algorithms
   - Supports curriculum learning through adjustable parameters

## File Structure

### Core Gameplay Scripts
- `PlayerMovement.cs` - WASD movement with mouse aiming
- `Shooting.cs` - Bullet firing with powerup support
- `PlayerLivesScript.cs` - Player health and game over handling
- `EnemyScript.cs` / `TestEnemyScript.cs` - Enemy behavior patterns
- `EnemyAgent.cs` - ML-Agent wrapper for RL training
- `RLTrainingManager.cs` - Episode management for training

### Support Systems
- `PowerupSpawnSystem.cs` - Random powerup generation
- `WaveManagerScript.cs` - Enemy wave spawning
- `Health powerups` - Collectible health restoration
- `Multi-shot powerups` - Temporary spread shot capability

### ML-Agents Specific
- Located in `Assets/ML-Agents` folder
- Configuration files for training parameters
- Prefabs set up for agent training

## Scenes
- **MainMenu.unity** - Game entry point
- **SampleScene.unity** - Standard gameplay level
- **Testing.unity** - Specialized scene for RL training experiments

## Prefabs
- Player character with movement and shooting components
- Various enemy types (shooters, swarmers, etc.)
- Power-up items (health, multi-shot)
- Bullet prefabs for player and enemy projectiles

## How the RL System Works Together

1. **Observation Collection**: EnemyAgent gathers state information (positions, distance, health) and feeds it to the neural network policy
2. **Action Selection**: Policy outputs continuous movement values (-1 to 1 range for X,Y axes)
3. **Action Application**: EnemyAgent converts policy output to Rigidbody2D velocity
4. **Reward Calculation**: Based on survival, damage dealt/taken, and victory conditions
5. **Episode Management**: RLTrainingManager handles the timing and reset of training cycles
6. **Learning**: ML-Agents updates policy based on collected experiences to maximize cumulative reward

## Extensibility
The system is designed to be modular:
- New enemy types can be added with minimal changes
- Reward functions can be adjusted in EnemyAgent for different behaviors
- Observation space can be extended with additional game state information
- Training parameters can be modified without changing core game logic

This implementation provides a robust platform for researching adaptive NPC behaviors through reinforcement learning in a game context.


## Novelty
The honest novelty is not “DDA has never been done.” Dynamic difficulty adjustment is already a well-established field, including performance-based DDA, player modeling, deep-learning-based DDA, and RL-based DDA. Bullet-hell generation is also established through systems like Talakat and Keiki.

The defensible novelty of this project is more specific:

**A bullet-hell DDA system that adapts enemy pressure using real-time bullet-field survivability, player telemetry, and adaptive NPC parameters together.**

Most DDA systems adjust broad difficulty values: enemy health, damage, spawn rate, or level parameters. What we are trying to do is different because the controller looks at the actual bullet-hell state: bullet density, bullet proximity, projected collision risk, near misses, player health, damage trend, hit rate, and survival pressure.

So the proposed academic contribution is:

> A counterfactual bullet-pressure-based dynamic difficulty system for adaptive NPC bullet-hell combat, integrated with Unity ML-Agents-compatible enemy training.

“Counterfactual” here means the system estimates not only what happened, but what is likely to happen soon: can the player plausibly survive the current bullet field over the next short time window?

That is stronger than simple rules like:

```text
if player health low -> reduce difficulty
if player score high -> increase difficulty
```

Instead, the system tries to estimate pressure from the live combat field.

Relevant prior work boundaries:
- DDA in games is well studied.
- RL-based DDA exists.
- Bullet-hell procedural generation exists.
- The novelty is the combination of bullet-field pressure analysis, player telemetry, and adaptive NPC/wave/powerup control in this specific bullet-hell RL context.

**What Has Been Implemented**
Implemented so far:

1. **RL Enemy Agent**
   `EnemyAgent.cs` implements a Unity ML-Agents `Agent`.

It currently observes:
- enemy position
- player position
- distance to player
- normalized enemy health
- normalized player health

It defines rewards for:
- surviving
- taking damage
- hitting the player
- defeating the player
- dying

Important limitation: `OnActionReceived` currently does not yet read `actions.ContinuousActions[0]` and `[1]`; movement values are still set to zero. So the RL structure exists, but learned movement control is not fully wired.

2. **RL Episode Manager**
   `RLTrainingManager.cs` manages repeated training episodes without reloading the scene.

It handles:
- player/enemy reset
- episode timer
- episode count
- projectile cleanup
- ending on player death, enemy death, or timeout
- console logging of episode stats

3. **Training Enemy Health**
   `TestEnemyHealthScript.cs` supports RL training by allowing the enemy to reach zero health without destroying itself. This lets the training manager reset the episode cleanly.

4. **Test Enemy**
   `TestEnemyScript.cs` provides a configurable enemy with:
- movement
- fire rate
- bullet speed
- spread angle
- training-safe health integration

5. **Dynamic Difficulty System**
   Newly implemented DDA components:
- `DanmakuDDAController.cs`
- `BulletPressureAnalyzer.cs`
- `PlayerPerformanceTelemetry.cs`
- `DifficultyProfile.cs`

The DDA system adjusts:
- enemy fire rate
- bullet speed
- spread angle
- enemy movement speed
- enemy spawn interval
- powerup spawn interval
- max active enemy bullets

It is currently a rule/controller-based adaptive system, not a trained RL DDA agent.

6. **Documentation**
   Added:
- `AGENTS.md`
- `RL_IMPLEMENTATION.md`

`RL_IMPLEMENTATION.md` explains the current RL architecture and known gaps.

**What Still Needs To Be Done**
Highest priority next steps:

1. **Fix RL Action Application**
   In `EnemyAgent.OnActionReceived`, replace the zero movement values with:

```csharp
float moveX = actions.ContinuousActions[0];
float moveY = actions.ContinuousActions[1];
```

Without this, the learned policy cannot control movement.

2. **Connect Hit Rewards Properly**
   Enemy projectile hits should call:

```csharp
enemyAgent.RewardForHit();
```

Right now the reward method exists, but hit events need to be consistently connected.

3. **Add ML-Agents Behavior Parameters**
   In Unity, the enemy prefab or scene object needs:
- `Behavior Parameters`
- continuous action size: `2`
- vector observation size matching current observations
- decision requester or manual decision calls

4. **Create Training Config**
   Add a YAML config for PPO or SAC. PPO is the safer first choice.

5. **Define Evaluation Baselines**
   For academic strength, compare:
- fixed difficulty
- simple health/score DDA
- current bullet-pressure DDA
- RL enemy with and without DDA

6. **Log Experimental Data**
   Add CSV/JSON logging for:
- episode duration
- player HP over time
- enemy HP over time
- pressure score
- difficulty scalar
- deaths
- hit rate
- near misses
- bullets active

7. **Run Actual Training**
   Train the enemy policy with ML-Agents, then evaluate it against human/player-controller behavior.

8. **Strengthen Novelty Claim**
   The final paper/report should avoid saying “no one has done DDA before.” The better claim is:

> This project proposes and evaluates a bullet-field-pressure-aware DDA controller for adaptive NPC bullet-hell combat, combining short-horizon survivability estimation with player telemetry and ML-Agents-compatible enemy training.