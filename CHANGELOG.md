# Changelog

## Unreleased

### Added

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

### Changed

- Updated `EnemyAgent` to read PPO continuous action outputs for horizontal and vertical movement instead of using zero movement values.
- Extended `EnemyAgent` observations with current DDA difficulty and pressure values when the controller is available.
- Updated `EnemyAgent` to configure ML-Agents `BehaviorParameters` and `DecisionRequester` automatically for the `EnemyAgent` behavior.
- Updated `TestEnemyScript` so difficulty can scale burst size from 3 bullets up to 6 bullets.
- Updated `MultiShooterScript` so difficulty can increase burst count for stronger bullet pressure.
- Updated enemy, wave, random spawn, projectile, and powerup scripts to consume `DifficultyProfile`.
- Updated player shooting and collision flows to report telemetry for shots fired, hits, damage, and powerup collection.
- Updated `Assets/Scenes/Testing.unity` so the enemy uses the trained PPO model with 9 vector observations and 2 continuous movement actions.

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

### Notes

- PPO training has been run for 50,000 steps using `config/ppo_enemy_agent_initial.yaml`.
- The trained model was exported to `results/enemy_agent_ppo_initial/EnemyAgent.onnx` and imported into `Assets/ML-Agents/Models/EnemyAgent.onnx`.
- The DDA system remains rule/controller-based; PPO currently controls enemy movement while DDA controls bullet pressure and difficulty tuning.
- Current verification has been done with `dotnet build adaptive-npc-rl.sln` and Unity batch import of the trained model.
