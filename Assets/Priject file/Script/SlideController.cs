using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class SlideController : MonoBehaviour
{
    [System.Serializable]
    public class PageData
    {
        [Header("Events")]
        public UnityEvent onNextClick;
        public UnityEvent onNextCompleted;
        public UnityEvent onBackClick;

        [Header("Page Button Controls")]
        public bool unlockTwoButtons; // If checked, Next and Back/Previous are immediately interactable on this page
        public bool lockTwoButtons;
    }

    // --- Added for PersistentAssetController ---
    public static int CurrentIndex;
    public static event System.Action<int> OnSlideChanged;
    // -------------------------------------------

    [Header("Total Pages")]
    public int totalPages = 1;

    [Header("Buttons")]
    public Button nextButton;
    public Button backButton;
    public Button secondButton;

    [Header("Initial State Overrides")]
    public bool nextButtonAlwaysInteractable = false;
    public bool backButtonAlwaysInteractable = false;
    public bool secondButtonAlwaysInteractable = false;

    [Header("Page Number UI")]
    public TMP_Text pageNumberText;

    [Header("Page Settings")]
    public List<PageData> pages = new List<PageData>();

    [Header("Testing Cheat")]
    public bool cheatButtons = false; // 🔥 Cheat checkbox

    protected int currentPage = 0;
    protected HashSet<int> completedPages = new HashSet<int>();

    private HashSet<int> nextClickTriggeredPages = new HashSet<int>();
    public static SlideController Instance { get; private set; }

    protected virtual void Start()
    {
        UpdatePage();
        UpdateButtonStates();
    }

    public int GetCurrentPage()
    {
        return currentPage;
    }

    public void NextPage()
    {
        if (currentPage >= totalPages - 1)
            return;

        bool isPageUnlocked = currentPage < pages.Count && pages[currentPage].unlockTwoButtons;

        if (!nextButtonAlwaysInteractable && !cheatButtons && !isPageUnlocked && !completedPages.Contains(currentPage))
            return;

        if (currentPage < pages.Count && !nextClickTriggeredPages.Contains(currentPage))
        {
            pages[currentPage]?.onNextClick?.Invoke();
            nextClickTriggeredPages.Add(currentPage);
        }

        int previous = currentPage;
        currentPage++;

        UpdatePage();
        UpdateButtonStates();

        if (previous < pages.Count)
        {
            pages[previous]?.onNextCompleted?.Invoke();
        }
    }

    public void BackPage()
    {
        if (currentPage <= 0)
            return;

        if (currentPage < pages.Count)
        {
            pages[currentPage]?.onBackClick?.Invoke();
        }

        currentPage--;

        UpdatePage();
        UpdateButtonStates();
    }
    public void Awake()
    {
        Instance = this;
    }
    public void MarkPageCompleted()
    {
        if (!completedPages.Contains(currentPage))
            completedPages.Add(currentPage);

        UpdateButtonStates();
    }

    protected virtual void UpdatePage()
    {
        // --- Added for PersistentAssetController ---
        CurrentIndex = currentPage;
        OnSlideChanged?.Invoke(currentPage);
        // -------------------------------------------

        UpdatePageNumber();
    }

    void UpdateButtonStates()
    {
        bool isCompleted = completedPages.Contains(currentPage);
        bool isTwoButtonsUnlocked = currentPage < pages.Count && pages[currentPage].unlockTwoButtons;

        // Next Button State
        if (nextButton != null)
        {
            if (currentPage >= totalPages - 1)
            {
                nextButton.interactable = false;
            }
            else
            {
                nextButton.interactable = nextButtonAlwaysInteractable || cheatButtons || isTwoButtonsUnlocked || isCompleted;
            }
        }

        // Back/Previous Button State
        if (backButton != null)
        {
            if (currentPage <= 0)
            {
                backButton.interactable = false;
            }
            else
            {
                backButton.interactable = backButtonAlwaysInteractable || cheatButtons || isTwoButtonsUnlocked || true;
            }
        }

        // Second Button State
        if (secondButton != null)
        {
            if (secondButtonAlwaysInteractable || cheatButtons)
            {
                secondButton.interactable = true;
            }
            else if (currentPage < pages.Count && pages[currentPage].lockTwoButtons)
            {
                secondButton.interactable = isCompleted;
            }
            else
            {
                secondButton.interactable = true;
            }
        }
    }

    void UpdatePageNumber()
    {
        if (pageNumberText == null) return;

        pageNumberText.text =
            (currentPage + 1).ToString("D2") + " / " +
            totalPages.ToString("D2");
    }
}