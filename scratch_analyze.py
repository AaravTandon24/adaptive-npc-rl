import pandas as pd
import numpy as np

def win_rate_low(wr, low_cutoff=0.2, med_start=0.4):
    if wr <= low_cutoff: return 1.0
    if wr >= med_start: return 0.0
    return 1.0 - (wr - low_cutoff) / (med_start - low_cutoff)

def win_rate_high(wr, high_start=0.6, high_cutoff=0.8):
    if wr <= high_start: return 0.0
    if wr >= high_cutoff: return 1.0
    return (wr - high_start) / (high_cutoff - high_start)

def survival_time_low(st, low_cutoff=8.0, med_start=15.0):
    if st <= low_cutoff: return 1.0
    if st >= med_start: return 0.0
    return 1.0 - (st - low_cutoff) / (med_start - low_cutoff)

def survival_time_high(st, high_start=25.0, high_cutoff=40.0):
    if st <= high_start: return 0.0
    if st >= high_cutoff: return 1.0
    return (st - high_start) / (high_cutoff - high_start)

# Simulate current fuzzy classifier logic
def evaluate_current(rolling_wr, avg_st):
    wr_high = win_rate_high(rolling_wr)
    st_high = survival_time_high(avg_st)
    wr_low = win_rate_low(rolling_wr)
    st_low = survival_time_low(avg_st)
    
    up_strength = min(wr_high, st_high)
    down_strength = min(wr_low, st_low)
    return up_strength, down_strength

def evaluate_custom(rolling_wr, avg_st, wr_low_c, wr_med_s, wr_high_s, wr_high_c, st_low_c, st_med_s, st_high_s, st_high_c, rule_type=0):
    wr_high = win_rate_high(rolling_wr, wr_high_s, wr_high_c)
    st_high = survival_time_high(avg_st, st_high_s, st_high_c)
    wr_low = win_rate_low(rolling_wr, wr_low_c, wr_med_s)
    st_low = survival_time_low(avg_st, st_low_c, st_med_s)
    
    if rule_type == 0:
        # AND: min(wr, st)
        up_strength = min(wr_high, st_high)
        down_strength = min(wr_low, st_low)
    elif rule_type == 1:
        # DOWN is just wr_low
        up_strength = min(wr_high, st_high)
        down_strength = wr_low
    elif rule_type == 2:
        # DOWN is OR: max(wr_low, st_low)
        up_strength = min(wr_high, st_high)
        down_strength = max(wr_low, st_low)
    elif rule_type == 3:
        # Win rate only
        up_strength = wr_high
        down_strength = wr_low
        
    return up_strength, down_strength

# Load the data
easy = pd.read_csv('Logs/RLTrainingLogs/static_easy.csv')
hard = pd.read_csv('Logs/RLTrainingLogs/static_hard.csv')
rbd = pd.read_csv('Logs/RLTrainingLogs/rule_based_dda.csv')

def simulate_with_params(df, name, params, rule_type=0):
    outcomes = df['outcome'].eq('enemy_defeated').astype(int).tolist()
    raw_st = df['survival_time'].tolist()
    
    rolling_wr = []
    avg_st = []
    
    for i in range(len(df)):
        start_idx = max(0, i - 9)
        window_outcomes = outcomes[start_idx:i+1]
        
        window_tracked_st = []
        for j in range(start_idx, i+1):
            if df.iloc[j]['outcome'] == 'enemy_defeated':
                window_tracked_st.append(60.0)
            else:
                window_tracked_st.append(df.iloc[j]['survival_time'])
                
        rolling_wr.append(np.mean(window_outcomes))
        avg_st.append(np.mean(window_tracked_st))
        
    current_tier = 1 # Start at Medium
    tier_history = [current_tier]
    last_change = -1
    up_count = 0
    down_count = 0
    
    for idx in range(len(df)):
        episode = idx + 1
        if episode < 10:
            tier_history.append(current_tier)
            continue
            
        if last_change >= 0 and episode - last_change < 5:
            tier_history.append(current_tier)
            continue
            
        wr = rolling_wr[idx]
        st = avg_st[idx]
        
        up_s, down_s = evaluate_custom(wr, st, *params, rule_type)
        
        if up_s > 0.6 and current_tier < 3:
            current_tier += 1
            last_change = episode
            up_count += 1
        elif down_s > 0.6 and current_tier > 0:
            current_tier -= 1
            last_change = episode
            down_count += 1
            
        tier_history.append(current_tier)
        
    return current_tier, up_count, down_count, pd.Series(tier_history).value_counts().to_dict()

# Let's test a few candidate parameters and rule types:
# format: wr_low_c, wr_med_s, wr_high_s, wr_high_c, st_low_c, st_med_s, st_high_s, st_high_c
candidates = [
    # Candidate 0: Original
    ((0.2, 0.4, 0.6, 0.8, 8.0, 15.0, 25.0, 40.0), 0),
    # Candidate 6: High survival low bounds (AND rule)
    ((0.3, 0.5, 0.6, 0.8, 35.0, 55.0, 25.0, 40.0), 0),
    # Candidate 7: DOWN based on WR only (st only for UP)
    ((0.3, 0.5, 0.6, 0.8, 8.0, 15.0, 25.0, 40.0), 1),
    # Candidate 8: DOWN based on OR
    ((0.3, 0.5, 0.6, 0.8, 8.0, 15.0, 25.0, 40.0), 2),
    # Candidate 9: Win rate only for both UP and DOWN
    ((0.3, 0.5, 0.6, 0.8, 8.0, 15.0, 25.0, 40.0), 3),
    # Candidate 10: Higher low WR threshold (low=0.4, med=0.7) and WR only for DOWN
    ((0.4, 0.7, 0.6, 0.8, 8.0, 15.0, 25.0, 40.0), 1),
    # Candidate 11: Candidate 10 but with AND rule (needs higher survival low threshold as well)
    ((0.4, 0.7, 0.6, 0.8, 20.0, 45.0, 25.0, 40.0), 0),
    # Candidate 12: Symmetric win rate only (UP: 0.7-0.9, DOWN: 0.4-0.7)
    ((0.4, 0.7, 0.7, 0.9, 8.0, 15.0, 25.0, 40.0), 3),
]

for idx, (cand, r_type) in enumerate(candidates):
    print(f"\n--- Candidate {idx} (Params: {cand}, Rule Type: {r_type}) ---")
    for df, name in [(easy, "Static Easy"), (hard, "Static Hard"), (rbd, "Rule Based DDA")]:
        final_t, ups, downs, dist = simulate_with_params(df, name, cand, r_type)
        print(f"  {name:15s} | Final: {final_t} | UP: {ups:2d} | DOWN: {downs:2d} | Distribution: {dist}")




