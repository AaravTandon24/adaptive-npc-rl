using UnityEngine;

public class FuzzyLogicTester : MonoBehaviour
{
#if UNITY_EDITOR
    private void Start()
    {
        RunTests();
    }
#endif

    public static void RunTests()
    {
        Debug.Log("==================================================");
        Debug.Log("[FuzzyLogicTester] Starting self-test...");
        Debug.Log("==================================================");

        // We create a temporary GameObject to hold the component
        GameObject go = new GameObject("TempClassifier");
        FuzzyTierClassifier classifier = go.AddComponent<FuzzyTierClassifier>();

        classifier.minEpisodesBeforeFirstChange = 0;
        classifier.episodeCooldownBetweenChanges = 0;

        int passes = 0;
        int total = 0;

        void AssertEqual(DifficultyTier expected, DifficultyTier actual, string message)
        {
            total++;
            if (expected == actual)
            {
                passes++;
                Debug.Log($"[PASS] {message} (Expected: {expected}, Got: {actual})");
            }
            else
            {
                Debug.LogError($"[FAIL] {message} (Expected: {expected}, Got: {actual})");
            }
        }

        // Test initial state
        AssertEqual(DifficultyTier.Medium, classifier.CurrentTier, "Initial state should be Medium");

        // Case 1: Tier UP
        DifficultyTier tier = classifier.Evaluate(0.9f, 50f, 1);
        AssertEqual(DifficultyTier.Hard, tier, "0.9 WR & 50s survival should Tier UP to Hard");

        // Case 2: Tier UP again to Expert
        tier = classifier.Evaluate(0.9f, 50f, 2);
        AssertEqual(DifficultyTier.Expert, tier, "0.9 WR & 50s survival should Tier UP to Expert");

        // Case 3: Clamping at Expert
        tier = classifier.Evaluate(0.9f, 50f, 3);
        AssertEqual(DifficultyTier.Expert, tier, "Should clamp at Expert");

        // Case 4: Tier DOWN — win rate alone drives demotion
        // Survival time is HIGH (45 s) to confirm stLow is no longer required.
        // winRate_Low(0.1) = 1.0 > 0.6 => should Tier DOWN even though survival is long.
        tier = classifier.Evaluate(0.1f, 45f, 4);
        AssertEqual(DifficultyTier.Hard, tier, "0.1 WR with HIGH survival (45s) should still Tier DOWN to Hard");

        tier = classifier.Evaluate(0.1f, 45f, 5);
        AssertEqual(DifficultyTier.Medium, tier, "0.1 WR should Tier DOWN to Medium regardless of survival time");

        tier = classifier.Evaluate(0.1f, 45f, 6);
        AssertEqual(DifficultyTier.Easy, tier, "0.1 WR should Tier DOWN to Easy regardless of survival time");

        // Case 5: Clamping at Easy
        tier = classifier.Evaluate(0.1f, 5f, 7);
        AssertEqual(DifficultyTier.Easy, tier, "Should clamp at Easy");

        // Case 6: Near threshold but no change
        tier = classifier.Evaluate(0.9f, 50f, 8);
        AssertEqual(DifficultyTier.Medium, tier, "Reset to Medium");

        // winRate_High(0.7) = 0.5
        // survivalTime_High(30) = (30-25)/15 = 0.333
        // min(0.5, 0.333) = 0.333 <= 0.6 => should NOT tier UP
        tier = classifier.Evaluate(0.7f, 30f, 9);
        AssertEqual(DifficultyTier.Medium, tier, "Below threshold should not trigger Tier UP");

        // Clean up
        Object.DestroyImmediate(go);

        Debug.Log("==================================================");
        Debug.Log($"[FuzzyLogicTester] Tests finished. Passed: {passes}/{total}");
        Debug.Log("==================================================");
    }
}
