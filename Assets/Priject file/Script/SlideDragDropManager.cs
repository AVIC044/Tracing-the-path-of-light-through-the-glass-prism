using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SlideDragDropManager : MonoBehaviour
{
    // ==========================================
    // DATA STRUCTURE FOR EACH SLIDE
    // ==========================================
    [System.Serializable]
    public class SlideDropData
    {
        [Tooltip("The index of the slide (0 for Slide 1, 1 for Slide 2, etc.)")]
        public int slideIndex;

        [Tooltip("How many drops need to be completed on this slide?")]
        public int targetValue;

        [Tooltip("Optional: Fire an extra event when this slide's drops are done.")]
        public UnityEvent OnSlideDropsCompleted;
    }

    [Header("Slide Controller Reference")]
    [Tooltip("Drag your GameObject with the SlideController script here.")]
    public SlideController slideController;

    [Header("Drop Requirements")]
    public List<SlideDropData> slideRequirements = new List<SlideDropData>();

    // Runtime variables
    private int currentDrops = 0;
    private int currentSlideIndex = 0;

    // ==========================================
    // LISTEN FOR SLIDE CHANGES
    // ==========================================
    private void OnEnable()
    {
        // Subscribe to the SlideController's slide change event
        SlideController.OnSlideChanged += HandleSlideChanged;
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        SlideController.OnSlideChanged -= HandleSlideChanged;
    }

    private void Start()
    {
        // Auto-find the SlideController if you forgot to assign it
        if (slideController == null)
        {
            slideController = FindObjectOfType<SlideController>();
        }
    }

    // Whenever the slide changes, reset the drop counter
    private void HandleSlideChanged(int newIndex)
    {
        currentDrops = 0;
        currentSlideIndex = newIndex;
    }

    // ==========================================
    // REGISTER DROPS
    // ==========================================

    /// <summary>
    /// Call this from your UIDragTo3DObject's 'OnObjectPlaced' event.
    /// </summary>
    public void RegisterDrop()
    {
        currentDrops++;

        // Find the rules for the slide we are currently on
        SlideDropData currentReq = slideRequirements.Find(x => x.slideIndex == currentSlideIndex);

        if (currentReq != null)
        {
            Debug.Log($"Slide {currentSlideIndex} Progress: {currentDrops} / {currentReq.targetValue}");

            // Did we reach the target for this slide?
            if (currentDrops >= currentReq.targetValue)
            {
                Debug.Log($"Slide {currentSlideIndex} Drops Completed!");

                // 1. Tell your unmodified SlideController to unlock the Next button!
                if (slideController != null)
                {
                    slideController.MarkPageCompleted();
                }

                // 2. Fire any extra events you set up in the Inspector
                currentReq.OnSlideDropsCompleted?.Invoke();
            }
        }
        else
        {
            Debug.LogWarning($"No drop requirement setup for slide index {currentSlideIndex} in the SlideDragDropManager.");
        }
    }
}