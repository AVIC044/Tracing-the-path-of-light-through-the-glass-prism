using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public AudioSource audioSource; 

    public void ButtonClickAudioPlay(AudioClip buttonclicksound)
    {
        audioSource.PlayOneShot(buttonclicksound);
    }
}
