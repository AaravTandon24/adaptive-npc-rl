import pandas as pd
import numpy as np

easy = pd.read_csv('Logs/RLTrainingLogs/static_easy.csv')
hard = pd.read_csv('Logs/RLTrainingLogs/static_hard.csv')
rbd  = pd.read_csv('Logs/RLTrainingLogs/rule_based_dda.csv')

# Replace inf damage ratios with NaN for stats (inf = player took 0 damage)
for df in [easy, hard, rbd]:
    df['damage_ratio'] = df['damage_ratio'].replace([float('inf')], np.nan)

for name, df in [('StaticEasy', easy), ('StaticHard', hard), ('RuleBasedDDA', rbd)]:
    wins   = df[df['outcome'] == 'enemy_defeated']
    losses = df[df['outcome'] == 'player_defeated']
    win_rate = len(wins) / len(df)

    print(f"\n{'='*50}")
    print(f"  {name}  ({len(df)} episodes)")
    print(f"{'='*50}")
    print(f"  win rate:              {win_rate:.3f}  ({len(wins)}W / {len(losses)}L)")
    print(f"  survival_time:         mean={df['survival_time'].mean():.1f}s  "
          f"p25={df['survival_time'].quantile(0.25):.1f}  p75={df['survival_time'].quantile(0.75):.1f}")
    print(f"  player_final_hp:       mean={df['player_final_hp'].mean():.2f}  "
          f"p25={df['player_final_hp'].quantile(0.25):.1f}  p75={df['player_final_hp'].quantile(0.75):.1f}  "
          f"min={df['player_final_hp'].min():.1f}  max={df['player_final_hp'].max():.1f}")
    print(f"  damage_ratio (no inf): mean={df['damage_ratio'].mean():.2f}  "
          f"p25={df['damage_ratio'].quantile(0.25):.2f}  p75={df['damage_ratio'].quantile(0.75):.2f}")
    print(f"  enemy_accuracy:        mean={df['enemy_accuracy'].mean():.3f}")
    print(f"  rolling_win_rate:      mean={df['rolling_win_rate'].mean():.3f}  "
          f"p25={df['rolling_win_rate'].quantile(0.25):.2f}  p75={df['rolling_win_rate'].quantile(0.75):.2f}")
    if len(wins) > 0:
        print(f"  [WINS]  avg hp left: {wins['player_final_hp'].mean():.2f}  "
              f"avg ratio: {wins['damage_ratio'].mean():.2f}")
    if len(losses) > 0:
        print(f"  [LOSS]  avg hp left: {losses['player_final_hp'].mean():.2f}  "
              f"avg ratio: {losses['damage_ratio'].mean():.2f}")

print("\n\n--- Suggested FuzzyTierClassifier bounds (based on data) ---")
print("(These are starting points — paste the output above and I'll compute exact values)")
