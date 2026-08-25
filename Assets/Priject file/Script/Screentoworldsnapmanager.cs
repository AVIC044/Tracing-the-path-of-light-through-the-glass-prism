using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ScreenToWorldSnapManager : MonoBehaviour
{
    [Header("Index-Paired Lists")]
    [Tooltip("Element 0 belongs to Snap Target 0, Element 1 belongs to Snap Target 1, etc.")]
    public List<RectTransform> draggables = new List<RectTransform>();

    [Tooltip("Element 0 is the correct target for Draggable 0, etc.")]
    public List<RectTransform> snapTargets = new List<RectTransform>();

    [Header("OBJECT TO HIDE AFTER CORRECT DROP")]
    [Tooltip("Element 0 will hide when Draggable 0 is correctly dropped. Element 1 for Draggable 1, etc.")]
    public List<GameObject> objectsToHide = new List<GameObject>();

    [Header("Placed Images (Optional)")]
    [Tooltip("Optional image shown after the draggable is correctly placed.")]
    public List<GameObject> placedImages = new List<GameObject>();

    [Header("Parent Canvas")]
    public Canvas canvas;

    [Header("Snap Animation")]
    [Tooltip("Time taken to return/snap. Set 0 for instant.")]
    public float snapAnimDuration = 0.15f;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent OnAllSnappedUnityEvent;

    public event Action OnAllSnapped;
    private class DragState
    {
        public RectTransform rect;
        public RectTransform correctTarget;

        public Vector2 originalAnchoredPos;
        public Transform originalParent;
        public int originalSiblingIndex;

        public bool isLocked;

        public CanvasGroup canvasGroup;
        public DragHandler handler;

        public GameObject placedImage;

        public GameObject objectToHide;

        public Vector2 grabOffset;
    }
    private readonly Dictionary<RectTransform, DragState> _states =
        new Dictionary<RectTransform, DragState>();

    private int _correctSnapCount = 0;

    private bool _allSnappedFired = false;

    private void Awake()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }

        SetupAll();
    }
    private void SetupAll()
    {
        if (draggables.Count != snapTargets.Count)
        {
            Debug.LogError(
                "[ScreenToWorldSnapManager] " +
                "Draggables and Snap Targets count must match."
            );
        }

        int count = Mathf.Min(
            draggables.Count,
            snapTargets.Count
        );

        for (int i = 0; i < count; i++)
        {
            RectTransform draggable = draggables[i];

            RectTransform target = snapTargets[i];

            if (draggable == null || target == null)
            {
                Debug.LogWarning(
                    "[ScreenToWorldSnapManager] " +
                    "Null entry at index " + i
                );

                continue;
            }

            DragState state = new DragState();

            state.rect = draggable;

            state.correctTarget = target;

            state.originalAnchoredPos =
                draggable.anchoredPosition;

            state.originalParent =
                draggable.parent;

            state.originalSiblingIndex =
                draggable.GetSiblingIndex();

            state.isLocked = false;


            CanvasGroup canvasGroup =
                draggable.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup =
                    draggable.gameObject.AddComponent<CanvasGroup>();
            }

            state.canvasGroup = canvasGroup;
            DragHandler handler =
                draggable.GetComponent<DragHandler>();

            if (handler == null)
            {
                handler =
                    draggable.gameObject.AddComponent<DragHandler>();
            }

            handler.Init(this, draggable);

            state.handler = handler;

            if (i < objectsToHide.Count)
            {
                if (objectsToHide[i] != null)
                {
                    state.objectToHide =
                        objectsToHide[i];
                }
            }

            if (i < placedImages.Count)
            {
                if (placedImages[i] != null)
                {
                    state.placedImage =
                        placedImages[i];

                    // Hide initially
                    state.placedImage.SetActive(false);
                }
            }

            _states[draggable] = state;
        }
    }
    internal void HandleDrop(
        RectTransform draggedRect,
        PointerEventData eventData)
    {
        if (!_states.TryGetValue(
            draggedRect,
            out DragState state))
        {
            return;
        }

        if (state.isLocked)
        {
            return;
        }

        RectTransform hitTarget =
            FindTargetUnderPointer(
                draggedRect,
                eventData
            );

        if (hitTarget != null &&
            hitTarget == state.correctTarget)
        {
            SnapCorrect(state);
        }

        else
        {
            SnapBack(state);
        }
    }
    private Camera ResolveCameraFor(
        RectTransform rect,
        PointerEventData eventData)
    {
        Canvas rectCanvas =
            rect.GetComponentInParent<Canvas>();

        if (rectCanvas == null)
        {
            return ResolveCamera(eventData);
        }

        if (rectCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        if (rectCanvas.renderMode ==
            RenderMode.WorldSpace)
        {
            if (rectCanvas.worldCamera != null)
            {
                return rectCanvas.worldCamera;
            }

            return Camera.main;
        }

        if (rectCanvas.worldCamera != null)
        {
            return rectCanvas.worldCamera;
        }

        return ResolveCamera(eventData);
    }

    private Camera ResolveCamera(
        PointerEventData eventData)
    {
        if (canvas != null &&
            canvas.renderMode ==
            RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        if (eventData != null &&
            eventData.pressEventCamera != null)
        {
            return eventData.pressEventCamera;
        }

        if (canvas != null)
        {
            return canvas.worldCamera;
        }

        return null;
    }

    private RectTransform FindTargetUnderPointer(
        RectTransform draggedRect,
        PointerEventData eventData)
    {
        for (int i = 0;
             i < snapTargets.Count;
             i++)
        {
            RectTransform target =
                snapTargets[i];

            if (target == null)
            {
                continue;
            }

            Camera targetCamera =
                ResolveCameraFor(
                    target,
                    eventData
                );

            bool inside =
                RectTransformUtility.RectangleContainsScreenPoint(
                    target,
                    eventData.position,
                    targetCamera
                );

            if (inside)
            {
                return target;
            }
        }

        return null;
    }

    private void SnapCorrect(DragState state)
    {
        // Already locked?
        if (state.isLocked)
        {
            return;
        }

        state.isLocked = true;

        if (state.objectToHide != null)
        {
            state.objectToHide.SetActive(false);

            Debug.Log(
                "[ScreenToWorldSnapManager] " +
                "Object Hidden: " +
                state.objectToHide.name
            );
        }

        if (state.canvasGroup != null)
        {
            state.canvasGroup.blocksRaycasts = false;
        }

        if (state.handler != null)
        {
            state.handler.enabled = false;
        }

        if (state.placedImage != null)
        {
            state.rect.gameObject.SetActive(false);

            state.placedImage.SetActive(true);
        }

        else
        {
            state.rect.SetParent(
                state.correctTarget,
                true
            );

            state.rect.SetAsLastSibling();

            state.rect.localRotation =
                Quaternion.identity;

            state.rect.localScale =
                Vector3.one;

            StopAndAnimate(
                state.rect,
                Vector2.zero
            );
        }

        _correctSnapCount++;

        Debug.Log(
            "[ScreenToWorldSnapManager] " +
            "Correct Drop: " +
            _correctSnapCount +
            "/" +
            _states.Count
        );

        CheckAllSnapped();
    }

    private void SnapBack(DragState state)
    {
        if (state.rect.parent !=
            state.originalParent)
        {
            state.rect.SetParent(
                state.originalParent,
                false
            );

            state.rect.SetSiblingIndex(
                state.originalSiblingIndex
            );
        }

        StopAndAnimate(
            state.rect,
            state.originalAnchoredPos
        );
    }

    private void StopAndAnimate(
        RectTransform rect,
        Vector2 targetPosition)
    {
        DragHandler handler =
            rect.GetComponent<DragHandler>();

        if (handler != null)
        {
            handler.StopAllCoroutines();

            handler.StartCoroutine(
                handler.AnimateTo(
                    rect,
                    targetPosition,
                    snapAnimDuration
                )
            );
        }
        else
        {
            rect.anchoredPosition =
                targetPosition;
        }
    }
    private void CheckAllSnapped()
    {
        if (_allSnappedFired)
        {
            return;
        }

        if (_states.Count == 0)
        {
            return;
        }

        if (_correctSnapCount >= _states.Count)
        {
            _allSnappedFired = true;

            Debug.Log(
                "[ScreenToWorldSnapManager] " +
                "ALL TEXTS CORRECT!"
            );

            // C# Event
            if (OnAllSnapped != null)
            {
                OnAllSnapped.Invoke();
            }

            // Unity Inspector Event
            if (OnAllSnappedUnityEvent != null)
            {
                OnAllSnappedUnityEvent.Invoke();
            }
        }
    }

    public bool IsAllSnapped()
    {
        if (_states.Count == 0)
        {
            return false;
        }

        return _correctSnapCount >= _states.Count;
    }

    public int CorrectSnapCount
    {
        get
        {
            return _correctSnapCount;
        }
    }
    public int TotalDraggables
    {
        get
        {
            return _states.Count;
        }
    }

    internal void HandleBeginDrag(
        RectTransform draggedRect,
        PointerEventData eventData)
    {
        if (!_states.TryGetValue(
            draggedRect,
            out DragState state))
        {
            return;
        }

        if (state.isLocked)
        {
            return;
        }

        if (state.canvasGroup != null)
        {
            state.canvasGroup.blocksRaycasts = false;
        }

        draggedRect.SetAsLastSibling();

        Camera camera =
            ResolveCamera(eventData);

        RectTransform parentRect =
            draggedRect.parent as RectTransform;

        if (parentRect != null)
        {
            if (RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    camera,
                    out Vector2 pointerLocal))
            {
                state.grabOffset =
                    draggedRect.anchoredPosition -
                    pointerLocal;
            }
            else
            {
                state.grabOffset =
                    Vector2.zero;
            }
        }
        else
        {
            state.grabOffset =
                Vector2.zero;
        }
    }

    internal void HandleDrag(
        RectTransform draggedRect,
        PointerEventData eventData)
    {
        if (!_states.TryGetValue(
            draggedRect,
            out DragState state))
        {
            return;
        }

        if (state.isLocked)
        {
            return;
        }

        Camera camera =
            ResolveCamera(eventData);

        RectTransform parentRect =
            draggedRect.parent as RectTransform;

        if (parentRect != null)
        {
            if (RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    camera,
                    out Vector2 pointerLocal))
            {
                draggedRect.anchoredPosition =
                    pointerLocal +
                    state.grabOffset;
            }
        }
    }
    internal void HandleEndDrag(
        RectTransform draggedRect,
        PointerEventData eventData)
    {
        if (!_states.TryGetValue(
            draggedRect,
            out DragState state))
        {
            return;
        }

        if (state.isLocked)
        {
            return;
        }
        if (state.canvasGroup != null)
        {
            state.canvasGroup.blocksRaycasts = true;
        }

        HandleDrop(
            draggedRect,
            eventData
        );
    }
}
[RequireComponent(typeof(RectTransform))]
public class DragHandler :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private ScreenToWorldSnapManager _manager;

    private RectTransform _rect;

    public void Init(
        ScreenToWorldSnapManager manager,
        RectTransform rect)
    {
        _manager = manager;

        _rect = rect;
    }
    public void OnBeginDrag(
        PointerEventData eventData)
    {
        if (_manager != null)
        {
            _manager.HandleBeginDrag(
                _rect,
                eventData
            );
        }
    }
    public void OnDrag(
        PointerEventData eventData)
    {
        if (_manager != null)
        {
            _manager.HandleDrag(
                _rect,
                eventData
            );
        }
    }
    public void OnEndDrag(
        PointerEventData eventData)
    {
        if (_manager != null)
        {
            _manager.HandleEndDrag(
                _rect,
                eventData
            );
        }
    }
    public System.Collections.IEnumerator AnimateTo(
        RectTransform rect,
        Vector2 target,
        float duration)
    {
        if (duration <= 0f)
        {
            rect.anchoredPosition =
                target;

            yield break;
        }

        Vector2 start =
            rect.anchoredPosition;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t =
                Mathf.Clamp01(
                    time / duration
                );

            float eased =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f
                );

            rect.anchoredPosition =
                Vector2.Lerp(
                    start,
                    target,
                    eased
                );

            yield return null;
        }
        rect.anchoredPosition =
            target;
    }
}