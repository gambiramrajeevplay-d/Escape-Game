using Script;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Pauser : MonoBehaviour
{
    public static Pauser instance;

    [Header("UI")]
    public GameObject PausePannel;
    public GameObject LevelObject;
    public GameObject PauseButton;

    [Header("Sound UI")]
    public Image soundIcon;
    public Sprite sound_on;
    public Sprite sound_off;

    [Header("Sound Toggle Slide")]
    [SerializeField] private RectTransform soundToggleRect;   // same object as soundIcon
    [SerializeField] private float onPosX = 40f;
    [SerializeField] private float offPosX = -40f;
    [SerializeField] private float slideDuration = 0.15f;

    private Coroutine slideRoutine;

    public static bool PauseLocked = false;

    private GameObject inGameUI;

    private void Awake()
    {
        instance = this;

        AudioManagerPause.Initialize();

        AudioListener.volume =
            AudioManagerPause.IsMuted ? 0f : 1f;
    }

    private void OnEnable()
    {
        if (PauseButton != null)
            PauseButton.SetActive(
                !AndroidTV.IsAndroidOrFireTv());

        UpdateSoundIcon(true); // snap on enable, no slide
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == "Tutorial")
        {
            PauseLocked = true;
        }

        LevelObject = GameObject.FindGameObjectWithTag("InGame");

        UpdateSoundIcon(true); // snap on start, no slide
    }

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

    public void Pause()
    {
        if (PauseLocked)
            return;

        PausePannel.SetActive(true);

        if (LevelObject != null)
            LevelObject.SetActive(false);

        if (PauseButton != null)
            PauseButton.SetActive(false);

        PlayerControllerRoblox player =
            FindObjectOfType<PlayerControllerRoblox>();

        if (player != null)
            player.canControl = false;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        UpdateSoundIcon();

        if (GameManager.Instance.resultCamera != null)
        {
            GameManager.Instance.resultCamera.gameObject.SetActive(true);
        }
    }

    public void Resume()
    {
        PausePannel.SetActive(false);

        if (LevelObject  != null)
            LevelObject.SetActive(true);

        if (PauseButton != null)
        {
            PauseButton.SetActive(
                !AndroidTV.IsAndroidOrFireTv());
        }

        PlayerControllerRoblox player =
            FindObjectOfType<PlayerControllerRoblox>();

        if (player != null)
            player.canControl = true;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (GameManager.Instance.resultCamera != null)
        {
            GameManager.Instance.resultCamera.gameObject.SetActive(false);
        }
    }

    public void MM()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        PauseLocked = false;

        SceneManager.LoadScene("UI");
    }

    public void ToggleSound()
    {
        AudioManagerPause.IsMuted =
            !AudioManagerPause.IsMuted;

        AudioListener.volume =
            AudioManagerPause.IsMuted ? 0f : 1f;

        UpdateSoundIcon();
    }

    void UpdateSoundIcon(bool instant = false)
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
            float targetX = AudioManagerPause.IsMuted ? offPosX : onPosX;

            if (slideRoutine != null)
                StopCoroutine(slideRoutine);

            if (!instant && gameObject.activeInHierarchy)
            {
                slideRoutine = StartCoroutine(SlideToggle(targetX));
            }
            else
            {
                Vector2 pos = soundToggleRect.anchoredPosition;
                soundToggleRect.anchoredPosition = new Vector2(targetX, pos.y);
            }
        }
    }

    private System.Collections.IEnumerator SlideToggle(float targetX)
    {
        Vector2 start = soundToggleRect.anchoredPosition;
        Vector2 end = new Vector2(targetX, start.y);

        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.unscaledDeltaTime; // unscaled - works even when paused (Time.timeScale = 0)
            soundToggleRect.anchoredPosition = Vector2.Lerp(start, end, t / slideDuration);
            yield return null;
        }

        soundToggleRect.anchoredPosition = end;
    }

    public static void LockPause()
    {
        PauseLocked = true;

        if (instance != null)
        {
            instance.PausePannel.SetActive(false);

            if (instance.LevelObject != null)
            {
                instance.LevelObject.SetActive(true);
            }
        }
    }

    public static void UnlockPause()
    {
        if (SceneManager.GetActiveScene().name == "Tutorial")
            return;

        PauseLocked = false;
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            if (!PauseLocked)
            {
                Pause();
            }
        }
    }
}