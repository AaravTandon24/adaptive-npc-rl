using System.Collections.Generic;
using UnityEngine;

public class PlayerPerformanceTelemetry : MonoBehaviour
{
    private struct Snapshot
    {
        public float time;
        public float healthPercent;
        public int shotsFired;
        public int shotsHit;
        public float damageTaken;
        public float damageDealt;
        public int powerupsCollected;
        public int nearMisses;
    }

    [Header("References")]
    public PlayerLivesScript playerLives;

    [Header("Sampling")]
    public float sampleInterval = 1f;
    public float rollingWindow = 20f;

    private readonly Queue<Snapshot> snapshots = new Queue<Snapshot>();
    private float nextSampleTime;
    private int shotsFired;
    private int shotsHit;
    private float damageTaken;
    private float damageDealt;
    private int powerupsCollected;
    private int nearMisses;

    private void Awake()
    {
        if (playerLives == null)
            playerLives = GetComponent<PlayerLivesScript>();
    }

    private void Update()
    {
        if (Time.time >= nextSampleTime)
        {
            AddSnapshot();
            nextSampleTime = Time.time + Mathf.Max(0.1f, sampleInterval);
        }
    }

    public void ReportShotFired()
    {
        shotsFired++;
    }

    public void ReportShotHit(float damage)
    {
        shotsHit++;
        damageDealt += Mathf.Max(0f, damage);
    }

    public void ReportDamageTaken(float damage)
    {
        damageTaken += Mathf.Max(0f, damage);
    }

    public void ReportPowerupCollected()
    {
        powerupsCollected++;
    }

    public void ReportNearMiss()
    {
        nearMisses++;
    }

    public PlayerDifficultyState GetPlayerState()
    {
        AddSnapshot();

        Snapshot current = snapshots.Count > 0 ? snapshots.ToArray()[snapshots.Count - 1] : CreateSnapshot();
        Snapshot baseline = current;
        foreach (Snapshot snapshot in snapshots)
        {
            baseline = snapshot;
            break;
        }

        float elapsed = Mathf.Max(0.1f, current.time - baseline.time);
        int firedDelta = Mathf.Max(0, current.shotsFired - baseline.shotsFired);
        int hitDelta = Mathf.Max(0, current.shotsHit - baseline.shotsHit);

        return new PlayerDifficultyState
        {
            healthPercent = current.healthPercent,
            damageTakenPerSecond = (current.damageTaken - baseline.damageTaken) / elapsed,
            damageDealtPerSecond = (current.damageDealt - baseline.damageDealt) / elapsed,
            hitRate = firedDelta > 0 ? (float)hitDelta / firedDelta : 0f,
            shotsPerSecond = firedDelta / elapsed,
            powerupsCollected = current.powerupsCollected - baseline.powerupsCollected,
            nearMissesPerSecond = (current.nearMisses - baseline.nearMisses) / elapsed
        };
    }

    private void AddSnapshot()
    {
        Snapshot snapshot = CreateSnapshot();
        snapshots.Enqueue(snapshot);

        float cutoff = Time.time - Mathf.Max(1f, rollingWindow);
        while (snapshots.Count > 1 && snapshots.Peek().time < cutoff)
            snapshots.Dequeue();
    }

    private Snapshot CreateSnapshot()
    {
        return new Snapshot
        {
            time = Time.time,
            healthPercent = playerLives != null ? playerLives.RemainingHealthPercentage : 1f,
            shotsFired = shotsFired,
            shotsHit = shotsHit,
            damageTaken = damageTaken,
            damageDealt = damageDealt,
            powerupsCollected = powerupsCollected,
            nearMisses = nearMisses
        };
    }

    // Public getters added for RLTrainingManager CSV logging
    public int TotalShotsFired => shotsFired;
    public int TotalShotsHit => shotsHit;
    public float TotalDamageDealt => damageDealt;
    public float TotalDamageTaken => damageTaken;

    /// <summary>
    /// Resets all cumulative counters and clears the snapshot queue.
    /// Call this at the start of each episode so per-episode metrics are accurate.
    /// </summary>
    public void ResetEpisode()
    {
        shotsFired = 0;
        shotsHit   = 0;
        damageTaken  = 0f;
        damageDealt  = 0f;
        powerupsCollected = 0;
        nearMisses   = 0;
        snapshots.Clear();
        nextSampleTime = 0f;
    }
}

public struct PlayerDifficultyState
{
    public float healthPercent;
    public float damageTakenPerSecond;
    public float damageDealtPerSecond;
    public float hitRate;
    public float shotsPerSecond;
    public int powerupsCollected;
    public float nearMissesPerSecond;
}
