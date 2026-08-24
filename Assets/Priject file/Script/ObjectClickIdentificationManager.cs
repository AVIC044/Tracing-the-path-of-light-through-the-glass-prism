using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class ObjectClickIdentificationManager : MonoBehaviour
{
    // =========================================================
    // IDENTIFICATION TARGET
    // =========================================================

    [System.Serializable]
    public class IdentificationTarget
    {
        // -----------------------------------------------------
        // OBJECT
        // -----------------------------------------------------

        [Header("Target Identification Item")]

        public string itemName = "Rheostat";

        [Tooltip(
            "The 3D clickable object. Must have a Collider."
        )]
        public Collider clickableCollider;


        // -----------------------------------------------------
        // CAMERA
        // -----------------------------------------------------

        [Header("Camera Point")]

        [Tooltip(
            "Camera moves to this point when this object is clicked."
        )]
        public Transform cameraPoint;


        // -----------------------------------------------------
        // DESCRIPTION
        // -----------------------------------------------------

        [Header("Description Content")]

        [Tooltip(
            "Title shown in the common Description UI."
        )]
        public string title = "Rheostat";

        [TextArea(3, 10)]
        [Tooltip(
            "Description shown in the common Description UI."
        )]
        public string description =
            "Enter the description for this object here.";
    }


    // =========================================================
    // CAMERA CONFIGURATION
    // =========================================================

    [Header("Camera Configuration")]

    [Tooltip(
        "The camera that will move."
    )]
    [SerializeField]
    private Camera mainCamera;

    [Tooltip(
        "The default camera position."
    )]
    [SerializeField]
    private Transform defaultCameraPoint;


    // =========================================================
    // CAMERA MOVEMENT
    // =========================================================

    [Header("Camera Movement")]

    [Tooltip(
        "Time taken to move the camera."
    )]
    [SerializeField]
    private float cameraMoveDuration = 1f;

    [Tooltip(
        "Animation Curve used for smooth camera movement."
    )]
    [SerializeField]
    private AnimationCurve cameraMovementCurve =
        AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f
        );


    // =========================================================
    // COMMON DESCRIPTION UI
    // =========================================================

    [Header("Common Description UI")]

    [Tooltip(
        "The complete description UI panel. It is shared by every object."
    )]
    [SerializeField]
    private GameObject descriptionUI;

    [Tooltip(
        "Common title text. The clicked object's title is placed here."
    )]
    [SerializeField]
    private TMP_Text titleText;

    [Tooltip(
        "Common description text. The clicked object's description is placed here."
    )]
    [SerializeField]
    private TMP_Text descriptionText;

    [Tooltip(
        "Back button used to return to the default camera."
    )]
    [SerializeField]
    private Button backButton;


    // =========================================================
    // CORRECT TARGETS
    // =========================================================

    [Header("Correct Clickable Items Setup")]

    [SerializeField]
    private List<IdentificationTarget> correctTargets =
        new List<IdentificationTarget>();


    // =========================================================
    // WRONG TARGETS
    // =========================================================

    [Header("Wrong Clickable Items Setup")]

    [Tooltip(
        "Objects that are considered wrong answers."
    )]
    [SerializeField]
    private List<Collider> wrongColliders =
        new List<Collider>();


    // =========================================================
    // AUDIO SETTINGS
    // =========================================================

    [Header("Audio Settings")]

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip correctSound;

    [SerializeField]
    private AudioClip wrongSound;


    // =========================================================
    // WORLD SPACE POPUPS
    // =========================================================

    [Header("World Space Canvas & Prefabs")]

    [SerializeField]
    private Transform worldSpaceCanvasParent;

    [SerializeField]
    private GameObject correctWorldSpacePrefab;

    [SerializeField]
    private GameObject wrongWorldSpacePrefab;

    [SerializeField]
    private float yOffsetDistance = 0.05f;

    [SerializeField]
    private Vector3 spawnScaleMultiplier =
        new Vector3(
            0.005f,
            0.005f,
            0.005f
        );


    // =========================================================
    // EVENTS
    // =========================================================

    [Header("Events")]

    public UnityEvent OnCorrectObjectClicked;

    public UnityEvent OnWrongObjectClicked;

    public UnityEvent OnAllObjectsCompleted;


    // =========================================================
    // RUNTIME DATA
    // =========================================================

    private readonly HashSet<int> completedIndices =
        new HashSet<int>();

    private readonly HashSet<Collider> spawnedWrongColliders =
        new HashSet<Collider>();

    private readonly List<Transform> activePopups =
        new List<Transform>();

    private Coroutine cameraMoveCoroutine;

    private bool isCameraMoving;

    private int currentTargetIndex = -1;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        InitializeDescriptionUI();

        InitializeBackButton();
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        UpdatePopupsFacingCamera();

        if (Input.GetMouseButtonDown(0))
        {
            HandleClick(
                Input.mousePosition
            );
        }
    }


    // =========================================================
    // UPDATE POPUP BILLBOARDING
    // =========================================================

    private void UpdatePopupsFacingCamera()
    {
        if (mainCamera == null)
            return;

        for (int i = activePopups.Count - 1; i >= 0; i--)
        {
            if (activePopups[i] == null)
            {
                activePopups.RemoveAt(i);
                continue;
            }

            activePopups[i].rotation = mainCamera.transform.rotation;
        }
    }


    // =========================================================
    // INITIALIZE DESCRIPTION UI
    // =========================================================

    private void InitializeDescriptionUI()
    {
        if (descriptionUI != null)
        {
            descriptionUI.SetActive(false);
        }

        if (titleText != null)
        {
            titleText.text = "";
        }

        if (descriptionText != null)
        {
            descriptionText.text = "";
        }
    }


    // =========================================================
    // INITIALIZE BACK BUTTON
    // =========================================================

    private void InitializeBackButton()
    {
        if (backButton == null)
            return;

        backButton.onClick.RemoveListener(
            GoBackToDefaultCamera
        );

        backButton.onClick.AddListener(
            GoBackToDefaultCamera
        );
    }


    // =========================================================
    // HANDLE CLICK
    // =========================================================

    private void HandleClick(
        Vector3 screenPoint
    )
    {
        if (mainCamera == null)
            return;

        // Do not allow object clicks while camera is moving.
        if (isCameraMoving)
            return;

        // Do not allow clicking objects while the description UI is open.
        if (
            descriptionUI != null &&
            descriptionUI.activeSelf
        )
        {
            return;
        }

        // Prevent raycasting into 3D world when clicking on UI buttons or canvas
        if (
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject()
        )
        {
            return;
        }

        Ray ray =
            mainCamera.ScreenPointToRay(
                screenPoint
            );

        if (
            Physics.Raycast(
                ray,
                out RaycastHit hit
            )
        )
        {
            int matchedIndex =
                GetMatchingCorrectTargetIndex(
                    hit.collider
                );

            if (matchedIndex != -1)
            {
                ProcessCorrectClick(
                    matchedIndex,
                    hit.collider
                );
            }
            else if (IsWrongColliderMatch(hit.collider, out Collider matchedWrongCollider))
            {
                ProcessWrongClick(
                    matchedWrongCollider
                );
            }
            // Clicking environment objects (tables, walls, floor) does nothing
        }
        // Clicking empty space or skybox does nothing
    }


    // =========================================================
    // FIND CORRECT TARGET
    // =========================================================

    private int GetMatchingCorrectTargetIndex(
        Collider hitCollider
    )
    {
        for (
            int i = 0;
            i < correctTargets.Count;
            i++
        )
        {
            IdentificationTarget target =
                correctTargets[i];

            if (
                target.clickableCollider == null
            )
            {
                continue;
            }

            if (
                target.clickableCollider ==
                hitCollider
            )
            {
                return i;
            }

            if (
                hitCollider.transform.IsChildOf(
                    target.clickableCollider.transform
                )
            )
            {
                return i;
            }
        }

        return -1;
    }


    // =========================================================
    // FIND WRONG TARGET
    // =========================================================

    private bool IsWrongColliderMatch(
        Collider hitCollider,
        out Collider matchedCollider
    )
    {
        for (int i = 0; i < wrongColliders.Count; i++)
        {
            Collider wrongCol = wrongColliders[i];

            if (wrongCol == null)
            {
                continue;
            }

            if (wrongCol == hitCollider || hitCollider.transform.IsChildOf(wrongCol.transform))
            {
                matchedCollider = wrongCol;
                return true;
            }
        }

        matchedCollider = null;
        return false;
    }


    // =========================================================
    // CORRECT CLICK
    // =========================================================

    private void ProcessCorrectClick(
        int index,
        Collider hitCollider
    )
    {
        if (
            index < 0 ||
            index >= correctTargets.Count
        )
        {
            return;
        }

        IdentificationTarget target =
            correctTargets[index];

        currentTargetIndex = index;

        PlaySound(correctSound);


        // -----------------------------------------------------
        // MARK AS COMPLETED
        // -----------------------------------------------------

        bool wasAlreadyCompleted =
            completedIndices.Contains(index);

        if (!wasAlreadyCompleted)
        {
            completedIndices.Add(index);
        }


        // -----------------------------------------------------
        // SPAWN CORRECT EFFECT
        // -----------------------------------------------------

        if (
            !wasAlreadyCompleted &&
            correctWorldSpacePrefab != null &&
            hitCollider != null
        )
        {
            Vector3 spawnPosition =
                CalculateTopPosition(
                    hitCollider
                );

            SpawnPopUpEffect(
                correctWorldSpacePrefab,
                spawnPosition
            );
        }


        // -----------------------------------------------------
        // EVENT
        // -----------------------------------------------------

        if (!wasAlreadyCompleted)
        {
            OnCorrectObjectClicked?.Invoke();
        }


        // -----------------------------------------------------
        // MOVE CAMERA
        // -----------------------------------------------------

        if (target.cameraPoint != null)
        {
            StartCameraMovement(
                target.cameraPoint,
                index,
                true
            );
        }
        else
        {
            Debug.LogWarning(
                "[Identification] Camera Point is missing for: " +
                target.itemName,
                this
            );

            // If no camera point exists, immediately show the description.
            ShowDescriptionUI(index);
        }


        // -----------------------------------------------------
        // ALL COMPLETED
        // -----------------------------------------------------

        if (
            !wasAlreadyCompleted &&
            completedIndices.Count >=
            correctTargets.Count
        )
        {
            OnAllObjectsCompleted?.Invoke();
        }
    }


    // =========================================================
    // WRONG CLICK
    // =========================================================

    private void ProcessWrongClick(
        Collider hitCollider
    )
    {
        PlaySound(wrongSound);

        if (
            hitCollider != null &&
            wrongWorldSpacePrefab != null
        )
        {
            if (
                !spawnedWrongColliders.Contains(
                    hitCollider
                )
            )
            {
                spawnedWrongColliders.Add(
                    hitCollider
                );

                Vector3 spawnPosition =
                    CalculateTopPosition(
                        hitCollider
                    );

                SpawnPopUpEffect(
                    wrongWorldSpacePrefab,
                    spawnPosition
                );
            }
        }

        OnWrongObjectClicked?.Invoke();
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
    // START CAMERA MOVEMENT
    // =========================================================

    private void StartCameraMovement(
        Transform destination,
        int targetIndex,
        bool showDescriptionAfterMovement
    )
    {
        if (
            mainCamera == null ||
            destination == null
        )
        {
            return;
        }

        if (cameraMoveCoroutine != null)
        {
            StopCoroutine(
                cameraMoveCoroutine
            );
        }

        cameraMoveCoroutine =
            StartCoroutine(
                MoveCameraSmoothly(
                    destination,
                    targetIndex,
                    showDescriptionAfterMovement
                )
            );
    }


    // =========================================================
    // SMOOTH CAMERA MOVEMENT
    // =========================================================

    private IEnumerator MoveCameraSmoothly(
        Transform destination,
        int targetIndex,
        bool showDescriptionAfterMovement
    )
    {
        isCameraMoving = true;

        Vector3 startPosition =
            mainCamera.transform.position;

        Quaternion startRotation =
            mainCamera.transform.rotation;

        Vector3 targetPosition =
            destination.position;

        Quaternion targetRotation =
            destination.rotation;

        float elapsed = 0f;

        while (
            elapsed <
            cameraMoveDuration
        )
        {
            if (
                mainCamera == null ||
                destination == null
            )
            {
                isCameraMoving = false;
                yield break;
            }

            elapsed += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    elapsed /
                    cameraMoveDuration
                );

            float curveValue =
                cameraMovementCurve.Evaluate(
                    normalizedTime
                );

            mainCamera.transform.position =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    curveValue
                );

            mainCamera.transform.rotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    curveValue
                );

            yield return null;
        }


        // -----------------------------------------------------
        // FINAL CAMERA POSITION
        // -----------------------------------------------------

        if (mainCamera != null)
        {
            mainCamera.transform.position =
                targetPosition;

            mainCamera.transform.rotation =
                targetRotation;
        }

        isCameraMoving = false;
        cameraMoveCoroutine = null;


        // -----------------------------------------------------
        // SHOW DESCRIPTION AFTER ARRIVING
        // -----------------------------------------------------

        if (
            showDescriptionAfterMovement &&
            targetIndex >= 0
        )
        {
            ShowDescriptionUI(
                targetIndex
            );
        }
    }


    // =========================================================
    // SHOW COMMON DESCRIPTION UI
    // =========================================================

    private void ShowDescriptionUI(
        int index
    )
    {
        if (
            index < 0 ||
            index >= correctTargets.Count
        )
        {
            return;
        }

        IdentificationTarget target =
            correctTargets[index];


        // -----------------------------------------------------
        // SET TITLE
        // -----------------------------------------------------

        if (titleText != null)
        {
            titleText.text =
                target.title;
        }


        // -----------------------------------------------------
        // SET DESCRIPTION
        // -----------------------------------------------------

        if (descriptionText != null)
        {
            descriptionText.text =
                target.description;
        }


        // -----------------------------------------------------
        // ENABLE COMMON UI
        // -----------------------------------------------------

        if (descriptionUI != null)
        {
            descriptionUI.SetActive(true);
        }
    }


    // =========================================================
    // BACK BUTTON
    // =========================================================

    public void GoBackToDefaultCamera()
    {
        // -----------------------------------------------------
        // DISABLE DESCRIPTION UI FIRST
        // -----------------------------------------------------

        if (descriptionUI != null)
        {
            descriptionUI.SetActive(false);
        }


        // -----------------------------------------------------
        // CLEAR CURRENT TEXT
        // -----------------------------------------------------

        if (titleText != null)
        {
            titleText.text = "";
        }

        if (descriptionText != null)
        {
            descriptionText.text = "";
        }


        // -----------------------------------------------------
        // RESET TARGET
        // -----------------------------------------------------

        currentTargetIndex = -1;


        // -----------------------------------------------------
        // RETURN CAMERA
        // -----------------------------------------------------

        if (
            defaultCameraPoint == null
        )
        {
            Debug.LogWarning(
                "[Identification] Default Camera Point is not assigned.",
                this
            );

            return;
        }

        StartCameraMovement(
            defaultCameraPoint,
            -1,
            false
        );
    }


    // =========================================================
    // CALCULATE POPUP POSITION
    // =========================================================

    private Vector3 CalculateTopPosition(
        Collider col
    )
    {
        Bounds bounds =
            col.bounds;

        return new Vector3(
            bounds.center.x,
            bounds.max.y +
            yOffsetDistance,
            bounds.center.z
        );
    }


    // =========================================================
    // SPAWN POPUP
    // =========================================================

    private void SpawnPopUpEffect(
        GameObject prefab,
        Vector3 worldPosition
    )
    {
        GameObject instance;

        if (
            worldSpaceCanvasParent != null
        )
        {
            instance =
                Instantiate(
                    prefab,
                    worldSpaceCanvasParent
                );
        }
        else
        {
            instance =
                Instantiate(prefab);
        }

        instance.transform.position =
            worldPosition;

        // Immediately face camera plane
        if (mainCamera != null)
        {
            instance.transform.rotation =
                mainCamera.transform.rotation;
        }

        activePopups.Add(instance.transform);

        StartCoroutine(
            PopUpAnimation(
                instance.transform
            )
        );
    }


    // =========================================================
    // POPUP ANIMATION
    // =========================================================

    private IEnumerator PopUpAnimation(
        Transform targetTransform
    )
    {
        if (targetTransform == null)
            yield break;

        float duration = 0.3f;
        float elapsed = 0f;

        Vector3 targetScale =
            Vector3.Scale(
                targetTransform.localScale,
                spawnScaleMultiplier
            );

        targetTransform.localScale =
            Vector3.zero;

        while (
            elapsed < duration
        )
        {
            if (
                targetTransform == null
            )
            {
                yield break;
            }

            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );

            float smoothValue =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            targetTransform.localScale =
                Vector3.Lerp(
                    Vector3.zero,
                    targetScale,
                    smoothValue
                );

            yield return null;
        }

        if (
            targetTransform != null
        )
        {
            targetTransform.localScale =
                targetScale;
        }
    }
}