# Repository Guidelines

## Project Structure & Module Organization

This is a Unity 2022.3.46f1 project for a 2D shooter with ML-Agents-based adaptive NPC training. Runtime C# scripts live in `Assets/Scripts/`, including gameplay controllers, enemy behaviors, `EnemyAgent.cs`, and `RLTrainingManager.cs`. Unity scenes are in `Assets/Scenes/`; prefabs are in `Assets/Prefab/`; sprites, animations, fonts, and materials are under `Assets/MainSprites/`, `Assets/Fonts/`, and `Assets/TextMesh Pro/`. Dependencies are tracked in `Packages/manifest.json`; Unity settings are in `ProjectSettings/`.

Do not commit generated Unity cache folders such as `Library/`, `Logs/`, or `UserSettings/`.

## Build, Test, and Development Commands

- Open locally: launch Unity Hub and open this repository with Unity `2022.3.46f1`.
- Restore packages: Unity resolves dependencies from `Packages/manifest.json` on project open.
- Run tests in batch mode:
  ```powershell
  Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -quit
  ```
- Build from the editor: use `File > Build Settings`, confirm scenes from `Assets/Scenes/`, then build for the target platform.

## Coding Style & Naming Conventions

Use Unity C# conventions: one `MonoBehaviour` or `Agent` class per file, with the file name matching the class name. Use PascalCase for classes and public methods, camelCase for private fields and locals, and `[Header]` or `[Tooltip]` where inspector clarity matters. Keep Unity event methods (`Awake`, `Start`, `Update`, `FixedUpdate`) focused and move reusable logic into named helper methods.

Prefer 4-space indentation. Avoid unrelated scene or prefab churn when changing scripts, since Unity asset serialization can create noisy diffs.

## Testing Guidelines

The Unity Test Framework package is installed, but no dedicated `Assets/Tests/` suite is currently present. Add Edit Mode tests under `Assets/Tests/EditMode/` for pure logic and Play Mode tests under `Assets/Tests/PlayMode/` for scene, physics, or training-loop behavior. Name test files after the system under test, for example `RLTrainingManagerTests.cs`.

Before opening a pull request, smoke-test affected scenes in the editor, especially `Assets/Scenes/Testing.unity` for ML-Agents changes.

## Commit & Pull Request Guidelines

Git history currently contains only `Initial commit`, so use clear imperative commit subjects such as `Add enemy reward reset logic` or `Fix player projectile collision`. Keep commits scoped to one behavior or asset change.

Pull requests should include a summary, changed scenes or prefabs, manual test notes, and screenshots or clips for visible gameplay/UI changes. Link related issues and call out ML-Agents training configuration changes.

## Agent-Specific Instructions

Preserve `.meta` files when moving or renaming Unity assets. For RL work, document observation, action, and reward changes in code comments or `PROJECT_SUMMARY.md` so training behavior remains reproducible.
