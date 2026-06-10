# PPO Movement Improvement Implementation Guide

Use this guide to reapply the PPO movement-learning changes after pulling the latest repo state. The goal is to make PPO cleanly own enemy movement while keeping shooting scripted.

## Why This Change Exists

Before this change, `EnemyAgent` could output movement actions, but `TestEnemyScript` also moved the same enemy with hardcoded approach/retreat logic. That creates a control conflict: PPO changes velocity through `Rigidbody2D`, while the script directly changes `transform.position`.

This implementation fixes that by:

- Disabling scripted movement automatically when `EnemyAgent` is present.
- Expanding PPO observations from `9` to `29`.
- Adding movement-specific reward shaping.
- Keeping enemy shooting behavior scripted.
- Updating editor setup and documentation to match the new observation size.

Existing ONNX models trained with the old 9-observation input are not compatible. Retrain PPO after applying this.

## Files To Change

- `Assets/Scripts/EnemyAgent.cs`
- `Assets/Scripts/TestEnemyScript.cs`
- `Assets/Editor/RLTrainingSceneSetup.cs`
- `PPO_TRAINING.md`
- `RL_IMPLEMENTATION.md`
- `PROJECT_SUMMARY.md`

## Step 1: Pull First

Before reapplying the changes:

```powershell
git pull origin arzaan-branch
git status --short
```

If there are local generated Unity files, either stash them or discard them intentionally before pulling. Avoid committing generated `Library/`, `Logs/`, or `UserSettings/`.

## Step 2: Update `EnemyAgent`

In `Assets/Scripts/EnemyAgent.cs`, add movement-training inspector fields near the existing `moveSpeed` field:

```csharp
[Header("Movement Training")]
[Tooltip("Distance where the enemy is too close to the player")]
public float idealRangeMin = 3f;
[Tooltip("Distance where the enemy is too far from the player")]
public float idealRangeMax = 6f;
[Tooltip("Arena minimum corner used for boundary observations and rewards")]
public Vector2 arenaMin = new Vector2(-8f, -4.5f);
[Tooltip("Arena maximum corner used for boundary observations and rewards")]
public Vector2 arenaMax = new Vector2(8f, 4.5f);
[Tooltip("Distance from arena edge that starts producing boundary penalties")]
public float boundaryDangerDistance = 0.75f;
[Tooltip("Observation radius used to normalize nearest player bullet distance")]
public float bulletObservationRadius = 10f;
```

Add reward-shaping fields near the existing reward fields:

```csharp
[Tooltip("Reward for staying in the preferred firing/kiting range")]
public float idealRangeReward = 0.003f;
[Tooltip("Penalty for being too close to the player")]
public float tooClosePenalty = 0.006f;
[Tooltip("Penalty for being too far from the player")]
public float tooFarPenalty = 0.002f;
[Tooltip("Reward for moving laterally around the player while in range")]
public float lateralMovementReward = 0.002f;
[Tooltip("Penalty for producing almost no movement")]
public float idlePenalty = 0.001f;
[Tooltip("Reward for moving away from an incoming player bullet")]
public float dodgeReward = 0.004f;
[Tooltip("Penalty for staying near arena bounds")]
public float boundaryPenalty = 0.003f;
[Tooltip("Penalty for abrupt movement direction changes")]
public float directionChangePenalty = 0.001f;
```

Add cached fields:

```csharp
private Rigidbody2D playerRb;
private Vector2 previousMove;
```

When caching `playerHealthScript`, also cache:

```csharp
playerRb = player.GetComponent<Rigidbody2D>();
```

Change ML-Agents behavior configuration:

```csharp
behaviorParameters.BrainParameters.VectorObservationSize = 29;
behaviorParameters.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);
```

Important: do not force `BehaviorType` inside `EnemyAgent` if you want the Inspector to control Default, Heuristic, or Inference mode. Forcing `Default` can make Unity wait for a Python trainer when you just want inference/manual testing.

In `OnEpisodeBegin()`, reset:

```csharp
previousMove = Vector2.zero;
```

## Step 3: Replace The Observation Vector

Replace the old 9-value observation logic with a 29-value observation vector:

1. Normalized enemy position x/y
2. Normalized player position x/y
3. Relative player offset x/y
4. Normalized distance to player
5. Direction to player x/y
6. Enemy velocity x/y
7. Player velocity x/y
8. Normalized enemy HP
9. Normalized player HP
10. DDA current difficulty
11. DDA current pressure
12. Nearest player-bullet relative x/y
13. Nearest player-bullet velocity direction x/y
14. Nearest player-bullet normalized distance
15. Nearest player-bullet approaching flag
16. Boundary distances: left, right, bottom, top

The total must be exactly `29`.

Use helper methods for:

- `NormalizePosition(Vector2 position)`
- `GetNormalizedBoundaryDistances(Vector2 position)`
- `GetNearestPlayerBullet(Vector2 enemyPosition)`
- `GetBulletVelocity(GameObject bullet)`

The nearest bullet should search objects tagged `Player Bullet`, compute relative position to the enemy, read `Rigidbody2D.velocity`, and mark `isApproaching` when the bullet velocity points toward the enemy.

## Step 4: Harden `OnActionReceived`

Keep two continuous actions:

```csharp
float moveX = actions.ContinuousActions[0];
float moveY = actions.ContinuousActions[1];
```

Add safety for invalid model output:

```csharp
if (float.IsNaN(moveX) || float.IsNaN(moveY) || float.IsInfinity(moveX) || float.IsInfinity(moveY))
{
    moveX = 0f;
    moveY = 0f;
}
```

Clamp both values to `[-1, 1]`, normalize if magnitude is above `1`, then apply:

```csharp
rb.velocity = move * moveSpeed;
```

Clamp enemy position to `arenaMin`/`arenaMax` after applying velocity so the model cannot drift off-screen.

## Step 5: Add Movement Reward Shaping

Create `ApplyMovementRewards(Vector2 move)` and call it after the normal survival reward.

Reward/punish:

- `idealRangeReward` when distance to player is between `idealRangeMin` and `idealRangeMax`.
- `lateralMovementReward` when moving perpendicular to the player direction while in range.
- `tooClosePenalty` when under `idealRangeMin`.
- `tooFarPenalty` when over `idealRangeMax`.
- `idlePenalty` when movement magnitude is almost zero.
- `directionChangePenalty` for abrupt movement direction changes.
- `boundaryPenalty` when close to arena edges.
- `dodgeReward` when an incoming player bullet exists and movement points away from it.

After applying movement rewards:

```csharp
previousMove = move;
```

## Step 6: Update Heuristic Controls

Use keys that do not conflict with the player movement controls. Recommended:

```csharp
I = up
K = down
J = left
L = right
```

This avoids fighting the player WASD/arrow controls during manual heuristic testing.

## Step 7: Disable Scripted Movement In `TestEnemyScript`

In `Assets/Scripts/TestEnemyScript.cs`, add these fields under movement settings:

```csharp
[Tooltip("Use hardcoded approach/retreat movement. Disable this for PPO-controlled movement training.")]
public bool useScriptedMovement = true;
[Tooltip("Automatically disable scripted movement when an EnemyAgent is attached.")]
public bool disableScriptedMovementWhenAgentPresent = true;
```

After caching `enemyAgent` in `Start()`:

```csharp
if (disableScriptedMovementWhenAgentPresent && enemyAgent != null)
    useScriptedMovement = false;
```

Move the old approach/retreat logic into:

```csharp
private void HandleScriptedMovement()
```

In `Update()`, call it only when:

```csharp
if (useScriptedMovement)
    HandleScriptedMovement();
```

Leave shooting unchanged. The point is to let PPO own movement while keeping firing behavior stable.

## Step 8: Update Editor Scene Setup

In `Assets/Editor/RLTrainingSceneSetup.cs`, update:

```csharp
private const int VectorObservationSize = 29;
```

The continuous action size stays:

```csharp
private const int ContinuousActionSize = 2;
```

Run `Tools/RL/Configure Testing Scene` in Unity after applying the change.

## Step 9: Update Documentation

Update `PPO_TRAINING.md`:

- Change vector observations from `9` to `29`.
- Note that observations include normalized positions, relative player direction/distance, enemy/player velocity, health, DDA state, nearest player-bullet information, and arena-boundary distances.

Update `RL_IMPLEMENTATION.md`:

- Replace the old observation description with the 29-value movement-focused observation vector.
- Mention movement reward shaping.
- Mention that `TestEnemyScript` disables scripted movement when `EnemyAgent` is attached.

Update `PROJECT_SUMMARY.md`:

- Remove stale notes saying movement actions are not wired.
- State that PPO movement actions are applied to `Rigidbody2D.velocity`.
- State that old 9-observation ONNX models must be retrained.

## Step 10: Validate

Run:

```powershell
dotnet build adaptive-npc-rl.sln
```

Expected result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Then open Unity and run:

```text
Tools/RL/Configure Testing Scene
```

Check the training enemy:

- `Behavior Parameters` vector observation size is `29`.
- Continuous actions size is `2`.
- `Decision Requester` exists.
- `TestEnemyScript.useScriptedMovement` becomes disabled when `EnemyAgent` is present.

## Step 11: Retrain PPO

Because observation size changed from `9` to `29`, old trained models are incompatible.

Start training:

```powershell
mlagents-learn config/ppo_enemy_agent.yaml --run-id enemy_agent_ppo_movement_v2
```

When ML-Agents is listening, press Play in Unity.

After training, import the new ONNX model and assign it to the enemy `Behavior Parameters`.

## Expected Behavior After Reapplying

The enemy should learn movement around these incentives:

- Stay at useful firing/kiting range.
- Move laterally instead of standing still.
- Move away from incoming player bullets.
- Avoid arena edges.
- Avoid jittery direction changes.
- Survive long enough to keep firing.
- Use scripted shooting to hit the player while PPO decides where to move.

