using UnityEngine;

public class AutoScaler : MonoBehaviour
{
    [Header("Target Object")]
    [Tooltip("The object (or Image) you want to scale.")]
    public Transform targetObject;

    [Header("Scaling Settings")]
    [Tooltip("How fast the object scales up and down.")]
    public float scaleSpeed = 3f;

    [Tooltip("The maximum size multiplier (e.g., 1.2 means it gets 20% bigger).")]
    public float maxScaleMultiplier = 1.2f;

    [Tooltip("The minimum size multiplier (e.g., 0.8 means it gets 20% smaller).")]
    public float minScaleMultiplier = 0.8f;

    private Vector3 originalScale;
    private bool isScaling = false;

    void Start()
    {
        // Save the original scale at the start so we can return to it later
        if (targetObject != null)
        {
            originalScale = targetObject.localScale;
        }
    }

    void Update()
    {
        // If the scaling is active, animate the scale up and down smoothly
        if (isScaling && targetObject != null)
        {
            // Use Mathf.Sin to create a smooth wave between 0 and 1 over time
            float wave = (Mathf.Sin(Time.time * scaleSpeed) + 1f) / 2f;

            // Calculate the current multiplier based on the wave
            float currentMultiplier = Mathf.Lerp(minScaleMultiplier, maxScaleMultiplier, wave);

            // Apply the new scale
            targetObject.localScale = originalScale * currentMultiplier;
        }
    }

    // =========================================================
    // FUNCTION 1: TRIGGER THIS TO START SCALING UP AND DOWN
    // =========================================================
    public void StartAutoScaling()
    {
        if (targetObject == null) return;

        isScaling = true;
        Debug.Log("Scaling");
    }

    // =========================================================
    // FUNCTION 2: TRIGGER THIS TO STOP SCALING AND RESET
    // =========================================================
    public void StopScalingAndReset()
    {
        if (targetObject == null) return;

        isScaling = false;

        // Reset the object back to its exact original size
        targetObject.localScale = originalScale;
        Debug.Log("Nooooo Scaling");
    }
}