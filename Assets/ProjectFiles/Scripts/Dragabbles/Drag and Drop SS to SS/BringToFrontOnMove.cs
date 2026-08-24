using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class BringToFrontOnMove : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private RectTransform rectTransform;
    private Transform grandGrandParentTransform;

    private Transform originalParent;
    private int originalSiblingIndex;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (transform.parent != null &&
            transform.parent.parent != null &&
            transform.parent.parent.parent != null)
        {
            grandGrandParentTransform = transform.parent.parent.parent;
        }
        else
        {
            Debug.LogWarning(
                $"{nameof(BringToFrontOnMove)} requires a Grand Grand Parent to function.",
                this
            );
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        BringToFront();
    }

    public void OnDrag(PointerEventData eventData)
    {
        BringToFront();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (originalParent == null)
            return;

        transform.SetParent(originalParent, true);
        transform.SetSiblingIndex(originalSiblingIndex);
    }

    private void BringToFront()
    {
        if (grandGrandParentTransform == null)
            return;

        if (transform.parent != grandGrandParentTransform)
            transform.SetParent(grandGrandParentTransform, true);

        transform.SetAsLastSibling();
    }
}