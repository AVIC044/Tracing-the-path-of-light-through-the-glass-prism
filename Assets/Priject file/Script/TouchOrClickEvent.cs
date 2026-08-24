using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class TouchOrClickEvent : MonoBehaviour
{
    // ========================= BASE EVENT =========================

    [Header("Base Touch Event")]
    public UnityEvent OnTouched;

    // ========================= CONDITIONAL EVENTS =========================

    [System.Serializable]
    public class ConditionalEvent
    {
        [Header("Page Condition")]
        [Tooltip("Event will trigger only when current page index matches this value.")]
        public int requiredPageIndex;

        public UnityEvent onInvoked;

        [Header("Trigger Settings")]
        public bool allowMultipleTriggers = true;

        [HideInInspector] public bool hasTriggered;
    }

    [Header("Invoke When Page Index Matches")]
    public List<ConditionalEvent> conditionalEvents = new List<ConditionalEvent>();

    // ========================= SETTINGS =========================

    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Behavior")]
    [SerializeField] private bool ignoreUI = true;

    // ========================= INTERNAL =========================

    private Collider cachedCollider;

    // ========================= LIFECYCLE =========================

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        ResetAllConditionalTriggers();
    }

    private void Update()
    {
        if (targetCamera == null)
            return;

        bool processedThisFrame = false;

        // -------- TOUCH --------
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;

            if (touch.press.wasPressedThisFrame)
            {
                ProcessPointer(touch.position.ReadValue());
                processedThisFrame = true; // Mark as processed to prevent double-firing
            }
        }

        // -------- MOUSE --------
        // Only run mouse click if we didn't already process a touch. 
        // (Android sometimes simulates a mouse click when you touch the screen)
        if (!processedThisFrame && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            ProcessPointer(Mouse.current.position.ReadValue());
        }
    }

    // ========================= INPUT PROCESSING =========================

    private void ProcessPointer(Vector2 screenPosition)
    {
        if (ignoreUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        if (hit.collider != cachedCollider)
            return;

        // --- 🔴 THE FIX: THIS WAS MISSING PREVIOUSLY ---

        // 1. Invoke the base event that happens on every tap
        OnTouched?.Invoke();

        // 2. Check the conditional events against the current SlideController index
        int currentSlide = SlideController.CurrentIndex;

        foreach (var evt in conditionalEvents)
        {
            if (evt.requiredPageIndex == currentSlide)
            {
                if (evt.allowMultipleTriggers || !evt.hasTriggered)
                {
                    evt.onInvoked?.Invoke();
                    evt.hasTriggered = true;
                }
            }
        }
    }

    // ========================= PUBLIC API =========================

    public void ResetAllConditionalTriggers()
    {
        foreach (var entry in conditionalEvents)
        {
            entry.hasTriggered = false;
        }
    }
}