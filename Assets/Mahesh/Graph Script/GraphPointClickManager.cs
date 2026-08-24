using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class GraphPointClickManager : MonoBehaviour
{
    [System.Serializable]
    public class GraphPoint
    {
        public string pointName;

        [Header("Correct Graph Area")]
        public RectTransform correctArea;

        [Header("Unlock Button")]
        public Button unlockButton;

        [Header("On Point Completed Event")]
        public UnityEvent onPointCompleted;

        [HideInInspector]
        public bool isActive;

        [HideInInspector]
        public bool isCompleted;
    }

    // =========================================================
    // GRAPH
    // =========================================================

    [Header("GRAPH")]
    [Tooltip("Full graph UI area used for touch/click detection")]
    public RectTransform graphArea;

    // =========================================================
    // GRAPH POINTS
    // =========================================================

    [Header("GRAPH POINTS")]
    public List<GraphPoint> graphPoints = new List<GraphPoint>();

    // =========================================================
    // DOT PREFABS
    // =========================================================

    [Header("DOT PREFABS")]
    public GameObject greenDotPrefab;
    public GameObject redDotPrefab;

    [Header("RED DOT")]
    public float redDotDuration = 0.5f;

    // =========================================================
    // AUDIO
    // =========================================================

    [Header("AUDIO")]
    public AudioSource audioSource;

    [Tooltip("Sound played when user clicks a wrong area")]
    public AudioClip wrongClickSound;

    [Tooltip("Sound played when user clicks the correct area")]
    public AudioClip correctClickSound;

    [Header("AUDIO VOLUME")]
    [Range(0f, 1f)]
    public float wrongSoundVolume = 1f;

    [Range(0f, 1f)]
    public float correctSoundVolume = 1f;

    // =========================================================
    // EVENTS
    // =========================================================

    [Header("ALL POINTS COMPLETED EVENT")]
    public UnityEvent onAllPointsCompleted;

    // =========================================================
    // PRIVATE
    // =========================================================

    private GameObject currentRedDot;
    private int activePointIndex = -1;
    private bool allPointsCompletedEventTriggered = false;
    private Canvas graphCanvas;

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (graphArea != null)
        {
            graphCanvas = graphArea.GetComponentInParent<Canvas>();
        }

        SetupButtons();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        HandleTouch();
    }

    // =========================================================
    // ANDROID TOUCH
    // =========================================================

    private void HandleTouch()
    {
        // Android
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                ProcessGraphClick(touch.position);
            }
            return;
        }

        // PC testing
        if (Input.GetMouseButtonDown(0))
        {
            ProcessGraphClick(Input.mousePosition);
        }
    }

    // =========================================================
    // PROCESS GRAPH CLICK
    // =========================================================

    private void ProcessGraphClick(Vector2 screenPosition)
    {
        if (graphArea == null)
        {
            Debug.LogWarning("Graph Area is not assigned!");
            return;
        }

        Camera eventCamera = GetCanvasCamera();

        bool insideGraph = RectTransformUtility.RectangleContainsScreenPoint(
            graphArea,
            screenPosition,
            eventCamera
        );

        if (!insideGraph)
        {
            return;
        }

        if (activePointIndex == -1)
        {
            Debug.Log("Select a graph button first.");
            return;
        }

        GraphPoint activePoint = graphPoints[activePointIndex];

        if (activePoint.correctArea == null)
        {
            Debug.LogWarning("Correct Area missing for " + activePoint.pointName);
            return;
        }

        bool correct = RectTransformUtility.RectangleContainsScreenPoint(
            activePoint.correctArea,
            screenPosition,
            eventCamera
        );

        if (correct)
        {
            CorrectPoint(activePoint);
        }
        else
        {
            ShowRedDot(screenPosition);
            PlayWrongSound();
        }
    }

    // =========================================================
    // CANVAS CAMERA
    // =========================================================

    private Camera GetCanvasCamera()
    {
        if (graphCanvas == null) return null;
        if (graphCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        if (graphCanvas.worldCamera != null) return graphCanvas.worldCamera;

        return Camera.main;
    }

    // =========================================================
    // SETUP BUTTONS
    // =========================================================

    private void SetupButtons()
    {
        for (int i = 0; i < graphPoints.Count; i++)
        {
            int index = i;

            graphPoints[i].isActive = false;
            graphPoints[i].isCompleted = false;

            if (graphPoints[i].unlockButton != null)
            {
                graphPoints[i].unlockButton.onClick.RemoveAllListeners();
                graphPoints[i].unlockButton.onClick.AddListener(() =>
                {
                    SelectPoint(index);
                });
            }
        }

        // Only enable the very first button at start
        EnableNextAvailableButton();
    }

    // =========================================================
    // BUTTON CLICK
    // =========================================================

    public void SelectPoint(int index)
    {
        if (index < 0 || index >= graphPoints.Count) return;

        GraphPoint point = graphPoints[index];

        if (point.isCompleted) return;
        if (activePointIndex != -1) return;

        // Double-check to strictly enforce order (prevent hacking the order)
        int expectedIndex = -1;
        for (int i = 0; i < graphPoints.Count; i++)
        {
            if (!graphPoints[i].isCompleted)
            {
                expectedIndex = i;
                break;
            }
        }
        if (index != expectedIndex)
        {
            Debug.Log("Please complete the previous points first.");
            return;
        }

        activePointIndex = index;
        point.isActive = true;

        Debug.Log("Selected: " + point.pointName);

        // Lock selected button while searching
        if (point.unlockButton != null)
        {
            point.unlockButton.interactable = false;
        }
    }

    // =========================================================
    // CORRECT POINT
    // =========================================================

    private void CorrectPoint(GraphPoint point)
    {
        Debug.Log("CORRECT: " + point.pointName);

        point.isCompleted = true;
        point.isActive = false;

        PlayCorrectSound();
        SpawnGreenDot(point.correctArea);
        point.onPointCompleted?.Invoke();

        if (point.unlockButton != null)
        {
            point.unlockButton.interactable = false;
        }

        activePointIndex = -1;

        // Turn on ONLY the next button in the sequence
        EnableNextAvailableButton();

        CheckAllPointsCompleted();
    }

    // =========================================================
    // GREEN DOT
    // =========================================================

    private void SpawnGreenDot(RectTransform correctArea)
    {
        if (greenDotPrefab == null || correctArea == null) return;

        GameObject greenDot = Instantiate(greenDotPrefab, graphArea);
        RectTransform greenRect = greenDot.GetComponent<RectTransform>();

        if (greenRect == null)
        {
            Destroy(greenDot);
            return;
        }

        Camera canvasCamera = GetCanvasCamera();
        Vector3 worldPosition = correctArea.TransformPoint(correctArea.rect.center);
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(canvasCamera, worldPosition);

        Vector2 localPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            graphArea,
            screenPosition,
            canvasCamera,
            out localPosition
        );

        greenRect.anchoredPosition = localPosition;
        greenRect.localRotation = Quaternion.identity;
    }

    // =========================================================
    // ENABLE NEXT SEQUENTIAL BUTTON
    // =========================================================

    private void EnableNextAvailableButton()
    {
        bool foundNext = false;

        for (int i = 0; i < graphPoints.Count; i++)
        {
            GraphPoint point = graphPoints[i];

            if (point.unlockButton == null) continue;

            if (point.isCompleted)
            {
                // Already done, keep locked
                point.unlockButton.interactable = false;
            }
            else
            {
                if (!foundNext)
                {
                    // This is the FIRST incomplete point. Unlock it!
                    point.unlockButton.interactable = true;
                    foundNext = true;
                }
                else
                {
                    // This is a future point. Keep it locked for now.
                    point.unlockButton.interactable = false;
                }
            }
        }
    }

    // =========================================================
    // RED DOT
    // =========================================================

    private void ShowRedDot(Vector2 screenPosition)
    {
        if (redDotPrefab == null) return;

        if (currentRedDot != null) Destroy(currentRedDot);

        currentRedDot = Instantiate(redDotPrefab, graphArea);
        RectTransform redRect = currentRedDot.GetComponent<RectTransform>();

        if (redRect == null)
        {
            Destroy(currentRedDot);
            return;
        }

        Camera canvasCamera = GetCanvasCamera();
        Vector2 localPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            graphArea,
            screenPosition,
            canvasCamera,
            out localPosition
        );

        redRect.anchoredPosition = localPosition;
        redRect.localRotation = Quaternion.identity;

        StartCoroutine(RemoveRedDot(currentRedDot));
    }

    // =========================================================
    // REMOVE RED DOT
    // =========================================================

    private IEnumerator RemoveRedDot(GameObject dot)
    {
        yield return new WaitForSeconds(redDotDuration);
        if (dot != null) Destroy(dot);
        if (currentRedDot == dot) currentRedDot = null;
    }

    // =========================================================
    // WRONG SOUND
    // =========================================================

    private void PlayWrongSound()
    {
        if (audioSource == null || wrongClickSound == null) return;
        audioSource.PlayOneShot(wrongClickSound, wrongSoundVolume);
    }

    // =========================================================
    // CORRECT SOUND
    // =========================================================

    private void PlayCorrectSound()
    {
        if (audioSource == null || correctClickSound == null) return;
        audioSource.PlayOneShot(correctClickSound, correctSoundVolume);
    }

    // =========================================================
    // CHECK ALL POINTS
    // =========================================================

    private void CheckAllPointsCompleted()
    {
        for (int i = 0; i < graphPoints.Count; i++)
        {
            if (!graphPoints[i].isCompleted) return;
        }

        if (allPointsCompletedEventTriggered) return;

        allPointsCompletedEventTriggered = true;

        Debug.Log("================================");
        Debug.Log("ALL GRAPH POINTS COMPLETED!");
        Debug.Log("================================");

        onAllPointsCompleted?.Invoke();
    }
}