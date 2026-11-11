using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Generates a quiet looping fluorescent-hum ambience at runtime.
/// Starts on the first user gesture (browsers block autoplay).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class StoreAmbience : MonoBehaviour
{
    AudioSource source;
    bool started;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0.07f;
        source.clip = BuildHumClip();
    }

    void Update()
    {
        if (started) return;
        if (!AnyGesture()) return;

        started = true;
        source.Play();
    }

    static bool AnyGesture()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        return false;
#else
        return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0;
#endif
    }

    static AudioClip BuildHumClip()
    {
        const int sampleRate = 44100;
        const int seconds = 4;
        int length = sampleRate * seconds;
        var data = new float[length];
        var random = new System.Random(1337);

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / sampleRate;
            float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.35f;
            hum += Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.14f;
            hum += Mathf.Sin(2f * Mathf.PI * 239f * t) * 0.035f;
            float noise = ((float)random.NextDouble() * 2f - 1f) * 0.02f;
            data[i] = (hum + noise) * 0.5f;
        }

        int fade = sampleRate / 4;
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            data[i] *= k;
            data[length - 1 - i] *= k;
        }

        var clip = AudioClip.Create("StoreHum", length, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
