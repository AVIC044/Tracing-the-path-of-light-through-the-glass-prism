using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class UIGraphLineDrawer : MonoBehaviour
{
    [Header("Graph Points (In Order)")]
    [Tooltip("Drag your UI Point objects (RectTransforms) here.")]
    public RectTransform[] points;

    [Header("Line Settings")]
    [Tooltip("The parent object that will hold the drawn lines (usually an empty UI object behind your points).")]
    public RectTransform lineParent;

    [Tooltip("Color of the drawn line.")]
    public Color lineColor = Color.white;

    [Tooltip("How thick the line should be.")]
    public float lineThickness = 10f;

    [Header("Drawing Settings")]
    [Tooltip("How fast the line draws (pixels per second).")]
    public float drawSpeed = 500f;

    [Header("Events")]
    [Tooltip("Fires exactly when the line finishes connecting the last point.")]
    public UnityEvent OnDrawingFinished;

    private Coroutine drawingCoroutine;
    private List<GameObject> spawnedLines = new List<GameObject>();

    // =========================================================
    // 1. CALL THIS TO START DRAWING
    // =========================================================
    public void StartDrawing()
    {
        if (points == null || points.Length < 2)
        {
            Debug.LogWarning("UIGraphLineDrawer: You need at least 2 points to draw a graph.");
            return;
        }
        if (lineParent == null)
        {
            Debug.LogError("UIGraphLineDrawer: Please assign a Line Parent in the inspector.");
            return;
        }

        // Erase any existing graph before starting a new one
        EraseGraph();

        drawingCoroutine = StartCoroutine(DrawGraphRoutine());
    }

    // =========================================================
    // 2. CALL THIS TO ERASE THE GRAPH
    // =========================================================
    public void EraseGraph()
    {
        if (drawingCoroutine != null)
        {
            StopCoroutine(drawingCoroutine);
            drawingCoroutine = null;
        }

        // Destroy all previously created UI lines
        foreach (GameObject line in spawnedLines)
        {
            if (line != null) Destroy(line);
        }
        spawnedLines.Clear();
    }

    // =========================================================
    // ANIMATION COROUTINE
    // =========================================================
    private IEnumerator DrawGraphRoutine()
    {
        for (int i = 0; i < points.Length - 1; i++)
        {
            RectTransform pointA = points[i];
            RectTransform pointB = points[i + 1];

            // 1. Create the UI Line object
            GameObject lineObj = new GameObject("Line_" + i + "_to_" + (i + 1));
            lineObj.transform.SetParent(lineParent, false);

            // Add to list so we can delete it later
            spawnedLines.Add(lineObj);

            // 2. Setup the Image component
            Image lineImage = lineObj.AddComponent<Image>();
            lineImage.color = lineColor;
            lineImage.raycastTarget = false; // So it doesn't block clicks

            // 3. Setup the RectTransform
            RectTransform lineRect = lineObj.GetComponent<RectTransform>();

            // Set pivot to Left-Center (X=0, Y=0.5). This allows us to stretch it to the right.
            lineRect.pivot = new Vector2(0f, 0.5f);

            // Position the start of the line exactly at Point A
            lineRect.position = pointA.position;

            // 4. Calculate Distance and Angle
            Vector3 direction = pointB.position - pointA.position;
            float distance = direction.magnitude;

            // Calculate the angle to rotate the line
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            lineRect.rotation = Quaternion.Euler(0, 0, angle);

            // 5. Animate the length (width) of the line
            float duration = distance / drawSpeed;
            float timePassed = 0f;

            while (timePassed < duration)
            {
                timePassed += Time.deltaTime;

                // Calculate current width based on time
                float currentWidth = Mathf.Lerp(0, distance, timePassed / duration);

                // Apply width and fixed thickness
                lineRect.sizeDelta = new Vector2(currentWidth, lineThickness);

                yield return null;
            }

            // Ensure it snaps perfectly to the end distance
            lineRect.sizeDelta = new Vector2(distance, lineThickness);
        }

        // The graph is fully drawn -> Trigger the Event!
        drawingCoroutine = null;
        OnDrawingFinished?.Invoke();
    }
}