using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class DialogUISet
{
    [Tooltip("Descriptor name for organizational purposes in the inspector.")]
    public string setName = "Dialog Set";

    [Tooltip("The parent GameObject panel for this dialog style.")]
    public GameObject dialogPanel;

    [Tooltip("Image component used by this dialog style.")]
    public Image dialogImage;

    [Tooltip("Text component used by this dialog style.")]
    public TextMeshProUGUI dialogText;

    [Tooltip("Optional override sprite for this dialog set. If left empty, no sprite assignment occurs.")]
    public Sprite dialogSprite;

    [Tooltip("Specify target Page Numbers (1-based: Page 1, Page 2, Page 3, etc.) where this dialog UI set should appear.")]
    public List<int> targetPages = new List<int>();
}

[System.Serializable]
public class AlternatePanelData
{
    public GameObject panel;

    [Tooltip("If enabled, this panel will remain active in upcoming pages")]
    public bool stayInUpcomingPages;

    [Header("Enable Once Feature")]
    [Tooltip("If enabled, panel will activate only once and never again on revisit")]
    public bool enableOnce;

    [HideInInspector] public bool hasBeenEnabledOnce;
}

[System.Serializable]
public class PageData
{
    [Header("Page Name / Page No")]
    public string pageName;

    [TextArea]
    public string pageText;

    [Header("Display Options")]
    public bool showDialogBox = true;
    public bool showAlternatePanels;

    [Header("Alternate Panels For This Page")]
    public List<AlternatePanelData> alternatePanels;
}

public class UIPromptController : MonoBehaviour
{
    [Header("Pages")]
    [SerializeField] private PageData[] pages;

    [Header("Dialog UI Sets")]
    [Tooltip("Configure different Dialog UI panels and assign target page numbers (1-based) to each set.")]
    [SerializeField] private List<DialogUISet> dialogUISets = new List<DialogUISet>();

    private int currentPageIndex = -1;

    private void OnEnable()
    {
        SlideController.OnSlideChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        SlideController.OnSlideChanged -= HandlePageChanged;
    }

    private void Start()
    {
        HandlePageChanged(SlideController.CurrentIndex);
    }

    private void HandlePageChanged(int index)
    {
        if (index < 0 || index >= pages.Length)
            return;

        currentPageIndex = index;
        ShowPage(index);
    }

    private void ShowPage(int index)
    {
        PageData page = pages[index];

        // Convert 0-based array index into 1-based Page Number
        int pageNumber = index + 1;

        // Reset all dialog panels first
        HideAllDialogSets();
        ResetAllPanels();

        // Handle Dialog UI swapping logic based on Page Number
        if (page.showDialogBox)
        {
            DialogUISet activeSet = GetDialogSetForPageNumber(pageNumber);

            if (activeSet != null && activeSet.dialogPanel != null)
            {
                activeSet.dialogPanel.SetActive(true);

                if (activeSet.dialogText != null)
                    activeSet.dialogText.text = page.pageText;

                if (activeSet.dialogImage != null && activeSet.dialogSprite != null)
                    activeSet.dialogImage.sprite = activeSet.dialogSprite;
            }
        }

        // Apply alternate panel visibility logic
        ApplyPanelVisibility(index);
    }

    private DialogUISet GetDialogSetForPageNumber(int pageNumber)
    {
        foreach (var set in dialogUISets)
        {
            if (set != null && set.targetPages.Contains(pageNumber))
            {
                return set;
            }
        }
        return null;
    }

    private void HideAllDialogSets()
    {
        foreach (var set in dialogUISets)
        {
            if (set != null && set.dialogPanel != null)
            {
                set.dialogPanel.SetActive(false);
            }
        }
    }

    private void ResetAllPanels()
    {
        foreach (var p in pages)
        {
            if (p.alternatePanels == null)
                continue;

            foreach (var panelData in p.alternatePanels)
            {
                if (panelData != null && panelData.panel != null)
                    panelData.panel.SetActive(false);
            }
        }
    }

    private void ApplyPanelVisibility(int currentIndex)
    {
        for (int i = 0; i <= currentIndex; i++)
        {
            PageData page = pages[i];

            if (!page.showAlternatePanels || page.alternatePanels == null)
                continue;

            foreach (var panelData in page.alternatePanels)
            {
                if (panelData == null || panelData.panel == null)
                    continue;

                if (panelData.enableOnce && panelData.hasBeenEnabledOnce)
                    continue;

                if (i == currentIndex)
                {
                    panelData.panel.SetActive(true);

                    if (panelData.enableOnce)
                        panelData.hasBeenEnabledOnce = true;
                }
                else if (panelData.stayInUpcomingPages)
                {
                    if (!panelData.enableOnce || !panelData.hasBeenEnabledOnce)
                    {
                        panelData.panel.SetActive(true);
                    }
                }
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(UIPromptController))]
[CanEditMultipleObjects]
public class UIPromptControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        if (GUILayout.Button("Name Pages"))
        {
            foreach (var t in targets)
            {
                UIPromptController controller = (UIPromptController)t;
                NamePages(controller);
            }
        }
    }

    private void NamePages(UIPromptController controller)
    {
        SerializedObject so = new SerializedObject(controller);
        SerializedProperty pagesProp = so.FindProperty("pages");

        if (pagesProp == null || pagesProp.arraySize == 0)
        {
            Debug.LogWarning("No pages found to rename.");
            return;
        }

        for (int i = 0; i < pagesProp.arraySize; i++)
        {
            SerializedProperty page = pagesProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = page.FindPropertyRelative("pageName");

            if (nameProp != null)
            {
                nameProp.stringValue = $"Page {i + 1}";
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
    }
}
#endif