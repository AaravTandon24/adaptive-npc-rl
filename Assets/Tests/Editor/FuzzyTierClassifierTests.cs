using NUnit.Framework;
using UnityEngine;

public class FuzzyTierClassifierTests
{
    [Test]
    public void TestFuzzyLogicTransitions()
    {
        // Arrange
        GameObject go = new GameObject("TestClassifier");
        FuzzyTierClassifier classifier = go.AddComponent<FuzzyTierClassifier>();

        // Set cooldowns to 0 so we can test step-by-step state changes immediately
        classifier.minEpisodesBeforeFirstChange = 0;
        classifier.episodeCooldownBetweenChanges = 0;

        // Verify initial state
        Assert.AreEqual(DifficultyTier.Medium, classifier.CurrentTier, "Initial tier should be Medium");

        // --- CASE 1: Tier UP conditions ---
        // winRate = 0.9f (winRate_High = 1.0f)
        // Player HP = 1.0f (PlayerHP_High = 1.0f)
        // min(1.0, 1.0) = 1.0 > 0.6 => should tier UP
        DifficultyTier tier = classifier.Evaluate(0.9f, 50f, 1.0f, 1);
        Assert.AreEqual(DifficultyTier.Hard, tier, "High winRate & Player HP should trigger Tier UP to Hard");

        // --- CASE 2: Tier UP again to Expert ---
        tier = classifier.Evaluate(0.9f, 50f, 1.0f, 2);
        Assert.AreEqual(DifficultyTier.Expert, tier, "High winRate & Player HP again should trigger Tier UP to Expert");

        // --- CASE 3: Clamping at Expert ---
        tier = classifier.Evaluate(0.9f, 50f, 1.0f, 3);
        Assert.AreEqual(DifficultyTier.Expert, tier, "Tier should be capped at Expert");

        // --- CASE 4: Tier DOWN conditions ---
        // winRate_Low(0.1) = 1.0 > 0.6 => should tier DOWN.
        // HP is low (0.0f) and survival time is HIGH (45 s).
        // Tier DOWN must fire on win rate alone regardless of how long the player survives or health.
        tier = classifier.Evaluate(0.1f, 45f, 0.0f, 4);
        Assert.AreEqual(DifficultyTier.Hard, tier, "Low winRate with HIGH survival (45s) should still trigger Tier DOWN to Hard");

        tier = classifier.Evaluate(0.1f, 45f, 0.0f, 5);
        Assert.AreEqual(DifficultyTier.Medium, tier, "Low winRate should trigger Tier DOWN to Medium regardless of survival time");

        tier = classifier.Evaluate(0.1f, 45f, 0.0f, 6);
        Assert.AreEqual(DifficultyTier.Easy, tier, "Low winRate should trigger Tier DOWN to Easy regardless of survival time");

        // --- CASE 5: Clamping at Easy ---
        tier = classifier.Evaluate(0.1f, 45f, 0.0f, 7);
        Assert.AreEqual(DifficultyTier.Easy, tier, "Tier should be capped at Easy");

        // --- CASE 6: Near-threshold check (boundary condition) ---
        // Reset tier back to Medium by doing two tier-ups.
        classifier.Evaluate(0.9f, 50f, 1.0f, 8);
        tier = classifier.Evaluate(0.9f, 50f, 1.0f, 9);
        Assert.AreEqual(DifficultyTier.Medium, tier, "Tier should be reset to Medium for boundary check");

        // winRate = 0.75f — winRate_High(0.75) = 0.25 (new bounds: 0.7-0.9)
        // Player HP = 0.5f — PlayerHP_High = 0.25
        // min(0.25, 0.25) = 0.25 <= 0.6 => should NOT tier UP
        tier = classifier.Evaluate(0.75f, 30f, 0.5f, 10);
        Assert.AreEqual(DifficultyTier.Medium, tier, "winRate=0.75 below new High threshold should not trigger Tier UP");

        // Clean up
        Object.DestroyImmediate(go);
    }
}
