using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

public class DrawLineController : MonoBehaviour
{
    // =========================================================
    // LINE TYPE
    // =========================================================
    public enum LineType { Full, Dotted }

    // =========================================================
    // DOT LABEL CONFIG (NEW FEATURE)
    // =========================================================
    [System.Serializable]
    public class DotLabelConfig
    {
        [Tooltip("The transform where this dot and label should appear")]
        public Transform pointTransform;

        [Tooltip("Label text (e.g., 'A', 'P1')")]
        public string label = "P";

        public Vector3 offset = new Vector3(0.002f, 0f, 0f);
    }

    // =========================================================
    // SLIDE DRAWING RULE
    // =========================================================
    [System.Serializable]
    public class SlideDrawingRule
    {
        [Header("Slide")]
        public int slideIndex;

        [Header("Hierarchy Settings")]
        [Tooltip("The parent object where this line will be created. Leave empty to create it at the root of the scene.")]
        public Transform lineParent; // <--- NEW: Parent transform for the line

        [Header("Line Type")]
        public LineType lineType = LineType.Full;

        [Header("Drawing Points")]
        public Transform startPoint;
        public Transform endPoint;

        [Header("Drawing Settings")]
        public float startPointRadius = 0.01f;
        public float endPointRadius = 0.01f;

        [Header("Dotted Line Settings")]
        public float dotLength = 0.002f;
        public float dotSpacing = 0.002f;

        [Header("Point Labels")]
        public List<DotLabelConfig> pointLabels = new List<DotLabelConfig>();

        [Header("Events (Per Element)")]
        [Tooltip("Fires only when THIS specific line finishes drawing.")]
        public UnityEvent onLineDrawn;

        private void OnValidate()
        {
            dotLength = Mathf.Max(0.00001f, dotLength);
            dotSpacing = Mathf.Max(0.00001f, dotSpacing);
        }
    }

    // =========================================================
    // SLIDE DRAWING DATA
    // =========================================================
    private class SlideDrawingData
    {
        public List<Vector3> points = new List<Vector3>();
        public bool completed;
        public GameObject lineObject;
        public List<GameObject> lineSegments = new List<GameObject>();
    }

    // =========================================================
    // PUBLIC FIELDS
    // =========================================================
    [Header("Pencil References")]
    public Transform pencilObject;
    public Transform pencilTip;

    [Header("Input Settings")]
    public bool requirePencilClick = true;
    public LayerMask pencilLayer = ~0;

    [Header("Slide Drawing Rules")]
    public List<SlideDrawingRule> slideRules = new List<SlideDrawingRule>();

    [Header("Line Settings")]
    public float lineWidth = 0.004f;
    public float surfaceOffset = 0.003f;
    public Color lineColor = Color.black;

    [Header("Global Events")]
    [Tooltip("Fires when ANY line finishes drawing.")]
    public UnityEvent onDrawComplete;

    [HideInInspector]
    public float currentDistance;

    // =========================================================
    // RUNTIME STATE
    // =========================================================
    private bool isDragging = false;
    private SlideDrawingRule currentRule;
    private SlideDrawingData currentSlideData;
    private int currentSlideIndex = -1;
    private Vector3 lineDirection;
    private float lineLength;
    private float surfaceY;

    // OPTIMIZATION: Track last updated distance to avoid running loops when standing still
    private float lastUpdateDistance = -1f;

    // OPTIMIZATION: Cache the material once
    private Material sharedLineMaterial;

    private Dictionary<int, SlideDrawingData> slideDataDictionary = new Dictionary<int, SlideDrawingData>();
    private Camera mainCamera;

    // Store active markers to easily clear multiple labels
    private List<GameObject> activeMarkers = new List<GameObject>();

    // =========================================================
    // START
    // =========================================================
    private void Start()
    {
        mainCamera = Camera.main;

        if (pencilObject == null || pencilTip == null)
        {
            Debug.LogError("DrawLine: Pencil references are missing.", this);
            return;
        }

        // OPTIMIZATION: Create the material ONCE at startup
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            sharedLineMaterial = new Material(shader);
            sharedLineMaterial.color = lineColor;
        }

        SlideController.OnSlideChanged += OnSlideChanged;
        LoadSlide(SlideController.CurrentIndex);
    }

    // =========================================================
    // UPDATE
    // =========================================================
    private void Update()
    {
        if (!requirePencilClick || mainCamera == null) return;

        if (Input.GetMouseButtonDown(0)) TryStartDrawing();
        else if (Input.GetMouseButtonUp(0)) StopDrawing();
        else if (Input.GetMouseButton(0) && isDragging)
        {
            ContinueDrawing();
        }
    }

    private void OnDestroy()
    {
        SlideController.OnSlideChanged -= OnSlideChanged;

        if (sharedLineMaterial != null) Destroy(sharedLineMaterial);
    }

    private void OnSlideChanged(int slideIndex)
    {
        isDragging = false;
        LoadSlide(slideIndex);
    }

    // =========================================================
    // LOAD SLIDE
    // =========================================================
    private void LoadSlide(int slideIndex)
    {
        currentSlideIndex = slideIndex;
        currentRule = null;
        lastUpdateDistance = -1f; // Reset update threshold

        foreach (var rule in slideRules)
        {
            if (rule != null && rule.slideIndex == slideIndex)
            {
                currentRule = rule;
                break;
            }
        }

        ClearSlideMarkers();

        if (currentRule == null || currentRule.startPoint == null || currentRule.endPoint == null)
        {
            currentSlideData = null;
            return;
        }

        CalculateLineData();
        currentSlideData = GetOrCreateSlideData(slideIndex);
        MovePencilToSlideStart();
        RestoreSlideLine();
    }

    private void MovePencilToSlideStart()
    {
        if (pencilObject == null || pencilTip == null || currentRule?.startPoint == null) return;
        Vector3 targetTipPosition = GetSurfacePosition(currentRule.startPoint.position);
        pencilObject.position = targetTipPosition - (pencilTip.position - pencilObject.position);
    }

    private SlideDrawingData GetOrCreateSlideData(int slideIndex)
    {
        if (!slideDataDictionary.TryGetValue(slideIndex, out var data))
        {
            data = new SlideDrawingData();
            slideDataDictionary.Add(slideIndex, data);
        }
        return data;
    }

    private void CalculateLineData()
    {
        lineDirection = (currentRule.endPoint.position - currentRule.startPoint.position).normalized;
        lineLength = Vector3.Distance(currentRule.startPoint.position, currentRule.endPoint.position);
        surfaceY = currentRule.startPoint.position.y + surfaceOffset;
    }

    private Vector3 GetSurfacePosition(Vector3 position)
    {
        position.y = surfaceY;
        return position;
    }

    // =========================================================
    // DRAWING LOGIC
    // =========================================================
    private void TryStartDrawing()
    {
        if (currentRule == null || currentSlideData == null || currentSlideData.completed) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, pencilLayer))
        {
            if (hit.transform == pencilObject || hit.transform.IsChildOf(pencilObject))
            {
                isDragging = true;
                CreateSlideLine();

                if (currentSlideData.points.Count == 0)
                {
                    currentSlideData.points.Add(GetSurfacePosition(currentRule.startPoint.position));
                }
                UpdateLine();
            }
        }
    }

    private void ContinueDrawing()
    {
        if (!isDragging || currentRule == null || currentSlideData == null) return;

        Vector3 mouse = Input.mousePosition;
        mouse.z = mainCamera.WorldToScreenPoint(currentRule.startPoint.position).z;
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mouse);

        float distance = Vector3.Dot(worldPos - currentRule.startPoint.position, lineDirection);
        distance = Mathf.Clamp(distance, 0f, lineLength);
        distance = Mathf.Max(distance, currentDistance); // Lock backward movement

        currentDistance = distance;

        Vector3 desiredTipPosition = currentRule.startPoint.position + lineDirection * distance;
        desiredTipPosition.y = surfaceY;
        pencilObject.position = desiredTipPosition - (pencilTip.position - pencilObject.position);

        currentSlideData.points.Clear();
        currentSlideData.points.Add(GetSurfacePosition(currentRule.startPoint.position));
        currentSlideData.points.Add(GetSurfacePosition(pencilTip.position));

        if (Mathf.Abs(currentDistance - lastUpdateDistance) > 0.0001f)
        {
            UpdateLine();
            lastUpdateDistance = currentDistance;
        }

        if (!currentSlideData.completed && distance >= lineLength - currentRule.endPointRadius)
        {
            CompleteDrawing();
        }
    }

    private void StopDrawing() => isDragging = false;

    private void CreateSlideLine()
    {
        if (currentSlideData.lineObject == null)
        {
            // Create the new line object
            currentSlideData.lineObject = new GameObject("DrawLine_Slide_" + currentSlideIndex);

            // <--- NEW: Set the parent if one was assigned in the Inspector
            if (currentRule != null && currentRule.lineParent != null)
            {
                currentSlideData.lineObject.transform.SetParent(currentRule.lineParent, true);
            }
        }
        else
        {
            currentSlideData.lineObject.SetActive(true);
        }
    }

    private GameObject GetOrCreateSegment(int index)
    {
        while (currentSlideData.lineSegments.Count <= index)
        {
            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(seg.GetComponent<Collider>());
            seg.transform.SetParent(currentSlideData.lineObject.transform);

            Renderer rend = seg.GetComponent<Renderer>();
            if (sharedLineMaterial != null)
                rend.sharedMaterial = sharedLineMaterial;

            currentSlideData.lineSegments.Add(seg);
        }
        return currentSlideData.lineSegments[index];
    }

    private void UpdateLine()
    {
        if (currentSlideData?.lineObject == null || currentSlideData.points.Count < 2) return;

        if (currentRule.lineType == LineType.Full) UpdateFullLine();
        else
        {
            UpdateDottedLine();
        }
    }

    private void UpdateFullLine()
    {
        Vector3 start = currentSlideData.points[0];
        Vector3 end = currentSlideData.points[1];
        float dist = Vector3.Distance(start, end);

        if (dist <= 0.0001f)
        {
            foreach (var seg in currentSlideData.lineSegments) seg.SetActive(false);
            return;
        }

        GameObject segMain = GetOrCreateSegment(0);
        segMain.SetActive(true);
        segMain.transform.position = (start + end) / 2f;
        segMain.transform.rotation = Quaternion.LookRotation(end - start);
        segMain.transform.localScale = new Vector3(lineWidth, 0.0001f, dist);

        for (int i = 1; i < currentSlideData.lineSegments.Count; i++)
            currentSlideData.lineSegments[i].SetActive(false);
    }

    private void UpdateDottedLine()
    {
        if (currentSlideData == null || currentSlideData.lineObject == null)
            return;

        if (currentSlideData.points.Count < 2)
            return;

        Vector3 start = currentSlideData.points[0];
        Vector3 end = currentSlideData.points[1];

        float dist = Vector3.Distance(start, end);

        int activeSegments = 0;

        // Nothing to draw
        if (dist <= 0.0001f)
        {
            for (int i = 0; i < currentSlideData.lineSegments.Count; i++)
            {
                if (currentSlideData.lineSegments[i] != null)
                    currentSlideData.lineSegments[i].SetActive(false);
            }

            return;
        }

        // ---------------------------------------------------------
        // SAFETY CHECK
        // ---------------------------------------------------------

        float dotLength = Mathf.Max(currentRule.dotLength, 0.00001f);
        float dotSpacing = Mathf.Max(currentRule.dotSpacing, 0.00001f);

        float step = dotLength + dotSpacing;

        // Extra protection against invalid values
        if (step <= 0.00001f)
        {
            Debug.LogWarning(
                "DrawLineController: Invalid dotLength/dotSpacing. " +
                "Dotted line update cancelled.",
                this
            );

            return;
        }

        Vector3 dir = (end - start).normalized;

        float currentTravel = 0f;

        // ---------------------------------------------------------
        // MAXIMUM SEGMENT SAFETY LIMIT
        // ---------------------------------------------------------

        const int MAX_SEGMENTS = 500;

        while (currentTravel < dist && activeSegments < MAX_SEGMENTS)
        {
            float remainingDistance = dist - currentTravel;

            float actualDotLen = Mathf.Min(dotLength, remainingDistance);

            if (actualDotLen <= 0.00001f)
                break;

            GameObject seg = GetOrCreateSegment(activeSegments);

            if (seg == null)
                break;

            seg.SetActive(true);

            Vector3 dotStartPos = start + dir * currentTravel;

            seg.transform.position = dotStartPos + dir * (actualDotLen * 0.5f);

            seg.transform.rotation = Quaternion.LookRotation(dir);

            seg.transform.localScale = new Vector3(lineWidth, 0.0001f, actualDotLen);

            activeSegments++;

            // IMPORTANT:
            // Always move forward.
            currentTravel += step;
        }

        // ---------------------------------------------------------
        // DISABLE UNUSED SEGMENTS
        // ---------------------------------------------------------

        for (int i = activeSegments; i < currentSlideData.lineSegments.Count; i++)
        {
            if (currentSlideData.lineSegments[i] != null)
                currentSlideData.lineSegments[i].SetActive(false);
        }

        // ---------------------------------------------------------
        // WARNING IF SAFETY LIMIT WAS HIT
        // ---------------------------------------------------------

        if (activeSegments >= MAX_SEGMENTS && currentTravel < dist)
        {
            Debug.LogWarning(
                "DrawLineController: Maximum dotted line segment limit reached. " +
                "Check dotLength, dotSpacing and line length.",
                this
            );
        }
    }

    private void CompleteDrawing()
    {
        if (currentSlideData == null) return;

        currentSlideData.completed = true;
        isDragging = false;
        currentDistance = lineLength;

        Vector3 targetEnd = GetSurfacePosition(currentRule.endPoint.position);
        pencilObject.position = targetEnd - (pencilTip.position - pencilObject.position);

        currentSlideData.points.Clear();
        currentSlideData.points.Add(GetSurfacePosition(currentRule.startPoint.position));
        currentSlideData.points.Add(GetSurfacePosition(pencilTip.position));

        UpdateLine();

        currentRule?.onLineDrawn?.Invoke();

        // Fire the global event
        onDrawComplete?.Invoke();
    }

    private void RestoreSlideLine()
    {
        if (currentSlideData == null) return;

        if (currentSlideData.lineSegments.Count == 0 && currentSlideData.lineObject == null)
        {
            currentDistance = 0f;
            return;
        }

        if (currentSlideData.lineObject != null) currentSlideData.lineObject.SetActive(true);

        UpdateLine();
        currentDistance = currentSlideData.completed ? lineLength : CalculateSavedDistance();

        if (currentDistance > 0 && currentRule != null)
        {
            Vector3 targetTipPosition = currentRule.startPoint.position + lineDirection * currentDistance;
            targetTipPosition.y = surfaceY;
            pencilObject.position = targetTipPosition - (pencilTip.position - pencilObject.position);
        }
    }

    private float CalculateSavedDistance()
    {
        if (currentSlideData == null || currentSlideData.points.Count < 2) return 0f;
        float distance = Vector3.Dot(currentSlideData.points[1] - currentRule.startPoint.position, lineDirection);
        return Mathf.Clamp(distance, 0f, lineLength);
    }

    public void ClearLine()
    {
        if (currentSlideData == null) return;

        if (currentSlideData.lineObject != null)
        {
            Destroy(currentSlideData.lineObject);
            currentSlideData.lineObject = null;
            currentSlideData.lineSegments.Clear();
        }

        currentSlideData.points.Clear();
        currentSlideData.completed = false;
        currentDistance = 0f;
        isDragging = false;
        MovePencilToSlideStart();
    }

    public void ClearAllLines()
    {
        foreach (var pair in slideDataDictionary)
        {
            if (pair.Value.lineObject != null) Destroy(pair.Value.lineObject);
        }
        slideDataDictionary.Clear();
        currentSlideData = null;
        currentDistance = 0f;
        isDragging = false;
        MovePencilToSlideStart();
    }

    // =========================================================
    // MARKERS
    // =========================================================
    public void ShowSlidePoints()
    {
        if (currentRule == null) return;

        // Clear first to prevent duplicate dots if the event is called multiple times
        ClearSlideMarkers();

        foreach (var labelConfig in currentRule.pointLabels)
        {
            if (labelConfig.pointTransform != null)
            {
                Vector3 pos = GetSurfacePosition(labelConfig.pointTransform.position);
                CreateDot(pos);
                CreateCharacter(pos, labelConfig.label, labelConfig.offset);
            }
        }
    }

    private void ClearSlideMarkers()
    {
        foreach (var marker in activeMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        activeMarkers.Clear();
    }

    private void CreateDot(Vector3 pos)
    {
        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.transform.position = pos;
        dot.transform.localScale = Vector3.one * 0.0015f;

        Renderer rend = dot.GetComponent<Renderer>();
        if (rend != null && sharedLineMaterial != null)
        {
            rend.sharedMaterial = sharedLineMaterial;
        }

        Destroy(dot.GetComponent<Collider>());
        activeMarkers.Add(dot);
    }

    private void CreateCharacter(Vector3 pos, string labelText, Vector3 offset)
    {
        GameObject charObj = new GameObject("DotCharacter");
        charObj.transform.position = pos + offset;

        TextMesh textMesh = charObj.AddComponent<TextMesh>();
        textMesh.text = labelText;
        textMesh.fontSize = 100;
        textMesh.characterSize = 0.0008f;
        textMesh.color = Color.black;
        textMesh.anchor = TextAnchor.MiddleCenter;

        if (mainCamera != null) charObj.transform.rotation = mainCamera.transform.rotation;

        activeMarkers.Add(charObj);
    }
}