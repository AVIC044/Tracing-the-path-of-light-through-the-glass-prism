using UnityEngine;

public class AllTextDropComplete : MonoBehaviour
{
    [Header("5 ScreenToWorldSnapManager Options")]
    public ScreenToWorldSnapManager option1;
    public ScreenToWorldSnapManager option2;
    public ScreenToWorldSnapManager option3;
    public ScreenToWorldSnapManager option4;
    public ScreenToWorldSnapManager option5;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip correctDropSound;

    [Header("Image To Show After All 5 Drops")]
    public GameObject imageToShow;

    [Header("Page Navigation")]
    public SlideController pageNavigation;

    private bool completed = false;

    // We use these arrays to cleanly track each option's state
    private ScreenToWorldSnapManager[] optionsArray;
    private bool[] alreadySnapped;

    private void Start()
    {
        if (imageToShow != null)
        {
            imageToShow.SetActive(false);
        }

        // Bundle the options into an array so we can easily loop through them
        optionsArray = new ScreenToWorldSnapManager[] { option1, option2, option3, option4, option5 };

        // This array keeps track of which ones have already triggered the sound
        alreadySnapped = new bool[5];
    }

    private void Update()
    {
        if (completed)
            return;

        // Check for missing references
        if (option1 == null || option2 == null || option3 == null || option4 == null || option5 == null)
        {
            return;
        }

        bool allDropped = true;

        // =====================================================
        // Check ALL 5 drops and play sound if newly snapped
        // =====================================================
        for (int i = 0; i < optionsArray.Length; i++)
        {
            bool isCurrentlySnapped = optionsArray[i].IsAllSnapped();

            // If it is snapped now, but wasn't snapped in the previous frame
            if (isCurrentlySnapped && !alreadySnapped[i])
            {
                alreadySnapped[i] = true; // Mark as snapped
                PlayDropSound();          // Play the sound
            }

            // If even one is not snapped, they aren't all dropped yet
            if (!isCurrentlySnapped)
            {
                allDropped = false;
            }
        }

        // =====================================================
        // All 5 correctly dropped
        // =====================================================
        if (allDropped)
        {
            CompletePage();
        }
    }

    private void PlayDropSound()
    {
        if (audioSource != null && correctDropSound != null)
        {
            // PlayOneShot allows multiple overlapping sounds without cutting each other off
            audioSource.PlayOneShot(correctDropSound);
        }
    }

    // =========================================================
    // COMPLETE
    // =========================================================
    private void CompletePage()
    {
        if (completed)
            return;

        completed = true;

        // Show WHY Image
        if (imageToShow != null)
        {
            imageToShow.SetActive(true);
            Debug.Log("WHY IMAGE SHOWN");
        }

        // Page Navigation
        SlideController navigation = pageNavigation;

        if (navigation == null)
        {
            navigation = SlideController.Instance;
        }

        if (navigation != null)
        {
            pageNavigation.MarkPageCompleted();

            Debug.Log(
                "ALL 5 OPTIONS DROPPED CORRECTLY -> " +
                "WHY IMAGE SHOWN -> " +
                "NEXT PAGE UNLOCKED"
            );
        }
        else
        {
            Debug.LogError("PageNavigationController not found!");
        }
    }

    // =========================================================
    // RESET
    // =========================================================
    public void ResetCompletion()
    {
        completed = false;

        // Reset the audio tracking states so sounds can play again on replay
        if (alreadySnapped != null)
        {
            for (int i = 0; i < alreadySnapped.Length; i++)
            {
                alreadySnapped[i] = false;
            }
        }

        if (imageToShow != null)
        {
            imageToShow.SetActive(false);
        }

        Debug.Log("AllTextDropComplete Reset");
    }
}