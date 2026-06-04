# PPO Training

## Unity Setup

Open `Assets/Scenes/Testing.unity` in Unity `2022.3.46f1`. The training enemy must have:

- `EnemyAgent`
- `Rigidbody2D`
- `Behavior Parameters`
- `Decision Requester`
- `TestEnemyHealthScript`

`EnemyAgent` now configures its behavior name, observation size, action size, and decision requester at runtime:

- behavior name: `EnemyAgent`
- vector observations: `9`
- continuous actions: `2`
- decision period: `5`

## Python Trainer Setup

Use a Python version supported by the installed Unity ML-Agents package, ideally Python `3.10`.

```powershell
py -3.10 -m venv .venv-mlagents
.\.venv-mlagents310\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -r requirements-mlagents.txt
```

## Training Command

Start the trainer:

```powershell
mlagents-learn config/ppo_enemy_agent.yaml --run-id enemy_agent_ppo_001
```

When the trainer prints that it is listening, press Play in Unity.

## Outputs

ML-Agents writes checkpoints and the trained model under:

```text
results/enemy_agent_ppo_001/
```

Episode metrics are written by `RLTrainingManager` under:

```text
Logs/RLTrainingLogs/enemy_agent_episodes.csv
```

After training, assign the generated `.onnx` model to the enemy's `Behavior Parameters` component for inference.
