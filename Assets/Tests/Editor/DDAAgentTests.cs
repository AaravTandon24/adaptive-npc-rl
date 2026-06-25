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

    /// <summary>
    /// Verifies the absolute action mapping used by DDAAgent.OnActionReceived.
    ///
    /// DDAAgent converts continuous actions from [-1, 1] to a normalised [0, 1]
    /// value via  normalised = (action + 1) / 2, then writes that directly as the
    /// current multiplier — there is NO delta accumulation.
    ///
    /// Replaces the old TestDeltaActionClamping which tested an earlier delta-based
    /// design that was removed to prevent multiplier drift.
    /// </summary>
    [Test]
    public void TestAbsoluteActionMapping()
    {
        GameObject go = new GameObject("TestDDAAgent");
        var ddaAgent = go.AddComponent<DDAAgent>();

        // action = +1.0 → (1.0 + 1) / 2 = 1.0
        var maxActions = new Unity.MLAgents.Actuators.ActionBuffers(
            new float[] { 1.0f, 1.0f, 1.0f, 1.0f },
            new int[] { }
        );
        ddaAgent.OnActionReceived(maxActions);
        Assert.AreEqual(1.0f, ddaAgent.currentNormalizedFireRate,    0.001f, "FireRate should be 1.0 for action +1");
        Assert.AreEqual(1.0f, ddaAgent.currentNormalizedBulletSpeed, 0.001f, "BulletSpeed should be 1.0 for action +1");
        Assert.AreEqual(1.0f, ddaAgent.currentNormalizedSpreadAngle, 0.001f, "SpreadAngle should be 1.0 for action +1");
        Assert.AreEqual(1.0f, ddaAgent.currentNormalizedEnemySpeed,  0.001f, "EnemySpeed should be 1.0 for action +1");

        // action = -1.0 → (-1.0 + 1) / 2 = 0.0
        var minActions = new Unity.MLAgents.Actuators.ActionBuffers(
            new float[] { -1.0f, -1.0f, -1.0f, -1.0f },
            new int[] { }
        );
        ddaAgent.OnActionReceived(minActions);
        Assert.AreEqual(0.0f, ddaAgent.currentNormalizedFireRate,    0.001f, "FireRate should be 0.0 for action -1");
        Assert.AreEqual(0.0f, ddaAgent.currentNormalizedBulletSpeed, 0.001f, "BulletSpeed should be 0.0 for action -1");
        Assert.AreEqual(0.0f, ddaAgent.currentNormalizedSpreadAngle, 0.001f, "SpreadAngle should be 0.0 for action -1");
        Assert.AreEqual(0.0f, ddaAgent.currentNormalizedEnemySpeed,  0.001f, "EnemySpeed should be 0.0 for action -1");

        // action = 0.0 → (0.0 + 1) / 2 = 0.5
        var midActions = new Unity.MLAgents.Actuators.ActionBuffers(
            new float[] { 0.0f, 0.0f, 0.0f, 0.0f },
            new int[] { }
        );
        ddaAgent.OnActionReceived(midActions);
        Assert.AreEqual(0.5f, ddaAgent.currentNormalizedFireRate,    0.001f, "FireRate should be 0.5 for action 0");
        Assert.AreEqual(0.5f, ddaAgent.currentNormalizedBulletSpeed, 0.001f, "BulletSpeed should be 0.5 for action 0");
        Assert.AreEqual(0.5f, ddaAgent.currentNormalizedSpreadAngle, 0.001f, "SpreadAngle should be 0.5 for action 0");
        Assert.AreEqual(0.5f, ddaAgent.currentNormalizedEnemySpeed,  0.001f, "EnemySpeed should be 0.5 for action 0");

        // Repeated identical actions should produce the same result (no drift/accumulation)
        ddaAgent.OnActionReceived(midActions);
        Assert.AreEqual(0.5f, ddaAgent.currentNormalizedFireRate, 0.001f, "Repeated action 0 must not drift");

        Object.DestroyImmediate(go);
    }
}
