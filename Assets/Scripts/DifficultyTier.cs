/// <summary>
/// Difficulty tier used by <see cref="FuzzyTierClassifier"/> to represent the
/// current inferred skill level of the player.
/// Ordered from easiest (0) to hardest (3) so integer casting enables
/// safe tier-up/down arithmetic.
/// </summary>
public enum DifficultyTier
{
    Easy   = 0,
    Medium = 1,
    Hard   = 2,
    Expert = 3
}
