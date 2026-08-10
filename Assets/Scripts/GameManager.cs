using Script;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI")]
    public GameObject passPanel;
    public GameObject failPanel;

    [Header("Current Level Root")]
    public GameObject currentLevel;

    [Header("Level Settings")]
    public int currentLevelIndex = 1;

    [Header("Result Audio")]
    public AudioClip winClip;
    public AudioClip loseClip;

    [Header("Game Start")]
    public float startDelay = 2f;

    [Header("Win")]
    public float passDelay = 2f;

    [Header("Result Camera")]
    public Camera resultCamera;

    [Header("Reward UI")]
    public TMP_Text winRewardText;
    public TMP_Text loseRewardText;

    private bool gameEnded;
    private GameObject instructUI;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (passPanel != null)
            passPanel.SetActive(false);

        if (failPanel != null)
            failPanel.SetActive(false);
    }

    private void Start()
    {
        if (resultCamera != null)
            resultCamera.gameObject.SetActive(false);

        instructUI = GameObject.FindGameObjectWithTag("Instruct");

        if (passPanel == null)
            passPanel = GameObject.FindGameObjectWithTag("Pass");

        if (failPanel == null)
            failPanel = GameObject.FindGameObjectWithTag("Fail");

        if (currentLevel == null)
            currentLevel = GameObject.FindGameObjectWithTag("Level");

        if (passPanel != null)
            passPanel.SetActive(false);

        if (failPanel != null)
            failPanel.SetActive(false);

        Time.timeScale = 0f;
        AudioListener.pause = true;

        Pauser.LockPause();

        StartCoroutine(StartGameRoutine());
    }

    IEnumerator StartGameRoutine()
    {
        yield return new WaitForSecondsRealtime(startDelay);

        StartGame();
    }

    public void StartGame()
    {
        if (instructUI != null)
            instructUI.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        PlayerControllerRoblox player =
            FindObjectOfType<PlayerControllerRoblox>();

        if (player != null)
            player.canControl = true;

        Pauser.UnlockPause();
    }

    public void LevelPassed()
    {
        if (gameEnded)
            return;

        gameEnded = true;

        StartCoroutine(PassRoutine());
    }

    IEnumerator PassRoutine()
    {
        Pauser.LockPause();

        yield return new WaitForSeconds(passDelay);

        PlayerControllerRoblox player =
            FindObjectOfType<PlayerControllerRoblox>();

        if (player != null)
            player.canControl = false;

        int reward = 100;

        if (SceneManager.GetActiveScene().name == "Tutorial")
        {
            reward = 0;
        }
        else
        {
            CurrecnyManager.instance?.AddCurrency(reward);
            UnlockNextLevel();
        }

        UpdateRewardUI(reward);

        EnableResultCamera();

        StopAllGameAudio();
        PlayResultSound(winClip);

        if (passPanel != null)
            passPanel.SetActive(true);

        DisableLevel();

        Time.timeScale = 0f;
    }

    public void LevelFailed()
    {
        if (gameEnded)
            return;

        gameEnded = true;

        StartCoroutine(FailRoutine());
    }

    IEnumerator FailRoutine()
    {
        Pauser.LockPause();

        yield return new WaitForSeconds(1f);

        PlayerControllerRoblox player =
            FindObjectOfType<PlayerControllerRoblox>();

        if (player != null)
            player.canControl = false;

        UpdateRewardUI(0);

        EnableResultCamera();

        StopAllGameAudio();
        PlayResultSound(loseClip);

        if (failPanel != null)
            failPanel.SetActive(true);

        DisableLevel();

        Time.timeScale = 0f;
    }

    void UpdateRewardUI(int reward)
    {
        string text = reward.ToString();

        if (winRewardText != null)
            winRewardText.text = text;

        if (loseRewardText != null)
            loseRewardText.text = text;
    }

    void UnlockNextLevel()
    {
        if (SceneManager.GetActiveScene().name == "Tutorial")
            return;

        int unlocked =
            PlayerPrefs.GetInt(StringsData.playerLevel, 1);

        if (currentLevelIndex >= unlocked)
        {
            PlayerPrefs.SetInt(
                StringsData.playerLevel,
                currentLevelIndex + 1);

            PlayerPrefs.Save();
        }
    }

    void EnableResultCamera()
    {
        if (resultCamera != null)
            resultCamera.gameObject.SetActive(true);
    }

    void DisableLevel()
    {
        if (currentLevel != null)
            currentLevel.SetActive(false);
    }

    void StopAllGameAudio()
    {
        AudioSource[] sources =
            FindObjectsOfType<AudioSource>();

        foreach (AudioSource source in sources)
        {
            source.Stop();
        }
    }

    void PlayResultSound(AudioClip clip)
    {
        if (clip == null)
            return;

        GameObject obj = new GameObject("ResultAudio");

        AudioSource source =
            obj.AddComponent<AudioSource>();

        source.clip = clip;
        source.ignoreListenerPause = true;

        source.Play();

        Destroy(obj, clip.length);
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex);
    }

    public void GoHome()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(0);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}