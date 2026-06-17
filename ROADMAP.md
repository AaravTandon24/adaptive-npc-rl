# Adaptive NPC RL — Project Roadmap

> This document tracks remaining work. Completed phases (baseline data collection,
> rule-based DDA implementation, PPO enemy movement model v6, and FuzzyTierClassifier
> scaffolding) are omitted. Everything below represents pending or upcoming work.

---

## Phase 4 — Fuzzy Tier Classifier (Fixes & Validation)

The classifier structure exists but produces incorrect tier assignments on real data
(80% win-rate DDA run classifies the bot as Expert when it is empirically a Medium/Hard
player). The following targeted fixes must land before Phase 5 begins.

### 4.1 Fix Membership Function Thresholds

**Problem:** `SurvivalTime_Low` returns `0.0` for any survival time ≥ 15 s. Because the
win-tracking logic counts a victory as 60 s of survival, the rolling average survival time
is always far above 15 s even when the player is losing heavily. The AND-rule therefore
never fires for Tier DOWN.

**Fix — `WinRate` membership bounds:**

| Function | Current | Recommended |
| :--- | :--- | :--- |
| `WinRate_Low` — full membership below | `0.20` | `0.40` |
| `WinRate_Low` — zero above | `0.40` | `0.70` |
| `WinRate_High` — zero below | `0.60` | `0.70` |
| `WinRate_High` — full membership above | `0.80` | `0.90` |

### 4.2 Decouple Tier DOWN from Survival Time

Tier UP keeps the combined AND-rule (player must win *and* survive long).  
Tier DOWN should fire on win rate alone — a player losing repeatedly is struggling
regardless of how long each episode lasts.

```csharp
// FuzzyTierClassifier.cs — replace Tier DOWN rule
// OLD:  float tierDownStrength = Mathf.Min(wrLow, stLow);
// NEW:
float tierDownStrength = wrLow;   // win rate drives demotion; survival time stays for tier-up only
```

### 4.3 Fix DDA Controller — Win Penalty on Victories

`DanmakuDDAController.OnEpisodeEnd()` reduces difficulty when player HP < 35 %,
even on a *win*. This drives the difficulty down to 0.25–0.50, inflating the DDA win
rate to ~81 % instead of the target ~50 %, which in turn locks the fuzzy classifier
at Expert.

Make `lastEpisodeOutcome` accessible from `RLTrainingManager` (or pass it in) and
gate the HP-penalty on losses only:

```csharp
// DanmakuDDAController.cs
bool playerWon = (lastOutcome == "enemy_defeated");
if (currentPressure > maxTargetPressure
    || (!playerWon && playerState.healthPercent < 0.35f)
    || playerState.damageTakenPerSecond > 0.4f)
{
    desiredDifficulty -= 0.05f;
}
```

### 4.4 Re-run Baseline After Fixes

| Run | Target outcome | Episodes | Output file |
| :--- | :--- | :--- | :--- |
| Static Easy | Win rate ≈ 95–100 %, Tier → Expert | 500 | `static_easy.csv` (overwrite) |
| Static Hard | Win rate ≈ 0–5 %, Tier → Easy | 500 | `static_hard.csv` (overwrite) |
| Rule-Based DDA (fixed) | Win rate ≈ 45–55 %, Tier oscillates Medium/Hard | 500 | `rule_based_dda.csv` (overwrite) |

### 4.5 Update Unit Tests

Update `FuzzyTierClassifierTests.cs` to reflect the new threshold values and the
decoupled Tier DOWN rule. All existing transition assertions must pass.

---

## Phase 5 — DDA Meta-Controller (Core Research Contribution)

### 5.1 Design DDAAgent Observation Space (10 inputs)

| # | Observation | Range | Notes |
| :--- | :--- | :--- | :--- |
| 0 | `rollingWinRate` | 0–1 | 10-episode window |
| 1 | `challengeBalanceScore` | 0–1 | `1 - abs(winRate - 0.5)` |
| 2 | `avgSurvivalTime` | 0–1 | Normalised against episode time limit |
| 3 | `playerAccuracy` | 0–1 | Rolling 10-episode |
| 4 | `enemyAccuracy` | 0–1 | Rolling 10-episode |
| 5 | `avgPressure` | 0–1 | Rolling 10-episode |
| 6 | `currentDifficulty` | 0–1 | Live DDA controller value |
| 7 | `playerFinalHPNorm` | 0–1 | Rolling 10-episode average |
| 8 | `currentTierNorm` | 0–1 | Tier integer / 3 |
| 9 | `episodeProgressNorm` | 0–1 | Episodes done / total budget |

### 5.2 Design DDAAgent Action Space (4 continuous outputs)

Each action output is a delta adjustment, clamped to the current fuzzy tier's parameter
bounds before being applied to the scene.

| # | Action | Applied to |
| :--- | :--- | :--- |
| 0 | `Δ fireRateMultiplier` | `TestEnemyScript.fireRate` |
| 1 | `Δ bulletSpeedMultiplier` | `TestEnemyScript.bulletSpeed` |
| 2 | `Δ spreadAngleMultiplier` | `TestEnemyScript.spreadAngle` |
| 3 | `Δ enemySpeedMultiplier` | `TestEnemyScript.movementSpeed` |

Per-tier min/max bounds for each multiplier should be tuned via playtesting before
training begins and documented in a `DifficultyTierBounds.cs` or a Unity ScriptableObject.

### 5.3 Episode-Timescale Decision Making

`DDAAgent` makes **one decision per episode end**, not per frame. Wire the call inside
`RLTrainingManager.EndEpisode()` after `LogEpisodeStats()`:

```csharp
// RLTrainingManager.cs — after LogEpisodeStats()
if (_ddaAgent != null)
    _ddaAgent.RequestDecision();   // collects obs → action → applied next episode
```

### 5.4 Reward Function

```
R = w_cbs  × CBS                      // primary: keep CBS close to 1.0
  + w_sus  × sustainedBalanceBonus    // +bonus if CBS > 0.85 for ≥ 5 consecutive episodes
  - w_ext  × extremeWinRatePenalty    // -penalty if win rate < 0.1 or > 0.9
```

Suggested starting weights: `w_cbs = 1.0`, `w_sus = 0.3`, `w_ext = 0.5`.

### 5.5 Training Run

- **Algorithm:** PPO (via ML-Agents)
- **Duration:** 100 k – 200 k episodes
- **Config:** duplicate and adapt `config/ppo_enemy_agent_export_test.yaml`
- **Output:** `Logs/RLTrainingLogs/rl_dda_ppo.csv` (500 evaluation episodes after training)

---

## Phase 6 — Algorithm Comparison

Run identical 500-episode evaluation blocks after training each variant.

### 6.1 SAC Variant

- Implement or configure Soft Actor-Critic for the DDA meta-controller
- Train for the same episode budget as the PPO run
- Save evaluation log → `rl_dda_sac.csv`

### 6.2 Simpler Baseline Algorithm (DQN or A2C)

- Implement DQN (discretise actions into ±step increments) or A2C as a lighter
  comparison point
- Train for the same episode budget
- Save evaluation log → `rl_dda_dqn.csv` (or `rl_dda_a2c.csv`)

### 6.3 Comparison Matrix (target at end of Phase 6)

| Condition | CBS | Win Rate | Avg Survival | Player Accuracy | Notes |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Static Easy | — | — | — | — | baseline low |
| Static Hard | — | — | — | — | baseline high |
| Rule-Based DDA (fixed) | — | — | — | — | deterministic baseline |
| RL DDA — PPO | — | — | — | — | core contribution |
| RL DDA — SAC | — | — | — | — | comparison |
| RL DDA — DQN/A2C | — | — | — | — | comparison |

---

## Phase 7 — Full Results and Analysis

### 7.1 Statistical Comparison

Write or extend `analyze_baseline.py` to perform:

- **Pairwise t-tests** on CBS, survival time, player accuracy, damage ratio across all
  six conditions
- **Effect size** (Cohen's *d*) for CBS and win rate
- **Bonferroni correction** for multiple comparisons
- Report p-values and 95 % CIs in a Markdown table

### 7.2 Results Figures (generate with matplotlib or seaborn)

| Figure | X-axis | Y-axis / content |
| :--- | :--- | :--- |
| Rolling Win Rate Curves | Episode | Win rate (10-ep window), one line per condition |
| CBS Over Time | Episode | CBS value, shaded 95 % confidence band |
| Difficulty Parameter Trajectories | Episode | All 4 multipliers, faceted by condition |
| Survival Time Distribution | — | Violin or box plot, one per condition |
| Tier Assignment Over Time | Episode | Tier integer (0–3), stacked area chart |

### 7.3 Results Table

Fill in the comparison table in Phase 6.3 with real values and copy into the thesis /
report.

---

## Phase 8 — Validation (If Time Permits)

Work on the items below in the order listed. Each item is independent and can be dropped
if time runs out.

### 8.1 Human Study

- **Participants:** 15 – 20, within-subject design (each plays all three main conditions:
  Static Easy, Rule-Based DDA, RL DDA PPO)
- **Questionnaire:** Game Experience Questionnaire (GEQ) core module after each condition
- **Primary measure:** GEQ *Competence* + *Flow* subscales; secondary: perceived fairness
  open-response
- **Counterbalance** condition order to control for learning effects
- **Analysis:** Repeated-measures ANOVA on GEQ subscale scores

### 8.2 Multiple Movement Models

- Train dedicated PPO movement policies: **Aggressive**, **Kiting** (current v6),
  **Defensive** — each with a reward function tuned for that archetype
- Rank by empirical win rate against the player bot at `difficulty = 0.5`
- Integrate with the fuzzy tier selector: Easy tier → Defensive model, Hard/Expert tier →
  Aggressive model, Medium tier → Kiting (v6)
- Re-run the RL DDA PPO evaluation with the multi-model setup and save to
  `rl_dda_ppo_multimodel.csv`

### 8.3 VizDoom Secondary Validation

- Port DDAAgent observation/action design to a VizDoom environment
- Confirm CBS-based reward generalises beyond the Unity bullet-hell setting
- Document environment differences and any reward-shaping adjustments required

---

## Open Items & Known Issues

| Issue | Priority | Notes |
| :--- | :--- | :--- |
| Fuzzy classifier AND-rule locks bot in Expert under 80 % win rate | **High** | Fix in Phase 4.1–4.2 before any further data collection |
| DDA controller reduces difficulty on wins when player HP < 35 % | **High** | Fix in Phase 4.3 — inflates win rate and corrupts CBS target |
| `damage_ratio` shows `inf` for no-damage wins — needs capping | Medium | Already handled in `analyze_baseline.py`; cap in CSV writer too |
| Per-tier parameter bounds not yet playtested or documented | Medium | Must be done before Phase 5.2 training |
| `FuzzyTierClassifier` not yet wired to `DanmakuDDAController` | Medium | Phase 5.2 — DDAAgent output must be clamped to tier bounds |
| Unit tests do not cover new decoupled DOWN rule | Low | Update in Phase 4.5 |
