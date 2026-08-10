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

        UpdateSoundIcon();
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == "Tutorial")
        {
            PauseLocked = true;
        }

        inGameUI = GameObject.FindGameObjectWithTag("InGame");

        UpdateSoundIcon();
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

        if (inGameUI != null)
            inGameUI.SetActive(false);

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

        if (inGameUI != null)
            inGameUI.SetActive(true);

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

    void UpdateSoundIcon()
    {
        if (soundIcon != null)
        {
            soundIcon.sprite =
                AudioManagerPause.IsMuted
                    ? sound_off
                    : sound_on;
        }
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