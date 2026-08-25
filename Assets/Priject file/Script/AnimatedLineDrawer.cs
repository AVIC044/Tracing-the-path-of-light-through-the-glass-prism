using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class AnimatedLineDrawer : MonoBehaviour
{
    // =========================================================
    // DATA CLASSES
    // =========================================================

    public enum LineType
    {
        Full,
        Dotted
    }

    [System.Serializable]
    public class DotLabelConfig
    {
        [Tooltip("The transform where this dot and label should appear")]
        public Transform pointTransform;

        [Tooltip("Label text")]
        public string label = "P";

        public Vector3 offset = new Vector3(0.002f, 0f, 0f);
    }

    [System.Serializable]
    public class SlideDrawingRule
    {
        // Add slide-specific logic here if needed
    }

    // =========================================================
    // POINT SETUP
    // =========================================================

    [Header("Points Setup")]

    [Tooltip("Starting points for each line")]
    public Transform[] startPoints;

    [Tooltip("Ending points for each line")]
    public Transform[] endPoints;


    // =========================================================
    // LINE PARENT
    // =========================================================

    [Header("Line Parent")]

    [Tooltip("All generated line objects will become children of this GameObject")]
    public Transform lineParent;


    // =========================================================
    // DRAWING SETTINGS
    // =========================================================

    [Header("Drawing Settings")]

    [Tooltip("Time taken to draw all lines")]
    public float drawDuration = 2f;

    [Tooltip("Width of the lines")]
    public float lineWidth = 0.05f;

    [Tooltip("Color of the lines")]
    public Color lineColor = Color.black;


    // =========================================================
    // UNITY EVENTS
    // =========================================================

    [Header("Events")]

    [Tooltip("Triggered when all lines finish drawing")]
    public UnityEvent onAllLinesDrawCompleted;


    // =========================================================
    // PRIVATE VARIABLES
    // =========================================================

    private List<LineRenderer> lineRenderers =
        new List<LineRenderer>();

    private float currentDrawTime = 0f;

    private bool isDrawing = false;

    private bool hasCompleted = false;

    private Material sharedLineMaterial;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        // Check whether arrays are assigned
        if (startPoints == null || endPoints == null)
        {
            Debug.LogError(
                "AnimatedLineDrawer Error: Start Points or End Points array is not assigned."
            );

            return;
        }

        // Check Start Points count
        if (startPoints.Length == 0)
        {
            Debug.LogError(
                "AnimatedLineDrawer Error: No Start Points assigned."
            );

            return;
        }

        // Check End Points count
        if (endPoints.Length == 0)
        {
            Debug.LogError(
                "AnimatedLineDrawer Error: No End Points assigned."
            );

            return;
        }

        // Check whether both arrays have the same size
        if (startPoints.Length != endPoints.Length)
        {
            Debug.LogError(
                "AnimatedLineDrawer Error: Start Points Count = "
                + startPoints.Length
                + " | End Points Count = "
                + endPoints.Length
                + ". Both must have the same number of elements."
            );

            return;
        }

        // Check if any point is missing
        for (int i = 0; i < startPoints.Length; i++)
        {
            if (startPoints[i] == null)
            {
                Debug.LogError(
                    "AnimatedLineDrawer Error: Start Point at Element "
                    + i
                    + " is empty."
                );

                return;
            }

            if (endPoints[i] == null)
            {
                Debug.LogError(
                    "AnimatedLineDrawer Error: End Point at Element "
                    + i
                    + " is empty."
                );

                return;
            }
        }

        // Prevent divide-by-zero
        if (drawDuration <= 0f)
        {
            Debug.LogWarning(
                "Draw Duration cannot be 0 or negative. Setting it to 0.1."
            );

            drawDuration = 0.1f;
        }

        // If no parent is assigned, use this GameObject
        if (lineParent == null)
        {
            lineParent = transform;
        }

        // Create material
        CreateLineMaterial();

        // Create lines
        CreateLines();

        // Start animation
        currentDrawTime = 0f;

        hasCompleted = false;

        isDrawing = true;
    }


    // =========================================================
    // CREATE MATERIAL
    // =========================================================

    private void CreateLineMaterial()
    {
        Shader shader =
            Shader.Find("Sprites/Default")
            ?? Shader.Find("Unlit/Color");

        if (shader == null)
        {
            Debug.LogError(
                "AnimatedLineDrawer Error: Could not find a suitable shader."
            );

            return;
        }

        sharedLineMaterial = new Material(shader);

        sharedLineMaterial.color = lineColor;
    }


    // =========================================================
    // CREATE LINES
    // =========================================================

    private void CreateLines()
    {
        lineRenderers.Clear();

        for (int i = 0; i < startPoints.Length; i++)
        {
            // Create line GameObject
            GameObject lineObj =
                new GameObject("Line_" + (i + 1));

            // Set the assigned parent
            lineObj.transform.SetParent(
                lineParent,
                true
            );

            // Add Line Renderer
            LineRenderer lr =
                lineObj.AddComponent<LineRenderer>();

            // Set positions
            lr.positionCount = 2;

            // Set width
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;

            // Assign material
            if (sharedLineMaterial != null)
            {
                lr.sharedMaterial = sharedLineMaterial;
            }

            // Get start position
            Vector3 startPosition =
                startPoints[i].position;

            // Initially both points are at start
            lr.SetPosition(
                0,
                startPosition
            );

            lr.SetPosition(
                1,
                startPosition
            );

            // Add LineRenderer to list
            lineRenderers.Add(lr);
        }
    }


    // =========================================================
    // UPDATE DRAWING
    // =========================================================

    private void Update()
    {
        if (!isDrawing)
            return;

        // Increase drawing time
        currentDrawTime += Time.deltaTime;

        // Calculate drawing percentage
        float t =
            Mathf.Clamp01(
                currentDrawTime / drawDuration
            );

        // Update every line
        for (int i = 0; i < lineRenderers.Count; i++)
        {
            if (lineRenderers[i] == null)
                continue;

            // Calculate current line position
            Vector3 currentPosition =
                Vector3.Lerp(
                    startPoints[i].position,
                    endPoints[i].position,
                    t
                );

            // Move line end point
            lineRenderers[i].SetPosition(
                1,
                currentPosition
            );
        }

        // All lines completed
        if (t >= 1f)
        {
            isDrawing = false;

            // Trigger event only once
            if (!hasCompleted)
            {
                hasCompleted = true;

                Debug.Log(
                    "AnimatedLineDrawer: All lines finished drawing."
                );

                // Invoke Unity Event
                onAllLinesDrawCompleted?.Invoke();
            }
        }
    }


    // =========================================================
    // DRAW AGAIN
    // =========================================================

    public void StartDrawing()
    {
        if (lineRenderers.Count == 0)
        {
            Debug.LogWarning(
                "No lines available to draw."
            );

            return;
        }

        // Reset all lines
        for (int i = 0; i < lineRenderers.Count; i++)
        {
            if (lineRenderers[i] == null)
                continue;

            lineRenderers[i].SetPosition(
                0,
                startPoints[i].position
            );

            lineRenderers[i].SetPosition(
                1,
                startPoints[i].position
            );
        }

        // Reset timer
        currentDrawTime = 0f;

        // Allow completion event again
        hasCompleted = false;

        // Start drawing
        isDrawing = true;
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (sharedLineMaterial != null)
        {
            Destroy(sharedLineMaterial);
        }
    }
}