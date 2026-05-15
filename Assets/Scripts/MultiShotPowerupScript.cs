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

                Destroy(gameObject);
            }
        }
    }
}