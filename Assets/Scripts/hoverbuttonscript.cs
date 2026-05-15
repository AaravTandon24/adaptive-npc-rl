using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scale Settings")]
    [Tooltip("How much larger the button will become when hovered (1 = no change, 1.1 = 10% larger)")]
    public float hoverScale = 1.1f;

    [Tooltip("How fast the button scales up/down")]
    public float scaleSpeed = 10f;

    private Vector3 originalScale;
    private Vector3 targetScale;
    private RectTransform rectTransform;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
        targetScale = originalScale;
    }

    void Update()
    {
        // Smoothly interpolate between current scale and target scale
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.deltaTime * scaleSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Set target scale to be larger when mouse enters
        targetScale = originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Return to original scale when mouse exits
        targetScale = originalScale;
    }
}