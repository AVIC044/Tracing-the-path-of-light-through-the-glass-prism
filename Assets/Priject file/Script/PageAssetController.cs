using UnityEngine;
using System.Collections.Generic;

public class PageAssetController : MonoBehaviour
{
    [System.Serializable]
    public class PageAssets
    {
        public List<GameObject> assetsToEnable;
    }

    [Header("Slide Controller Reference")]
    [SerializeField] private SlideController slideController;

    [Header("Per Page Assets (Index = Page Index)")]
    [SerializeField] private List<PageAssets> pageAssets = new List<PageAssets>();

    private readonly List<GameObject> allTrackedAssets = new List<GameObject>();

    private int lastPage = -1;

    private void Awake()
    {
        CacheAllAssets();
    }

    private void Start()
    {
        UpdateAssets();
    }

    private void Update()
    {
        if (slideController == null)
            return;

        int currentPage = slideController.GetCurrentPage();

        if (currentPage != lastPage)
        {
            lastPage = currentPage;
            UpdateAssets();
        }
    }

    private void UpdateAssets()
    {
        int index = slideController.GetCurrentPage();

        if (index < 0 || index >= pageAssets.Count)
            return;

        DisableAll();

        var currentPage = pageAssets[index];

        foreach (var obj in currentPage.assetsToEnable)
        {
            if (obj != null)
                obj.SetActive(true);
        }
    }

    private void CacheAllAssets()
    {
        allTrackedAssets.Clear();

        foreach (var page in pageAssets)
        {
            if (page == null || page.assetsToEnable == null)
                continue;

            foreach (var obj in page.assetsToEnable)
            {
                if (obj != null && !allTrackedAssets.Contains(obj))
                    allTrackedAssets.Add(obj);
            }
        }
    }

    private void DisableAll()
    {
        foreach (var obj in allTrackedAssets)
        {
            if (obj != null)
                obj.SetActive(false);
        }
    }
}
