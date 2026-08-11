using UnityEngine;

/// <summary>
/// TEMPORARY DEBUG TOOL. Attach this to any GameObject that's mysteriously
/// turning off, and check the Console when you hit Play — it logs the exact
/// frame/time it happened plus a stack trace pointing at whatever code
/// called SetActive(false) on it (or on one of its parents).
///
/// Remove this component once you've found the cause; it has no gameplay
/// purpose of its own.
/// </summary>
public class DisableWatcher : MonoBehaviour
{
    private void OnEnable()
    {
        Debug.Log($"[DisableWatcher] '{name}' was ENABLED at frame {Time.frameCount}, time {Time.time:F2}s.", this);
    }

    private void OnDisable()
    {
        Debug.LogWarning($"[DisableWatcher] '{name}' was DISABLED at frame {Time.frameCount}, time {Time.time:F2}s.\n" +
            $"{System.Environment.StackTrace}", this);
    }
}
