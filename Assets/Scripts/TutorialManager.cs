using UnityEngine;
using Script;
using System.Collections;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("Tutorial UI")]
    [Tooltip("Tutorial shown on mobile/tablet.")]
    public GameObject mobileUI;

    [Tooltip("Tutorial shown on Android TV / Fire TV.")]
    public GameObject tvUI;

    [Header("Tutorial Duration")]
    [Tooltip("How long the tutorial UI stays visible.")]
    public float tutorialDuration = 2f;

    [Header("Device Detection")]
    [Tooltip("Optional TouchControlsUI used to determine whether this is TV or mobile.")]
    public TouchControlsUI touchControlsUI;

    private void Awake()
    {
        Instance = this;

        // Automatically find TouchControlsUI if not assigned.
        if (touchControlsUI == null)
        {
            touchControlsUI =
                FindObjectOfType<TouchControlsUI>();
        }

        // Hide both tutorial UIs initially.
        if (mobileUI != null)
            mobileUI.SetActive(false);

        if (tvUI != null)
            tvUI.SetActive(false);
    }

    private void Start()
    {
        ShowTutorial();
    }

    // =========================================================
    // SHOW TUTORIAL
    // =========================================================

    public void ShowTutorial()
    {
        Pauser.LockPause();

        bool isTV = IsTV();

        if (isTV)
        {
            // TV / FIRE TV
            if (tvUI != null)
                tvUI.SetActive(true);

            if (mobileUI != null)
                mobileUI.SetActive(false);
        }
        else
        {
            // MOBILE / TABLET
            if (mobileUI != null)
                mobileUI.SetActive(true);

            if (tvUI != null)
                tvUI.SetActive(false);
        }

        // Start the automatic hide timer.
        StartCoroutine(HideTutorialAfterDelay());
    }

    // =========================================================
    // DEVICE CHECK
    // =========================================================

    private bool IsTV()
    {
        if (touchControlsUI != null)
        {
            return touchControlsUI.IsTV();
        }

        return AndroidTV.IsAndroidOrFireTv();
    }

    // =========================================================
    // AUTO HIDE
    // =========================================================

    private IEnumerator HideTutorialAfterDelay()
    {
        yield return new WaitForSecondsRealtime(tutorialDuration);

        HideTutorial();
    }

    // =========================================================
    // HIDE TUTORIAL
    // =========================================================

    public void HideTutorial()
    {
        if (mobileUI != null)
            mobileUI.SetActive(false);

        if (tvUI != null)
            tvUI.SetActive(false);

        Pauser.UnlockPause();
    }

    // =========================================================
    // CHECK
    // =========================================================

    public bool IsTutorialOpen()
    {
        return
            (mobileUI != null && mobileUI.activeSelf) ||
            (tvUI != null && tvUI.activeSelf);
    }
}