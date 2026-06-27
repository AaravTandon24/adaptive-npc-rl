# Changelog

## Unreleased

## DDA Agent — Tuning Round 2 (Faster Response, Smooth Speed, Derived Bullet Count)

### Changed
- `extremeValuePenaltyWeight`: `0.2` → `0.08` — agent is less penalised for moving sliders away from `0.5`, allowing faster and more decisive difficulty changes.
- `bulletCountMultiplier` is no longer computed from `tierMidDifficulty`. It is now derived as `Lerp(1, 2, avgBulletPressure × bulletCountResponseScale)` where `avgBulletPressure` is the mean of the 3 bullet-behaviour normalized outputs (`fireRate`, `bulletSpeed`, `spreadAngle`). Bullet count now rises and falls proportionally to actual bullet behaviour without causing independent jarring jumps.
- Enemy speed (`enemySpeedMultiplier`) is now applied through a per-episode exponential smooth (`smoothedEnemySpeed`). The agent's raw target is stored as `currentNormalizedEnemySpeed` but only `enemySpeedSmoothRate` (default `0.25`) of the remaining gap is closed each episode, giving ~4 episodes of lag before a new target is fully reached.

### Added
- `bulletCountResponseScale` Inspector field (`[Range(0,1)]`, default `0.5`) — controls how strongly bullet count tracks the bullet-behaviour average.
- `enemySpeedSmoothRate` Inspector field (`[Range(0.05,1)]`, default `0.25`) — controls per-episode smoothing speed for enemy speed changes.
- `smoothedEnemySpeed` runtime field — visible in Inspector for debugging; shows the currently applied (lagged) enemy speed value vs. the agent's raw target.
- Debug log now shows `EnemySpeed(smoothed)=X (target=Y)` and `BulletCount=Z` every step.

## PPO v8 — Active Movement & Evasion (Batch A)

### Added
- Added `logThreatDiagnostics` toggle to `EnemyAgent.cs` (Inspector checkbox under **Threat Diagnostics** header). When enabled, prints each tracked threat's distance and TTI every `FixedUpdate`, and emits a `PERCEPTION GAP` warning for any bullet that is closer than the nearest tracked threat. Used to distinguish perception problems from reward-weighting problems before retuning. **Disable before retraining.**

### Changed
- `lateralMovementReward`: `0.005` → `0.02` (4×) — orbiting the player is now meaningfully profitable, breaking the centre-camping equilibrium.
- `idlePenalty`: `0.003` → `0.015` (5×) — standing still is now significantly punished, forcing the agent to stay in continuous motion.
- `dodgeReward`: `0.01` → `0.04` (4×) — moving away from an incoming bullet is now worth acting on, encouraging active evasion over passive tanking.

> **Note:** Batch B (`nearMissReward`, `dangerPenalty`, `maxDangerPenaltyPerStep`, `directionChangePenalty`, `beta`) is held back until post-v8 footage confirms Batch A alone was insufficient.


### Added

- Added `RlDDA` to the `TrainingCondition` enum in `RLTrainingManager.cs` so the 500-episode DDA PPO evaluation run logs to `rl_dda_ppo.csv` (distinct from `rule_based_dda.csv`). Added the corresponding `case` in `ApplyTrainingCondition()` — enables `DanmakuDDAController` while letting `DDAAgent` drive profiles via `ApplyAgentProfile()`.
- Added `GetLastEpisodePressure()` public getter on `RLTrainingManager` (returns `capturedPressure`, the per-episode average bullet-field pressure). Kept for future use as a DDAAgent observation when a retrain window is available.

### Fixed

- Fixed `DDAAgentTests.TestDeltaActionClamping` — renamed to `TestAbsoluteActionMapping` and rewrote assertions to match the current absolute action mapping (`(action + 1) / 2`). The old test was validating a delta-accumulation design that was removed to prevent multiplier drift; the assertions were wrong and would have failed.
- Fixed survival time inflation on wins in `RLTrainingManager.WriteEpisodeCsv()`. Previously a player win recorded `episodeTimeLimit` (60 s) regardless of actual fight length, which caused every win to saturate `SurvivalTime_High` in `FuzzyTierClassifier` and trigger spurious tier-UP transitions. Now always uses the actual `timeSurvived` value.
- Fixed `maxEpisodes` auto-stop in `RLTrainingManager.EndEpisode()` — the block was commented out, requiring manual Play button stop for the 500-episode eval run. It is now active: when `maxEpisodes > 0` and the limit is reached, the manager notifies both agents (so any in-flight PPO trajectory is finalised cleanly) then stops Play mode in the editor or quits the built player.

### Added

- Added `DifficultyTier.cs` — standalone `public enum DifficultyTier { Easy = 0, Medium = 1, Hard = 2, Expert = 3 }` so the type is available to all scripts without namespace friction.
- Added `FuzzyTierClassifier.cs` — new `MonoBehaviour` that classifies the player into one of four difficulty tiers using fuzzy logic membership functions evaluated at episode end.
  - Inputs: `rollingWinRate` (0–1), `playerFinalHP` (0–100), `damageRatio` (0+).
  - Nine membership functions (Low / Medium / High for each input) using shoulder and triangular shapes.
  - Tier-UP rule: `min(winRate_High, playerHP_High, damageRatio_High) > tierUpMembership`.
  - Tier-DOWN rule: `min(winRate_Low, playerHP_Low, damageRatio_Low) > tierDownMembership`.
  - Guards: 10-episode warm-up + 5-episode cooldown between changes; tier clamped to `[Easy, Expert]`.
  - All thresholds are Inspector-tweakable via `[Header]` / `[Tooltip]` fields.
  - Public method `DifficultyTier Evaluate(float, float, float, int)` — evaluates and returns the tier without applying difficulty parameters.
  - Not yet connected to `DanmakuDDAController`.
- Updated `RLTrainingManager.cs` to wire up `FuzzyTierClassifier`:
  - Added `[SerializeField] private FuzzyTierClassifier _tierClassifier;` under a new **"Fuzzy Tier Classifier"** Inspector header.
  - `WriteEpisodeCsv()` now calls `_tierClassifier.Evaluate()` at episode end using `rollingWinRate`, `playerFinalHP`, and `damageRatio` (∞ clamped to 99). Falls back to `DifficultyTier.Medium` with a warning if the field is not assigned.
  - Added `current_tier` as the last CSV column (integer: 0 = Easy, 1 = Medium, 2 = Hard, 3 = Expert), appended after `challenge_balance_score`.
  - `LogEpisodeStats()` logs `[FuzzyTier] Episode N: tier = <Name> (<int>)` after each episode.
- Added `DanmakuDDAController` for runtime dynamic difficulty adjustment based on player performance and bullet-field pressure.
- Added `BulletPressureAnalyzer` to estimate bullet-hell danger from active bullets, proximity to the player, projected collision risk, and near misses.
- Added `PlayerPerformanceTelemetry` to track player health, damage trends, shots fired, shots hit, hit rate, powerups collected, and near misses.
- Added `DifficultyProfile` and `IDifficultyTunable` so enemies, spawners, projectiles, and powerups can respond to difficulty changes consistently.
- Added dynamic scaling for enemy fire rate, bullet speed, spread angle, enemy movement speed, spawn interval, powerup cadence, max active bullets, and bullets per burst.
- Added auto-bootstrap behavior so the DDA controller can be created at runtime if not manually placed in a scene.
- Added `AGENTS.md` as a contributor guide for the Unity project.
- Added `RL_IMPLEMENTATION.md` documenting the current ML-Agents setup, reward design, episode manager, and remaining RL training tasks.
- Added `PPO_TRAINING.md` with setup, training, import, and verification commands for Unity ML-Agents.
- Added PPO trainer configs under `config/`, including quick export testing, initial 50k-step training, and longer training variants.
- Added `requirements-mlagents.txt` for the Python ML-Agents environment.
- Added `RLTrainingSceneSetup` editor tooling to configure the Testing scene, build a training player, and import the trained PPO model.
- Added trained `EnemyAgent.onnx` model asset under `Assets/ML-Agents/Models/`.
- Added CSV episode logging for reward, damage, survival, pressure, difficulty, and outcome metrics under `Logs/RLTrainingLogs/`.
- Added graze (near miss) reward system to `EnemyAgent.cs` for dodging incoming projectiles.
- Added shaped continuous kiting range rewards to `EnemyAgent.cs` for smooth kiting target gradients.
- Added progressive survival step-rewards scaling with episode duration to `EnemyAgent.cs`.
- Added randomized speed capability scaling (`0.2f - 1.0f`) during training resets to cover the speed range.
- Added model opset conversion utility `convert_model.py` to down-convert ONNX models to opset 15 and embed weights.
- Added `extremeValuePenaltyWeight` to `DDAAgent.cs` to penalize extreme outputs (0 or 1), forcing the agent to use moderate difficulty tweaks rather than cheesing the player bot.

### Changed

- Updated `FuzzyTierClassifier.cs` to use two inputs (`rollingWinRate` and `avgSurvivalTime`) instead of three, removing `damageRatio` and `playerFinalHP` until data quality issues are resolved.
- Refactored `FuzzyTierClassifier` membership functions and transition rules to use the updated input criteria (threshold > 0.6).
- Updated `RLTrainingManager.cs` to calculate and track the rolling average survival time over the last 10 episodes and pass it to the updated `Evaluate()` signature.
- Updated `EnemyAgent` to read PPO continuous action outputs for horizontal and vertical movement instead of using zero movement values.
- Extended `EnemyAgent` observations with current DDA difficulty and pressure values when the controller is available.
- Updated `EnemyAgent` to configure ML-Agents `BehaviorParameters` and `DecisionRequester` automatically for the `EnemyAgent` behavior.
- Updated `TestEnemyScript` so difficulty can scale burst size from 3 bullets up to 6 bullets.
- Updated `MultiShooterScript` so difficulty can increase burst count for stronger bullet pressure.
- Updated enemy, wave, random spawn, projectile, and powerup scripts to consume `DifficultyProfile`.
- Updated player shooting and collision flows to report telemetry for shots fired, hits, damage, and powerup collection.
- Updated `Assets/Scenes/Testing.unity` so the enemy uses the trained PPO model with 9 vector observations and 2 continuous movement actions.
- Capped total episode damage penalty to `-0.5f` in `EnemyAgent.cs` to prevent drowning out learning signals.
- Removed short-episode penalty in `EnemyAgent.cs` to prevent double-punishment.
- Updated `Testing.unity` scene to pre-configure Behavior Type to `Inference Only` and Vector Observation Size to `29`.
- Converted `DDAAgent.cs` continuous action space from relative deltas to absolute normalized values. This prevents difficulty parameters from permanently drifting and locking at 0 or 1.
- Increased `dodgeTriggerRadius` in `RLPlayerBot.cs` from `1.8f` to `2.5f` to prevent the DDA agent from exploiting the bot's pathfinding with slow bullet walls.

### Fixed

- Fixed `RLTrainingManager` episode metrics so player damage dealt and enemy damage dealt are actually counted.
- Fixed test-enemy player bullet hits to report player damage dealt to `RLTrainingManager`.
- Fixed enemy projectile hits to report enemy damage dealt to `RLTrainingManager`.
- Fixed enemy projectile hits to call `EnemyAgent.RewardForHit()`.
- Fixed test enemy burst scaling so it no longer starts at 6 bullets by default; it now starts at 3 and increases with difficulty.
- Fixed ML-Agents episode completion by having `RLTrainingManager` call `EnemyAgent.EndEpisode()` before resetting the episode.
- Fixed PPO training scene setup so the enemy has the required `Rigidbody2D`, `EnemyAgent`, `BehaviorParameters`, and `DecisionRequester` components.
- Fixed trained-model import so rerunning the import replaces the existing `EnemyAgent.onnx` instead of failing when the file already exists.
- Fixed ONNX export execution by running training with UTF-8 console output and installing the missing exporter dependency.
- Fixed Unity Sentis model loading crashes (`NullReferenceException` on constant loading) by down-converting exported ONNX model (`v6`) to opset 15.
- Fixed `convert_model.py` to sort checkpoints by file modification time instead of parsed step number, ensuring the correct newest model is picked when restarting training under the same run ID.

### Notes

- PPO training has been run for 50,000 steps using `config/ppo_enemy_agent_initial.yaml`.
- The trained model was exported to `results/enemy_agent_ppo_initial/EnemyAgent.onnx` and imported into `Assets/ML-Agents/Models/EnemyAgent.onnx`.
- The DDA system remains rule/controller-based; PPO currently controls enemy movement while DDA controls bullet pressure and difficulty tuning.
- Current verification has been done with `dotnet build adaptive-npc-rl.sln` and Unity batch import of the trained model.
- Deployed trained PPO model `v6` (`EnemyAgent_v6.onnx`) after completing 341,000 training steps and achieving positive mean rewards of ~`+0.9`.
- Deployed trained PPO model `v7` (`EnemyAgent_v7.onnx`) after completing 639,164 training steps with enhanced threat-awareness observations and danger-zone reward shaping.

## PPO v7 — Enhanced Threat Awareness

### Added
- **Multi-threat observations**: Track top-3 bullet threats sorted by ascending time-to-impact (5 obs per threat × 3 = 15 total), replacing the single nearest-bullet block (6 obs) and bullet count (1 obs).
- **Time-to-impact observation**: Each threat slot includes a normalized TTI value (`dist / closingSpeed / maxRelevantTime`) — encodes urgency directly instead of forcing the network to infer from raw distance and velocity.
- **`BulletThreat` struct** and `GetTopThreats(Vector2, int)` helper method for efficient sorted threat extraction from the cached bullet array.
- **`DistanceToLineSegment(Vector2, Vector2, Vector2)`** helper for computing perpendicular distance from a point to a projected bullet path segment.
- **Danger-zone continuous penalty**: Per-step penalty for being inside the projected path of any bullet (`predictionWindow = 0.3s`, `dangerRadius = 1.5f`), proportional to proximity and capped at `−0.03f` per step.
- Re-added **`currentSpeedScalar`** as an observation (1 obs) — the network can now correlate its own speed with dodge urgency.
- New Inspector fields: `maxRelevantTime`, `threatCount`, `dangerRadius`, `predictionWindow`, `dangerPenalty`, `maxDangerPenaltyPerStep`.

### Changed
- **Observation space**: 29 → 38 observations.
- **PPO network**: `hidden_units` increased from 256 → 320 to accommodate the richer input.
- **Scene config**: `VectorObservationSize` updated to 38 in `Testing.unity`.
- `convert_model.py` rewritten to handle ML-Agents' external data file layout and auto-resolve the latest checkpoint.
