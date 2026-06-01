# Reinforcement Learning Implementation

## Overview

This project includes a Unity ML-Agents reinforcement learning setup for training adaptive enemy behavior in a 2D bullet-hell shooter. The RL work is centered on a test enemy controlled by `EnemyAgent`, with episode lifecycle management handled by `RLTrainingManager`.

The implemented RL system is separate from the standard enemy AI scripts, so training behavior can be developed without replacing the base game logic.

## Core Components

### `EnemyAgent`

`Assets/Scripts/EnemyAgent.cs` implements a Unity ML-Agents `Agent` for enemy movement control. It requires a `Rigidbody2D` and uses ML-Agents namespaces for observations and actions.

Implemented features:

- Collects enemy position, player position, distance to player, normalized enemy health, and normalized player health.
- Defines a continuous action space for horizontal and vertical movement and applies those actions to `Rigidbody2D` velocity.
- Applies rewards for survival, damaging the player, winning, taking damage, and dying.
- Supports heuristic testing through keyboard movement input.
- Integrates with `TestEnemyHealthScript`, `EnemyHealthScript`, and `PlayerLivesScript`.

Current limitation: the agent still needs Unity `Behavior Parameters`, a `DecisionRequester`, and a PPO training configuration before a trained model can be produced and assigned.

## Episode Management

`Assets/Scripts/RLTrainingManager.cs` manages training episodes without reloading the scene.

Implemented episode behavior:

- Caches player and enemy start positions.
- Tracks episode time, episode count, damage metrics, and survival time.
- Ends episodes when the player dies, the enemy dies, or the time limit is reached.
- Resets player health, enemy health, positions, active state, time scale, and projectiles.
- Logs episode summary data to the Unity console.

The default episode time limit is configured through the inspector with `episodeTimeLimit`.

## Training Enemy Support

`Assets/Scripts/TestEnemyScript.cs` provides a test enemy for RL research scenarios. It includes configurable movement, health, fire rate, bullet speed, and spread-shot behavior.

`Assets/Scripts/TestEnemyHealthScript.cs` provides training-safe enemy health. It does not destroy the enemy on death; instead, it lets `RLTrainingManager` detect zero health and reset the episode.

## Reward Structure

The current reward terms in `EnemyAgent` are:

- `survivalReward`: small positive reward per decision step.
- `damagePenalty`: penalty when the enemy takes damage.
- `hitReward`: reward when enemy projectiles hit the player.
- `winReward`: reward when the player is defeated.
- `deathPenalty`: penalty when the enemy dies.

These values are exposed in the inspector for tuning.

## Dynamic Difficulty Integration

The project now also includes a non-RL dynamic difficulty adjustment system through `DanmakuDDAController`, `BulletPressureAnalyzer`, `PlayerPerformanceTelemetry`, and `DifficultyProfile`.

This DDA system is not itself a trained RL agent. It is a runtime controller that adjusts bullet pressure, spawn timing, enemy speed, and powerup cadence based on player telemetry and bullet-field pressure. It can be used alongside RL training to create more adaptive combat scenarios.

## Next RL Tasks

- Connect projectile hit events consistently to `EnemyAgent.RewardForHit`.
- Add ML-Agents behavior parameters in the Unity scene or prefab.
- Create a training configuration YAML for PPO or SAC.
- Add evaluation metrics for trained policy performance across fixed and DDA-enabled difficulty settings.
