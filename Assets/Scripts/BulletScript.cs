using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletScript : MonoBehaviour
{
    public GameObject hiteffect;
    public ScoreManager scoreManager;
    private PlayerPerformanceTelemetry telemetry;

    private void Awake()
    {
        scoreManager = GameObject.FindGameObjectWithTag("Score").GetComponent<ScoreManager>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            telemetry = player.GetComponent<PlayerPerformanceTelemetry>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.CompareTag("Enemy Bullet"))
        {
            if (telemetry == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    telemetry = player.GetComponent<PlayerPerformanceTelemetry>();
            }

            if (telemetry != null && collision.gameObject.CompareTag("Enemy"))
                telemetry.ReportShotHit(10f);

            scoreManager.AddScore(1);
            Destroy(gameObject);
        }
    }
}
