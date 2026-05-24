using UnityEngine;

public class PowerupScript : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Shooting shootingScript = collision.GetComponent<Shooting>();

            if (shootingScript != null)
            {
                shootingScript.ActivatePowerupMode();

                PlayerPerformanceTelemetry telemetry = collision.GetComponent<PlayerPerformanceTelemetry>();
                if (telemetry == null && DanmakuDDAController.Instance != null)
                    telemetry = collision.gameObject.AddComponent<PlayerPerformanceTelemetry>();

                if (telemetry != null)
                    telemetry.ReportPowerupCollected();

                Destroy(gameObject);
            }
        }
    }
}
