using System;
using UnityEngine;

public class AudioManagerPause : MonoBehaviour
{
    public static event Action<bool> OnMuteStateChanged;

    private static bool _isMuted = false;

    public static bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;

            AudioListener.volume = _isMuted ? 0f : 1f;

            PlayerPrefs.SetInt("AudioMuted", _isMuted ? 1 : 0);
            PlayerPrefs.Save();

            OnMuteStateChanged?.Invoke(_isMuted);
        }
    }

    public static void Initialize()
    {
        _isMuted = PlayerPrefs.GetInt("AudioMuted", 0) == 1;

        AudioListener.volume = _isMuted ? 0f : 1f;
    }
}