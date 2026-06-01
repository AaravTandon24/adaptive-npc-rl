using UnityEngine;

[System.Serializable]
public struct DifficultyProfile
{
    [Range(0.25f, 2f)] public float fireRateMultiplier;
    [Range(0.25f, 2f)] public float bulletSpeedMultiplier;
    [Range(0.25f, 2f)] public float spreadAngleMultiplier;
    [Range(0.25f, 2f)] public float spawnIntervalMultiplier;
    [Range(0.25f, 2f)] public float enemySpeedMultiplier;
    [Range(0.25f, 2f)] public float powerupSpawnMultiplier;
    [Range(1f, 2f)] public float bulletCountMultiplier;
    [Range(0, 300)] public int maxActiveEnemyBullets;

    public static DifficultyProfile Default => new DifficultyProfile
    {
        fireRateMultiplier = 1f,
        bulletSpeedMultiplier = 1f,
        spreadAngleMultiplier = 1f,
        spawnIntervalMultiplier = 1f,
        enemySpeedMultiplier = 1f,
        powerupSpawnMultiplier = 1f,
        bulletCountMultiplier = 1f,
        maxActiveEnemyBullets = 120
    };

    public void Clamp()
    {
        fireRateMultiplier = Mathf.Clamp(fireRateMultiplier, 0.25f, 2f);
        bulletSpeedMultiplier = Mathf.Clamp(bulletSpeedMultiplier, 0.25f, 2f);
        spreadAngleMultiplier = Mathf.Clamp(spreadAngleMultiplier, 0.25f, 2f);
        spawnIntervalMultiplier = Mathf.Clamp(spawnIntervalMultiplier, 0.25f, 2f);
        enemySpeedMultiplier = Mathf.Clamp(enemySpeedMultiplier, 0.25f, 2f);
        powerupSpawnMultiplier = Mathf.Clamp(powerupSpawnMultiplier, 0.25f, 2f);
        bulletCountMultiplier = Mathf.Clamp(bulletCountMultiplier, 1f, 2f);
        maxActiveEnemyBullets = Mathf.Clamp(maxActiveEnemyBullets, 0, 300);
    }

    public static DifficultyProfile FromPressure(float difficulty, int maxBullets)
    {
        difficulty = Mathf.Clamp01(difficulty);
        DifficultyProfile profile = Default;
        profile.fireRateMultiplier = Mathf.Lerp(0.55f, 1.65f, difficulty);
        profile.bulletSpeedMultiplier = Mathf.Lerp(0.65f, 1.45f, difficulty);
        profile.spreadAngleMultiplier = Mathf.Lerp(0.75f, 1.35f, difficulty);
        profile.spawnIntervalMultiplier = Mathf.Lerp(1.55f, 0.65f, difficulty);
        profile.enemySpeedMultiplier = Mathf.Lerp(0.75f, 1.35f, difficulty);
        profile.powerupSpawnMultiplier = Mathf.Lerp(0.75f, 1.55f, 1f - difficulty);

        // Delay bullet-count increases: scale only when difficulty is very high to avoid early spam.
        // Use a tighter threshold and a smooth power curve so increases are gradual near top-end.
        float raw = Mathf.InverseLerp(0.9f, 1f, difficulty); // starts scaling at difficulty >= 0.9
        float bulletCountDifficulty = Mathf.Pow(Mathf.Clamp01(raw), 2f); // ease-in curve
        profile.bulletCountMultiplier = Mathf.Lerp(1f, 2f, bulletCountDifficulty);

        profile.maxActiveEnemyBullets = maxBullets;
        profile.Clamp();
        return profile;
    }
}

public interface IDifficultyTunable
{
    void ApplyDifficulty(DifficultyProfile profile);
}
