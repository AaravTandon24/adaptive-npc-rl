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
        pauseScript = pauseMenu.GetComponent<PauseScript>();
    }

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        mousePos = cam.ScreenToWorldPoint(Input.mousePosition);

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
        if (rb == null)
        {
            Debug.LogError("[PlayerMovement] rb is NULL — movement is impossible. Assign Rigidbody2D in the Inspector.");
            return;
        }

        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
        Vector2 lookDir = mousePos - rb.position;
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg - 90f;
        rb.rotation = angle;
    }
}