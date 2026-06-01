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

### Changed

- Updated `EnemyAgent` to read PPO continuous action outputs for horizontal and vertical movement instead of using zero movement values.
- Extended `EnemyAgent` observations with current DDA difficulty and pressure values when the controller is available.
- Updated `TestEnemyScript` so difficulty can scale burst size from 3 bullets up to 6 bullets.
- Updated `MultiShooterScript` so difficulty can increase burst count for stronger bullet pressure.
- Updated enemy, wave, random spawn, projectile, and powerup scripts to consume `DifficultyProfile`.
- Updated player shooting and collision flows to report telemetry for shots fired, hits, damage, and powerup collection.

### Fixed

- Fixed `RLTrainingManager` episode metrics so player damage dealt and enemy damage dealt are actually counted.
- Fixed test-enemy player bullet hits to report player damage dealt to `RLTrainingManager`.
- Fixed enemy projectile hits to report enemy damage dealt to `RLTrainingManager`.
- Fixed enemy projectile hits to call `EnemyAgent.RewardForHit()`.
- Fixed test enemy burst scaling so it no longer starts at 6 bullets by default; it now starts at 3 and increases with difficulty.

### Notes

- The DDA system is currently rule/controller-based, not PPO-trained.
- PPO movement action wiring is now present, but a trained PPO model still requires Unity ML-Agents `Behavior Parameters`, a `DecisionRequester`, a training YAML, training runs, and model assignment.
- Current verification has been done with `dotnet build adaptive-npc-rl.sln`.
