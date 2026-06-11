# PPO Model v6 Training Summary

We have successfully trained and deployed the new PPO-based movement policy (**v6**) for the Enemy Agent. The agent has transitioned from a stationary policy (under punitive rewards) to an active dodging and kiting behavior.

## Model Specifications

* **Model File**: [EnemyAgent_v6.onnx](file:///c:/Users/Arzaa/adaptive-npc-rl/Assets/ML-Agents/Models/EnemyAgent_v6.onnx)
* **Opset Version**: ONNX Opset 15 (compatible with Unity Sentis `1.2.0-exp.2`)
* **Vector Observations**: 29 continuous inputs
* **Continuous Actions**: 2 outputs (Velocity X, Velocity Y)

---

## Key Training Modifications & Enhancements

### 1. Rebalanced Reward Function
To break the agent's learned "fear of movement" caused by high death penalties, we redesigned the reward function:
* **Graze/Near-Miss Reward**: Grants `+0.05` reward when player bullets pass within `1.2` units of the agent without a collision. This incentivizes active, close dodging.
* **Shaped Range-Keeping**: Replaced binary range rewards with a shaped gradient curve. The agent receives maximum reward (`+0.01/step`) at the midpoint of its ideal range (`5.0f–8.0f` units from the player), which tapers off smoothly.
* **Capped Damage Penalties**: Capped total episode damage penalties at `-0.5f` so damage does not drown out exploration signals.
* **Survival Bonus**: Added a progressive step-reward (`+0.005f * elapsed_ratio`) that scales as the agent survives longer in the episode.
* **Removed Redundant Penalties**: Deleted the duplicate short-episode penalty.

### 2. Spacing and Boundary Clamp
* Added a `0.8f` screen padding clamp for both the Player Bot and Enemy Agent to prevent them from sliding off-screen.
* Optimized the training bot to be a realistic sparring partner: kiting at `6.5f` units, aiming with high rotation jitter (`18.0`), and using lower tracking accuracy so bullets fly naturally around the enemy for graze collection.

### 3. Training Run Progress (`v6`)
* **Duration**: Trained up to **341,259 steps** using ML-Agents `mlagents-learn` command.
* **Performance**: Achieved **positive mean rewards (~ +0.9)**, showing successful adaptation to kiting, dodging, and grazing player bullets. (Previous runs struggled in negative reward ranges around `-6.5` to `-7.5`).

---

## Deployment Configuration

* **Behavior Type**: Configured the Enemy Agent to run in **`Inference Only`** mode by default.
* **Vector Observation Size**: Configured to **`29`** in the `Testing` scene to match the neural network shape.
