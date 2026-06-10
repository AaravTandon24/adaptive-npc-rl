using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 7f;
    public Rigidbody2D rb;
    Vector2 movement;
    Vector2 mousePos;
    public Camera cam;
    public GameObject pauseMenu;
    public PauseScript pauseScript;
    public bool check = false;

    void Awake()
    {
        // Cache rb here so it's always fresh
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (pauseMenu != null)
            pauseScript = pauseMenu.GetComponent<PauseScript>();
    }

    // Called every time the GameObject is re-enabled (e.g., after an RL episode reset)
    void OnEnable()
    {
        // Re-cache Rigidbody2D in case the reference went stale during SetActive(false)
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        // Zero out any leftover velocity from the previous episode
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        if (cam != null)
            mousePos = cam.ScreenToWorldPoint(Input.mousePosition);

        if (pauseScript == null) return; // guard: don't crash if pause menu missing

        if (Input.GetKeyUp(KeyCode.Escape))
        {
            pauseScript.Pause();
            check = true;
        }
        if (Input.GetKeyDown(KeyCode.Space) && check)
        {
            pauseScript.Resume();
            check = false;
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
        Vector2 lookDir = mousePos - rb.position;
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg - 90f;
        rb.rotation = angle;
    }
}