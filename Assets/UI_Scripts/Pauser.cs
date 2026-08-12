using Script;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class Pauser : MonoBehaviour
{
    public static Pauser instance;

    [Header("UI")]
    public GameObject PausePannel;
    public GameObject LevelObject;
    public GameObject PauseButton;

    [Header("Resume")]
    [Tooltip("Delay before the player can move after pressing Resume.")]
    public float resumeDelay = 0.5f;

    [Header("Sound UI")]
    public Image soundIcon;
    public Sprite sound_on;
    public Sprite sound_off;

    [Header("Sound Toggle Slide")]
    [SerializeField] private RectTransform soundToggleRect;
    [SerializeField] private float onPosX = 40f;
    [SerializeField] private float offPosX = -40f;
    [SerializeField] private float slideDuration = 0.15f;

    private Coroutine slideRoutine;
    private Coroutine resumeRoutine;

    public static bool PauseLocked = false;

    private Camera resultCamera;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        instance = this;

        AudioManagerPause.Initialize();

        AudioListener.volume =
            AudioManagerPause.IsMuted ? 0f : 1f;

        if (GameManager.Instance != null)
        {
            resultCamera = GameManager.Instance.resultCamera;
        }
    }

    // =========================================================
    // ENABLE
    // =========================================================

    private void OnEnable()
    {
        if (PauseButton != null)
        {
            PauseButton.SetActive(
                !AndroidTV.IsAndroidOrFireTv()
            );
        }

        UpdateSoundIcon(true);
    }

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == "Tutorial")
        {
            PauseLocked = true;
        }

        if (LevelObject == null)
        {
            LevelObject =
                GameObject.FindGameObjectWithTag("InGame");
        }

        if (resultCamera == null &&
            GameManager.Instance != null)
        {
            resultCamera =
                GameManager.Instance.resultCamera;
        }

        DisableResultCamera();

        UpdateSoundIcon(true);
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (PauseLocked)
            return;

        if (GameManager.Instance != null)
        {
            if ((GameManager.Instance.passPanel != null &&
                 GameManager.Instance.passPanel.activeSelf) ||

                (GameManager.Instance.failPanel != null &&
                 GameManager.Instance.failPanel.activeSelf))
            {
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Pause();
        }
    }

    // =========================================================
    // PAUSE
    // =========================================================

    public void Pause()
    {
        if (PauseLocked)
            return;

        Debug.Log("PAUSER: Pause pressed.");

        if (LevelObject == null)
        {
            LevelObject =
                GameObject.FindGameObjectWithTag("InGame");
        }

        if (resultCamera == null &&
            GameManager.Instance != null)
        {
            resultCamera =
                GameManager.Instance.resultCamera;
        }

        // -----------------------------------------------------
        // SHOW PAUSE PANEL
        // -----------------------------------------------------

        if (PausePannel != null)
            PausePannel.SetActive(true);

        if (PauseButton != null)
            PauseButton.SetActive(false);

        // -----------------------------------------------------
        // STOP PLAYER CONTROL
        // -----------------------------------------------------

        PlayerControllerRoblox player =
            FindObjectOfType<PlayerControllerRoblox>();

        if (player != null)
        {
            player.canControl = false;
        }

        // -----------------------------------------------------
        // PAUSE TIME / AUDIO
        // -----------------------------------------------------

        Time.timeScale = 0f;
        AudioListener.pause = true;

        UpdateSoundIcon();

        // -----------------------------------------------------
        // DISABLE LEVEL
        // -----------------------------------------------------

        if (LevelObject != null)
        {
            Debug.Log(
                "PAUSER: Disabling Level: " +
                LevelObject.name
            );

            LevelObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning(
                "PAUSER: LevelObject not found. " +
                "Make sure the Level Game Object has the 'InGame' tag."
            );
        }

        // -----------------------------------------------------
        // ENABLE RESULT CAMERA
        // -----------------------------------------------------

        EnableResultCamera();

        Debug.Log("PAUSER: Pause complete.");
    }

    // =========================================================
    // RESUME
    // =========================================================

    public void Resume()
    {
        if (resumeRoutine != null)
        {
            StopCoroutine(resumeRoutine);
        }

        resumeRoutine =
            StartCoroutine(ResumeRoutine());
    }

    // =========================================================
    // RESUME ROUTINE
    // =========================================================

    private IEnumerator ResumeRoutine()
    {
        Debug.Log("PAUSER: Resume pressed.");

        // -----------------------------------------------------
        // HIDE PAUSE PANEL
        // -----------------------------------------------------

        if (PausePannel != null)
            PausePannel.SetActive(false);

        // -----------------------------------------------------
        // ENABLE LEVEL
        // -----------------------------------------------------

        if (LevelObject != null)
        {
            LevelObject.SetActive(true);
        }

        // -----------------------------------------------------
        // GET PLAYER
        // -----------------------------------------------------

        PlayerControllerRoblox player =
            FindObjectOfType<PlayerControllerRoblox>();

        if (player != null)
        {
            // IMPORTANT:
            // Stop the old jump/movement from continuing.
            player.ResetMovementAfterPause();

            // Keep controls disabled during the delay.
            player.canControl = false;
        }

        // -----------------------------------------------------
        // DISABLE RESULT CAMERA
        // -----------------------------------------------------

        DisableResultCamera();

        // -----------------------------------------------------
        // KEEP TIME PAUSED DURING DELAY
        // -----------------------------------------------------

        Time.timeScale = 0f;

        // WaitForSeconds would NOT work here because
        // Time.timeScale is zero.
        yield return new WaitForSecondsRealtime(
            resumeDelay
        );

        // -----------------------------------------------------
        // RESUME GAME
        // -----------------------------------------------------

        Time.timeScale = 1f;
        AudioListener.pause = false;

        // Give control back AFTER the delay.
        if (player != null)
        {
            player.canControl = true;
        }

        if (PauseButton != null)
        {
            PauseButton.SetActive(
                !AndroidTV.IsAndroidOrFireTv()
            );
        }

        Debug.Log(
            "PAUSER: Resume complete after " +
            resumeDelay +
            " seconds."
        );

        resumeRoutine = null;
    }

    // =========================================================
    // RESULT CAMERA
    // =========================================================

    private void EnableResultCamera()
    {
        if (resultCamera == null)
        {
            Debug.LogWarning(
                "PAUSER: Result Camera is not assigned in GameManager."
            );

            return;
        }

        // Result Camera must NOT be a child of LevelObject.
        if (LevelObject != null &&
            resultCamera.transform.IsChildOf(
                LevelObject.transform))
        {
            Debug.LogWarning(
                "PAUSER: Result Camera is inside the LevelObject. " +
                "Move it outside the Level hierarchy."
            );

            return;
        }

        Camera[] allCameras =
            FindObjectsOfType<Camera>();

        foreach (Camera cam in allCameras)
        {
            if (cam == null)
                continue;

            if (cam != resultCamera)
                cam.enabled = false;
        }

        resultCamera.gameObject.SetActive(true);
        resultCamera.enabled = true;

        Debug.Log(
            "PAUSER: Result Camera enabled."
        );
    }

    // =========================================================
    // DISABLE RESULT CAMERA
    // =========================================================

    private void DisableResultCamera()
    {
        if (resultCamera == null)
            return;

        resultCamera.enabled = false;
        resultCamera.gameObject.SetActive(false);
    }

    // =========================================================
    // MAIN MENU
    // =========================================================

    public void MM()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        PauseLocked = false;

        SceneManager.LoadScene("UI");
    }

    // =========================================================
    // SOUND
    // =========================================================

    public void ToggleSound()
    {
        AudioManagerPause.IsMuted =
            !AudioManagerPause.IsMuted;

        AudioListener.volume =
            AudioManagerPause.IsMuted ? 0f : 1f;

        UpdateSoundIcon();
    }

    private void UpdateSoundIcon(bool instant = false)
    {
        if (soundIcon != null)
        {
            soundIcon.sprite =
                AudioManagerPause.IsMuted
                    ? sound_off
                    : sound_on;
        }

        if (soundToggleRect != null)
        {
            float targetX =
                AudioManagerPause.IsMuted
                    ? offPosX
                    : onPosX;

            if (slideRoutine != null)
                StopCoroutine(slideRoutine);

            if (!instant &&
                gameObject.activeInHierarchy)
            {
                slideRoutine =
                    StartCoroutine(
                        SlideToggle(targetX)
                    );
            }
            else
            {
                Vector2 pos =
                    soundToggleRect.anchoredPosition;

                soundToggleRect.anchoredPosition =
                    new Vector2(
                        targetX,
                        pos.y
                    );
            }
        }
    }

    private IEnumerator SlideToggle(float targetX)
    {
        Vector2 start =
            soundToggleRect.anchoredPosition;

        Vector2 end =
            new Vector2(
                targetX,
                start.y
            );

        float t = 0f;

        while (t < slideDuration)
        {
            t += Time.unscaledDeltaTime;

            soundToggleRect.anchoredPosition =
                Vector2.Lerp(
                    start,
                    end,
                    t / slideDuration
                );

            yield return null;
        }

        soundToggleRect.anchoredPosition = end;
    }

    // =========================================================
    // PAUSE LOCK
    // =========================================================

    public static void LockPause()
    {
        PauseLocked = true;

        if (instance != null)
        {
            if (instance.PausePannel != null)
                instance.PausePannel.SetActive(false);

            if (instance.LevelObject != null)
                instance.LevelObject.SetActive(true);

            instance.DisableResultCamera();
        }
    }

    public static void UnlockPause()
    {
        if (SceneManager.GetActiveScene().name == "Tutorial")
            return;

        PauseLocked = false;
    }

    // =========================================================
    // APPLICATION FOCUS
    // =========================================================

    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            if (!PauseLocked)
                Pause();
        }
    }
}