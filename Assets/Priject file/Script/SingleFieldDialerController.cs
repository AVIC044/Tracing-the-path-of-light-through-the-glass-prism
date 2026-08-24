using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class SingleFieldDialerController : MonoBehaviour
{
    [System.Serializable]
    public class PageField
    {
        [Header("References")]
        public TMP_InputField inputField;
        public Image feedbackImage;

        [Header("Page Objects")]
        [Tooltip("Objects to turn ON when answered correctly, only visible on this page.")]
        public GameObject[] objectsToEnable;

        [Header("Settings")]
        public float correctAnswer;
        public int pageIndex;

        [HideInInspector]
        public bool solved;
    }

    [Header("Page Fields")]
    public PageField[] pageFields;

    [Header("Common Wrong Feedback")]
    public TMP_Text feedbackText;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctSound;
    public AudioClip wrongSound;

    [Header("Feedback Sprites")]
    public Sprite correctSprite;
    public Sprite wrongSprite;

    [Header("Buttons")]
    public Button validateButton;
    public Button autoFillButton;

    [Header("Settings")]
    public int maxWrongAttempts = 3;
    public float tolerance = 0.001f;

    [Header("Events")]
    public UnityEvent OnCorrectAnswer;
    public UnityEvent OnWrongAnswer;
    public UnityEvent OnAllAnswersVerified;

    private SlideController slideController;

    private int wrongAttempts;
    private bool solved;
    private bool isValidating;
    private int previousPageIndex = -1;

    private Dictionary<int, string> savedValues = new Dictionary<int, string>();
    private Dictionary<int, bool> savedImageStates = new Dictionary<int, bool>();

    // ============================================================
    // CURRENT FIELD
    // ============================================================

    private PageField CurrentField
    {
        get
        {
            foreach (PageField field in pageFields)
            {
                if (field.pageIndex == SlideController.CurrentIndex)
                    return field;
            }

            return null;
        }
    }

    private TMP_InputField ActiveField
    {
        get
        {
            return CurrentField != null ? CurrentField.inputField : null;
        }
    }

    private float ActiveAnswer
    {
        get
        {
            return CurrentField != null ? CurrentField.correctAnswer : 0f;
        }
    }

    private Image ActiveImage
    {
        get
        {
            return CurrentField != null ? CurrentField.feedbackImage : null;
        }
    }

    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        slideController = FindFirstObjectByType<SlideController>();

        if (validateButton != null)
        {
            validateButton.onClick.RemoveAllListeners();
            validateButton.onClick.AddListener(OnValidatePressed);
        }

        if (autoFillButton != null)
        {
            autoFillButton.onClick.RemoveAllListeners();
            autoFillButton.onClick.AddListener(AutoFill);
        }

        HideFeedback();
        ResetAll();
    }

    // ============================================================
    // COMMON FEEDBACK TEXT
    // ============================================================

    private void ShowFeedback()
    {
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(true);
    }

    private void HideFeedback()
    {
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);
    }

    // ============================================================
    // WRONG ICON
    // ============================================================

    IEnumerator ShowWrongIconRoutine()
    {
        isValidating = true;

        if (ActiveImage != null)
        {
            ActiveImage.sprite = wrongSprite;
            ActiveImage.gameObject.SetActive(true);
        }

        // Common feedback text stays visible
        ShowFeedback();

        yield return new WaitForSeconds(0.7f);

        if (ActiveImage != null)
            ActiveImage.gameObject.SetActive(false);

        if (ActiveField != null)
        {
            ActiveField.text = "";
            ActiveField.Select();
            ActiveField.ActivateInputField();
        }

        isValidating = false;
    }

    // ============================================================
    // DIGIT
    // ============================================================

    public void OnDigitPressed(string digit)
    {
        if (solved || isValidating || ActiveField == null)
            return;

        if (!ActiveField.interactable)
            return;

        HideFeedback();

        int maxLength = ActiveAnswer.ToString().Contains(".") ? 4 : 3;

        if (ActiveField.text.Length >= maxLength)
            return;

        ActiveField.text += digit;
    }

    // ============================================================
    // DECIMAL
    // ============================================================

    public void OnDecimalPressed()
    {
        if (solved || isValidating || ActiveField == null)
            return;

        if (!ActiveField.interactable)
            return;

        HideFeedback();

        int maxLength = ActiveAnswer.ToString().Contains(".") ? 4 : 3;

        if (ActiveField.text.Length >= maxLength)
            return;

        if (!ActiveField.text.Contains("."))
        {
            if (ActiveField.text == "")
                ActiveField.text = "0.";
            else
                ActiveField.text += ".";
        }
    }

    // ============================================================
    // BACKSPACE
    // ============================================================

    public void OnBackspacePressed()
    {
        if (solved || isValidating || ActiveField == null)
            return;

        if (!ActiveField.interactable)
            return;

        HideFeedback();

        if (ActiveField.text.Length > 0)
        {
            ActiveField.text = ActiveField.text.Substring(0, ActiveField.text.Length - 1);
        }
    }

    // ============================================================
    // VALIDATE
    // ============================================================

    public void OnValidatePressed()
    {
        if (solved || isValidating || ActiveField == null)
            return;

        if (string.IsNullOrEmpty(ActiveField.text))
            return;

        if (!float.TryParse(ActiveField.text, out float value))
            return;

        // WRONG ANSWER
        if (Mathf.Abs(value - ActiveAnswer) > tolerance)
        {
            wrongAttempts++;

            if (audioSource != null && wrongSound != null)
            {
                audioSource.PlayOneShot(wrongSound);
            }

            ShowFeedback();
            OnWrongAnswer?.Invoke();

            if (wrongAttempts >= maxWrongAttempts && autoFillButton != null)
            {
                autoFillButton.gameObject.SetActive(true);
            }

            StartCoroutine(ShowWrongIconRoutine());
            return;
        }

        // CORRECT ANSWER
        StartCoroutine(ValidateAndAdvanceRoutine());
    }

    private IEnumerator ValidateAndAdvanceRoutine()
    {
        isValidating = true;

        HideFeedback();

        if (ActiveImage != null)
        {
            ActiveImage.sprite = correctSprite;
            ActiveImage.gameObject.SetActive(true);
        }

        if (audioSource != null && correctSound != null)
        {
            audioSource.PlayOneShot(correctSound);
        }

        // Lock this field permanently and turn on target objects for this field
        if (CurrentField != null)
        {
            CurrentField.solved = true;

            if (CurrentField.inputField != null)
            {
                CurrentField.inputField.interactable = false;
            }

            EnableFieldObjects(CurrentField, true);
        }

        OnCorrectAnswer?.Invoke();

        wrongAttempts = 0;

        if (autoFillButton != null)
            autoFillButton.gameObject.SetActive(false);

        yield return null;

        // Mark current page completed on SlideController to unlock navigation buttons
        slideController?.MarkPageCompleted();

        isValidating = false;

        MoveToNextField();
    }

    public void AutoFill()
    {
        if (solved || isValidating || ActiveField == null)
            return;

        HideFeedback();
        ActiveField.text = ActiveAnswer.ToString();
        OnValidatePressed();
    }

    private void ActivateOnlyCurrentField()
    {
        // Disable all fields first
        foreach (PageField field in pageFields)
        {
            if (field.inputField != null)
                field.inputField.interactable = false;
        }

        // Enable only current page field
        if (CurrentField != null && CurrentField.inputField != null && !CurrentField.solved)
        {
            CurrentField.inputField.interactable = true;
            CurrentField.inputField.Select();
            CurrentField.inputField.ActivateInputField();
        }

        // Update page objects visibility state across all fields
        UpdatePageObjectsVisibility(SlideController.CurrentIndex);
    }

    private void OnEnable()
    {
        SlideController.OnSlideChanged += OnPageChanged;
        ActivateOnlyCurrentField();
    }

    private void OnDisable()
    {
        SlideController.OnSlideChanged -= OnPageChanged;
    }

    private void OnPageChanged(int pageIndex)
    {
        HideFeedback();

        if (previousPageIndex > pageIndex)
        {
            foreach (PageField field in pageFields)
            {
                if (field.pageIndex == previousPageIndex)
                {
                    if (field.inputField != null)
                    {
                        savedValues[field.pageIndex] = field.inputField.text;
                        field.inputField.text = "";
                    }

                    if (field.feedbackImage != null)
                    {
                        savedImageStates[field.pageIndex] = field.feedbackImage.gameObject.activeSelf;
                        field.feedbackImage.gameObject.SetActive(false);
                    }

                    field.solved = false;

                    // Turn off enabled objects if moving backward and resetting
                    EnableFieldObjects(field, false);
                }
            }
        }

        foreach (PageField field in pageFields)
        {
            if (field.pageIndex == pageIndex)
            {
                if (field.inputField != null && savedValues.ContainsKey(pageIndex))
                {
                    field.inputField.text = savedValues[pageIndex];
                }

                if (field.feedbackImage != null && savedImageStates.ContainsKey(pageIndex))
                {
                    field.feedbackImage.gameObject.SetActive(savedImageStates[pageIndex]);
                }
            }
        }

        previousPageIndex = pageIndex;
        ActivateOnlyCurrentField();
    }

    // ============================================================
    // OBJECT MANAGEMENT
    // ============================================================

    private void EnableFieldObjects(PageField field, bool enable)
    {
        if (field == null || field.objectsToEnable == null) return;

        foreach (GameObject obj in field.objectsToEnable)
        {
            if (obj != null)
            {
                obj.SetActive(enable);
            }
        }
    }

    private void UpdatePageObjectsVisibility(int currentPageIndex)
    {
        foreach (PageField field in pageFields)
        {
            if (field.objectsToEnable == null) continue;

            bool shouldBeActive = field.solved && (field.pageIndex == currentPageIndex);

            foreach (GameObject obj in field.objectsToEnable)
            {
                if (obj != null)
                {
                    obj.SetActive(shouldBeActive);
                }
            }
        }
    }

    private void FinishPuzzle()
    {
        solved = true;

        if (validateButton != null)
            validateButton.interactable = false;

        if (autoFillButton != null)
            autoFillButton.gameObject.SetActive(false);

        HideFeedback();

        slideController?.MarkPageCompleted();

        OnAllAnswersVerified?.Invoke();
    }

    public void ResetAll()
    {
        solved = false;
        isValidating = false;
        wrongAttempts = 0;

        if (validateButton != null)
            validateButton.interactable = true;

        if (autoFillButton != null)
            autoFillButton.gameObject.SetActive(false);

        HideFeedback();

        foreach (PageField field in pageFields)
        {
            field.solved = false;

            if (field.inputField != null)
            {
                field.inputField.text = "";
                field.inputField.interactable = false;
            }

            if (field.feedbackImage != null)
            {
                field.feedbackImage.gameObject.SetActive(false);
            }

            EnableFieldObjects(field, false);
        }

        ActivateOnlyCurrentField();
    }

    public void MoveToNextField()
    {
        bool allSolved = true;

        foreach (PageField field in pageFields)
        {
            if (!field.solved)
            {
                allSolved = false;
                break;
            }
        }

        if (allSolved)
        {
            FinishPuzzle();
        }
    }
}