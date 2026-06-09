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

    // Set to >0 by RLTrainingManager after an episode reset to log the first
    // N FixedUpdate frames and confirm physics is running.
    private int debugFramesLeft = 0;

    public void TriggerDebugLog(int frames = 5)
    {
        debugFramesLeft = frames;
        updateLogFramesLeft = frames;
        Debug.LogWarning($"[PlayerMovement] Debug logging armed for {frames} frames. timeScale={Time.timeScale:F2}, rb={(rb == null ? "NULL!" : "ok")}, enabled={enabled}, gameObject.active={gameObject.activeInHierarchy}");
    }

    void Awake()
    {
        pauseScript = pauseMenu.GetComponent<PauseScript>();
    }

    // Throttle counter for Update diagnostics
    private int updateLogFramesLeft = 0;

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        mousePos = cam.ScreenToWorldPoint(Input.mousePosition);

        if (updateLogFramesLeft > 0)
        {
            updateLogFramesLeft--;
            Debug.LogWarning($"[PlayerMovement.Update] frame, timeScale={Time.timeScale:F2}, movement={movement}, cam={cam != null}");
        }

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
        // *** Log FIRST — before any rb access — so we see this even if rb is null ***
        if (debugFramesLeft > 0)
        {
            debugFramesLeft--;
            Debug.LogWarning($"[PlayerMovement.FixedUpdate] #{5 - debugFramesLeft}: " +
                             $"timeScale={Time.timeScale:F2}, fixedDT={Time.fixedDeltaTime:F4}, " +
                             $"rb={(rb == null ? "NULL!" : "ok")}, " +
                             $"simulated={rb?.simulated}, bodyType={rb?.bodyType}, movement={movement}");
        }

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
 