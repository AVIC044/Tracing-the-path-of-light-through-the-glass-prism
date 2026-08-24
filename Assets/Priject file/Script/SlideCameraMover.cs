using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class SlideCameraMover : MonoBehaviour
{
    [System.Serializable]
    public class PageCameraGroup
    {
        public List<Transform> cameraPoints = new List<Transform>();

        [Header("Camera Settings")]
        public float cameraMoveSpeed = 5f;

        [Header("Events")]
        public UnityEvent onCameraMoveStart;
        public UnityEvent onCameraMoveEnd;
    }

    [Header("Slide Controller Reference")]
    public SlideController slideController;

    [Header("Camera")]
    public Camera mainCamera;
    public float moveSpeed = 5f;

    [Header("Camera Points Per Page")]
    public List<PageCameraGroup> pageCameraGroups = new List<PageCameraGroup>();

    private Vector3 targetPosition;
    private Quaternion targetRotation;

    private Dictionary<int, Vector3> lockedPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Quaternion> lockedRotations = new Dictionary<int, Quaternion>();

    private int lastPage = -1;
    private bool isMoving = false;

    private Vector3 velocity = Vector3.zero;

    private float currentMoveSpeed;

    void Start()
    {
        if (slideController == null || mainCamera == null)
            return;

        lastPage = slideController.GetCurrentPage();
        MoveToPage(lastPage);
    }

    void Update()
    {
        if (slideController == null || mainCamera == null)
            return;

        int currentPage = slideController.GetCurrentPage();

        if (currentPage != lastPage)
        {
            MoveToPage(currentPage);
            lastPage = currentPage;
        }

        if (!isMoving)
            return;

        // Smooth Position (Fixed)
        mainCamera.transform.position = Vector3.SmoothDamp(
            mainCamera.transform.position,
            targetPosition,
            ref velocity,
            0.3f,
            Mathf.Infinity,
            Time.deltaTime);

        // Smooth Rotation
        mainCamera.transform.rotation = Quaternion.Slerp(
            mainCamera.transform.rotation,
            targetRotation,
            currentMoveSpeed * Time.deltaTime);

        // Stop when very close
        if (Vector3.Distance(mainCamera.transform.position, targetPosition) < 0.01f &&
            Quaternion.Angle(mainCamera.transform.rotation, targetRotation) < 0.5f)
        {
            mainCamera.transform.position = targetPosition;
            mainCamera.transform.rotation = targetRotation;
            isMoving = false;

            int pageIndex = slideController.GetCurrentPage();

            if (pageIndex < pageCameraGroups.Count)
                pageCameraGroups[pageIndex].onCameraMoveEnd?.Invoke();
        }
    }

    void MoveToPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= pageCameraGroups.Count)
            return;

        var group = pageCameraGroups[pageIndex];

        currentMoveSpeed = group.cameraMoveSpeed > 0 ? group.cameraMoveSpeed : moveSpeed;

        if (lockedPositions.ContainsKey(pageIndex))
        {
            targetPosition = lockedPositions[pageIndex];
            targetRotation = lockedRotations[pageIndex];
        }
        else
        {
            if (group.cameraPoints.Count == 0)
                return;

            targetPosition = group.cameraPoints[0].position;
            targetRotation = group.cameraPoints[0].rotation;
        }

        isMoving = true;

        group.onCameraMoveStart?.Invoke();
    }

    public void LockCurrentPagePosition()
    {
        if (slideController == null || mainCamera == null)
            return;

        int pageIndex = slideController.GetCurrentPage();

        if (!lockedPositions.ContainsKey(pageIndex))
        {
            lockedPositions[pageIndex] = mainCamera.transform.position;
            lockedRotations[pageIndex] = mainCamera.transform.rotation;
        }
    }
}
