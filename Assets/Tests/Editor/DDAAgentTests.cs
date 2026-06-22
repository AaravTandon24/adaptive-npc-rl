using NUnit.Framework;
using UnityEngine;

public class DDAAgentTests
{
    [Test]
    public void TestGetBoundsForTier()
    {
        // --- Easy Tier Bounds ---
        var easy = DifficultyProfile.GetBoundsForTier(DifficultyTier.Easy);
        Assert.AreEqual(0.550f, easy.fireRateMin, 0.001f);
        Assert.AreEqual(0.825f, easy.fireRateMax, 0.001f);
        Assert.AreEqual(0.650f, easy.bulletSpeedMin, 0.001f);
        Assert.AreEqual(0.850f, easy.bulletSpeedMax, 0.001f);
        Assert.AreEqual(0.750f, easy.spreadAngleMin, 0.001f);
        Assert.AreEqual(0.900f, easy.spreadAngleMax, 0.001f);
        Assert.AreEqual(0.750f, easy.enemySpeedMin, 0.001f);
        Assert.AreEqual(0.900f, easy.enemySpeedMax, 0.001f);

        // --- Medium Tier Bounds ---
        var medium = DifficultyProfile.GetBoundsForTier(DifficultyTier.Medium);
        Assert.AreEqual(0.825f, medium.fireRateMin, 0.001f);
        Assert.AreEqual(1.100f, medium.fireRateMax, 0.001f);
        Assert.AreEqual(0.850f, medium.bulletSpeedMin, 0.001f);
        Assert.AreEqual(1.050f, medium.bulletSpeedMax, 0.001f);
        Assert.AreEqual(0.900f, medium.spreadAngleMin, 0.001f);
        Assert.AreEqual(1.050f, medium.spreadAngleMax, 0.001f);
        Assert.AreEqual(0.900f, medium.enemySpeedMin, 0.001f);
        Assert.AreEqual(1.050f, medium.enemySpeedMax, 0.001f);

        // --- Hard Tier Bounds ---
        var hard = DifficultyProfile.GetBoundsForTier(DifficultyTier.Hard);
        Assert.AreEqual(1.100f, hard.fireRateMin, 0.001f);
        Assert.AreEqual(1.375f, hard.fireRateMax, 0.001f);
        Assert.AreEqual(1.050f, hard.bulletSpeedMin, 0.001f);
        Assert.AreEqual(1.250f, hard.bulletSpeedMax, 0.001f);
        Assert.AreEqual(1.050f, hard.spreadAngleMin, 0.001f);
        Assert.AreEqual(1.200f, hard.spreadAngleMax, 0.001f);
        Assert.AreEqual(1.050f, hard.enemySpeedMin, 0.001f);
        Assert.AreEqual(1.200f, hard.enemySpeedMax, 0.001f);

        // --- Expert Tier Bounds ---
        var expert = DifficultyProfile.GetBoundsForTier(DifficultyTier.Expert);
        Assert.AreEqual(1.375f, expert.fireRateMin, 0.001f);
        Assert.AreEqual(1.650f, expert.fireRateMax, 0.001f);
        Assert.AreEqual(1.250f, expert.bulletSpeedMin, 0.001f);
        Assert.AreEqual(1.450f, expert.bulletSpeedMax, 0.001f);
        Assert.AreEqual(1.200f, expert.spreadAngleMin, 0.001f);
        Assert.AreEqual(1.350f, expert.spreadAngleMax, 0.001f);
        Assert.AreEqual(1.200f, expert.enemySpeedMin, 0.001f);
        Assert.AreEqual(1.350f, expert.enemySpeedMax, 0.001f);
    }

    [Test]
    public void TestApplyAgentProfileBypass()
    {
        GameObject go = new GameObject("TestDDAController");
        var controller = go.AddComponent<DanmakuDDAController>();

        DifficultyProfile profile = new DifficultyProfile
        {
            fireRateMultiplier = 3.0f, // will be clamped to 2.0
            bulletSpeedMultiplier = 1.2f,
            spreadAngleMultiplier = 0.9f,
            enemySpeedMultiplier = 1.1f,
            spawnIntervalMultiplier = 1.0f,
            powerupSpawnMultiplier = 1.0f,
            bulletCountMultiplier = 1.5f,
            maxActiveEnemyBullets = 150
        };

        controller.ApplyAgentProfile(profile);

        Assert.AreEqual(2.0f, controller.CurrentProfile.fireRateMultiplier, 0.001f);
        Assert.AreEqual(1.2f, controller.CurrentProfile.bulletSpeedMultiplier, 0.001f);
        Assert.AreEqual(0.9f, controller.CurrentProfile.spreadAngleMultiplier, 0.001f);
        Assert.AreEqual(1.1f, controller.CurrentProfile.enemySpeedMultiplier, 0.001f);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void TestDeltaActionClamping()
    {
        GameObject go = new GameObject("TestDDAAgent");
        var ddaAgent = go.AddComponent<DDAAgent>();
        
        // Mock maxStepChange and initial state
        ddaAgent.maxStepChange = 0.2f;
        ddaAgent.currentNormalizedFireRate = 0.5f;

        // Simulate ActionBuffers with action = +1f (increase)
        var actions = new Unity.MLAgents.Actuators.ActionBuffers(
            new float[] { 1.0f, 0.0f, 0.0f, 0.0f },
            new int[] { }
        );

        ddaAgent.OnActionReceived(actions);

        // Normalized fire rate should increase by 1.0 * maxStepChange (0.2) -> 0.7
        Assert.AreEqual(0.7f, ddaAgent.currentNormalizedFireRate, 0.001f);

        // Apply action = +1f again -> 0.9
        ddaAgent.OnActionReceived(actions);
        Assert.AreEqual(0.9f, ddaAgent.currentNormalizedFireRate, 0.001f);

        // Apply action = +1f again -> 1.0 (clamped)
        ddaAgent.OnActionReceived(actions);
        Assert.AreEqual(1.0f, ddaAgent.currentNormalizedFireRate, 0.001f);

        // Apply action = -1f -> 0.8
        var decreaseActions = new Unity.MLAgents.Actuators.ActionBuffers(
            new float[] { -1.0f, 0.0f, 0.0f, 0.0f },
            new int[] { }
        );
        ddaAgent.OnActionReceived(decreaseActions);
        Assert.AreEqual(0.8f, ddaAgent.currentNormalizedFireRate, 0.001f);

        Object.DestroyImmediate(go);
    }
}
