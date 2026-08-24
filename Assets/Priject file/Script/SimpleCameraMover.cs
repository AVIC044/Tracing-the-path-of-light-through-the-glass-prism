using UnityEngine;
using System.Collections;

public class SimpleCameraMover : MonoBehaviour
{
    [Header("Drag your Camera here!")]
    public Transform cameraToMove;

    public float speed = 3f;

    // Call this from your UnityEvent and assign the target Transform
    public void MoveSmoothly(Transform target)
    {
        if (cameraToMove == null)
        {
            Debug.LogError("SimpleCameraMover: Camera is not assigned!");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(MoveRoutine(target));
    }

    private IEnumerator MoveRoutine(Transform target)
    {
        while (Vector3.Distance(cameraToMove.position, target.position) > 0.01f)
        {
            cameraToMove.position = Vector3.Lerp(cameraToMove.position, target.position, Time.deltaTime * speed);
            cameraToMove.rotation = Quaternion.Slerp(cameraToMove.rotation, target.rotation, Time.deltaTime * speed);
            yield return null;
        }

        // Snap exactly to the final position at the end
        cameraToMove.position = target.position;
        cameraToMove.rotation = target.rotation;
    }
}