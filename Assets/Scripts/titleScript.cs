using UnityEngine;
using UnityEngine.UI;

public class UITitleFloat : MonoBehaviour
{
    [Header("Float Settings")]
    [Tooltip("How high the title will float up and down in pixels")]
    public float floatHeight = 10f;

    [Tooltip("How fast the title will complete one float cycle")]
    public float floatSpeed = 1f;

    private RectTransform rectTransform;
    private Vector2 startAnchoredPosition;

    void Start()
    {
        // Get the RectTransform component (all UI elements have this)
        rectTransform = GetComponent<RectTransform>();
        // Store the initial anchored position
        startAnchoredPosition = rectTransform.anchoredPosition;
    }

    void Update()
    {
        // Calculate new Y position using a sine wave
        float newY = startAnchoredPosition.y + (Mathf.Sin(Time.time * floatSpeed) * floatHeight);

        // Update the anchored position, keeping X the same
        rectTransform.anchoredPosition = new Vector2(startAnchoredPosition.x, newY);
    }
}