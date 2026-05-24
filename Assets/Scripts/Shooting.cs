using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shooting : MonoBehaviour
{
    public Transform firePoint;
    public GameObject bulletPrefab;
    public GameObject multiShotPrefab;
    public float bulletForce = 20f;
    public float fireRate = 0.5f; // Time between shots (in seconds)
    private float nextFireTime = 0f; // Tracks when player can shoot next

    public float powerupDuration = 5f; // Duration of powerup in seconds
    private bool isPowerupActive = false;
    private Coroutine powerupCoroutine;
    private PlayerPerformanceTelemetry telemetry;

    private void Start()
    {
        telemetry = GetComponent<PlayerPerformanceTelemetry>();
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1") && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate; // Set next allowed fire time
        }
    }

    void Shoot()
    {
        if (telemetry == null)
            telemetry = GetComponent<PlayerPerformanceTelemetry>();

        if (telemetry != null)
            telemetry.ReportShotFired();

        if (!isPowerupActive) { 
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            rb.AddForce(firePoint.up * bulletForce, ForceMode2D.Impulse);
        }
        else
        {
            GameObject multiShot = Instantiate(multiShotPrefab, firePoint.position, firePoint.rotation);
            Rigidbody2D parentRb = multiShot.GetComponent<Rigidbody2D>();

            // Apply force to parent
            parentRb.AddForce(firePoint.up * bulletForce, ForceMode2D.Impulse);

            // Also apply movement to each child
            foreach (Transform child in multiShot.transform)
            {
                Rigidbody2D childRb = child.GetComponent<Rigidbody2D>();
                if (childRb != null)
                {
                    childRb.velocity = firePoint.up * bulletForce;
                }
            }
        }
    }

    // Call this method when player collects a powerup
    public void ActivatePowerupMode()
    {
        // If powerup already active, stop existing coroutine to reset timer
        if (isPowerupActive && powerupCoroutine != null)
        {
            StopCoroutine(powerupCoroutine);
        }

        // Activate powerup mode
        isPowerupActive = true;

        // Start countdown to deactivate powerup
        powerupCoroutine = StartCoroutine(DeactivatePowerupAfterDuration());
    }

    private IEnumerator DeactivatePowerupAfterDuration()
    {
        yield return new WaitForSeconds(powerupDuration);

        // Return to normal mode
        isPowerupActive = false;
        powerupCoroutine = null;
    }
}
