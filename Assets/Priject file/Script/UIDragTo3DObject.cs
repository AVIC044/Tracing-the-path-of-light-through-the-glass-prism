using System.Collections;
using UnityEngine;
using UnityEngine.Events; // <-- ADDED THIS NAMESPACE
using UnityEngine.EventSystems;

public class UIDragTo3DObject : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("UI")]
    [SerializeField] private RectTransform uiImage;

    [Header("Camera")]
    [SerializeField] private Camera mainCamera;

    [Header("Correct 3D Object")]
    [SerializeField] private GameObject target3DObject;

    [Header("Drop Collider")]
    [Tooltip("Collider that represents the valid drop area for this object.")]
    [SerializeField] private Collider dropCollider;

    [Header("Highlight Settings")]
    [SerializeField] private Material highlightMaterial;
    [Tooltip("If false, only the parent object gets highlighted. If true, child objects are highlighted too.")]
    [SerializeField] private bool applyHighlightToChildren = false;

    [Header("Return Settings")]
    [SerializeField] private float returnSpeed = 8f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip correctSound;

    [Header("Events")]
    [Tooltip("Fired when the UI object is successfully dropped on the correct 3D target.")]
    public UnityEvent OnObjectPlaced; // <-- ADDED THIS EVENT

    // Original UI position
    private Vector3 initialPosition;

    // Canvas
    private Canvas canvas;
    private RectTransform canvasRect;

    // Renderers
    private Renderer[] targetRenderers;     // Used for Hiding/Showing the whole object
    private Renderer[] highlightRenderers;  // Used specifically for Material swapping

    // Original materials
    private Material[][] originalMaterials;

    // Return coroutine
    private Coroutine returnCoroutine;

    // State
    private bool isCompleted = false;


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        // Get UI RectTransform
        if (uiImage == null)
            uiImage = GetComponent<RectTransform>();

        // Camera
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Canvas
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            canvasRect = canvas.GetComponent<RectTransform>();

        // Audio Source
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Store original position
        initialPosition = uiImage.position;


        // -----------------------------------------------------
        // GET RENDERERS & MATERIALS
        // -----------------------------------------------------

        if (target3DObject != null)
        {
            // 1. Get ALL renderers to hide/show the entire 3D object (true includes inactive ones)
            targetRenderers = target3DObject.GetComponentsInChildren<Renderer>(true);

            // 2. Decide which renderers get the highlight material
            if (applyHighlightToChildren)
            {
                highlightRenderers = targetRenderers;
            }
            else
            {
                highlightRenderers = target3DObject.GetComponents<Renderer>();
            }

            // 3. Store original materials ONLY for the ones we plan to highlight
            originalMaterials = new Material[highlightRenderers.Length][];

            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                originalMaterials[i] = highlightRenderers[i].sharedMaterials;
            }
        }


        // -----------------------------------------------------
        // AUTO FIND DROP COLLIDER
        // -----------------------------------------------------

        if (dropCollider == null && target3DObject != null)
        {
            dropCollider = target3DObject.GetComponentInChildren<Collider>(true);
        }
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        uiImage.gameObject.SetActive(true);
        HideTarget();
        RestoreOriginalMaterial();
    }


    // =========================================================
    // ENABLE / DISABLE (Controls Collider based on UI Parent)
    // =========================================================

    private void OnEnable()
    {
        if (dropCollider != null && !isCompleted)
        {
            dropCollider.enabled = true;
        }
    }

    private void OnDisable()
    {
        if (dropCollider != null)
        {
            dropCollider.enabled = false;
        }
    }


    // =========================================================
    // BEGIN DRAG
    // =========================================================

    private void UpdateDragPosition(PointerEventData eventData)
    {
        if (uiImage == null) return;
        uiImage.position = eventData.position;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isCompleted) return;

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        UpdateDragPosition(eventData);

        // This will force the target active and apply materials
        ShowTarget();
        ApplyHighlight();
    }


    public void OnDrag(PointerEventData eventData)
    {
        if (isCompleted) return;
        UpdateDragPosition(eventData);
    }


    // =========================================================
    // END DRAG
    // =========================================================

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isCompleted) return;

        bool correctDrop = CheckDrop(eventData.position);

        if (correctDrop)
        {
            CorrectDrop();
        }
        else
        {
            WrongDrop();
        }
    }


    // =========================================================
    // CHECK DROP
    // =========================================================

    private bool CheckDrop(Vector2 screenPosition)
    {
        if (mainCamera == null) return false;

        if (dropCollider == null)
        {
            Debug.LogWarning(gameObject.name + " has no Drop Collider assigned.");
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == dropCollider) return true;
            if (hit.collider.transform.IsChildOf(dropCollider.transform)) return true;
        }

        return false;
    }

    private void CorrectDrop()
    {
        isCompleted = true;

        PlaySound(correctSound);

        RestoreOriginalMaterial();
        ShowTarget(); // Object stays visible in its normal materials

        uiImage.gameObject.SetActive(false);

        if (dropCollider != null)
        {
            dropCollider.enabled = false;
        }

        // TRIGGER THE EVENT HERE
        OnObjectPlaced?.Invoke();
    }


    // =========================================================
    // WRONG DROP
    // =========================================================

    private void WrongDrop()
    {
        isCompleted = false;

        RestoreOriginalMaterial();
        HideTarget();

        uiImage.gameObject.SetActive(true);

        if (dropCollider != null)
        {
            dropCollider.enabled = true;
        }

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
        }

        returnCoroutine = StartCoroutine(ReturnToStart());
    }


    // =========================================================
    // AUDIO HELPER
    // =========================================================

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }


    // =========================================================
    // SHOW TARGET
    // =========================================================

    private void ShowTarget()
    {
        // 1. Force the GameObject to be active if it was disabled in the inspector
        if (target3DObject != null && !target3DObject.activeSelf)
        {
            target3DObject.SetActive(true);
        }

        // 2. Enable all renderers
        if (targetRenderers == null) return;
        foreach (Renderer renderer in targetRenderers)
        {
            if (renderer != null) renderer.enabled = true;
        }
    }


    // =========================================================
    // HIDE TARGET
    // =========================================================

    private void HideTarget()
    {
        if (targetRenderers == null) return;

        foreach (Renderer renderer in targetRenderers)
        {
            if (renderer != null) renderer.enabled = false;
        }
    }


    // =========================================================
    // APPLY HIGHLIGHT 
    // =========================================================

    private void ApplyHighlight()
    {
        if (highlightMaterial == null || highlightRenderers == null) return;

        foreach (Renderer renderer in highlightRenderers)
        {
            if (renderer == null) continue;

            Material[] materials = new Material[renderer.sharedMaterials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = highlightMaterial;
            }

            renderer.materials = materials;
        }
    }


    // =========================================================
    // RESTORE ORIGINAL MATERIAL
    // =========================================================

    private void RestoreOriginalMaterial()
    {
        if (highlightRenderers == null || originalMaterials == null) return;

        for (int i = 0; i < highlightRenderers.Length; i++)
        {
            if (highlightRenderers[i] == null) continue;

            highlightRenderers[i].sharedMaterials = originalMaterials[i];
        }
    }


    // =========================================================
    // SMOOTH RETURN
    // =========================================================

    private IEnumerator ReturnToStart()
    {
        Vector3 startPosition = uiImage.position;
        float duration = 0.15f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            uiImage.position = Vector3.Lerp(startPosition, initialPosition, t);
            yield return null;
        }

        uiImage.position = initialPosition;
        returnCoroutine = null;
    }


    // =========================================================
    // RESET THIS OBJECT
    // =========================================================

    public void ResetObject()
    {
        isCompleted = false;

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        uiImage.gameObject.SetActive(true);

        if (dropCollider != null)
        {
            dropCollider.enabled = true;
        }

        uiImage.position = initialPosition;

        RestoreOriginalMaterial();
        HideTarget();
    }
}