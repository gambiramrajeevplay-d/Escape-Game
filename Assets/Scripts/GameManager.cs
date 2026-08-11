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
    [Tooltip("Shows combined 'coinsCollected+reward' text, e.g. '2+100'.")]
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

        // Make absolutely sure the result camera starts disabled.
        if (resultCamera != null)
        {
            resultCamera.enabled = false;
            resultCamera.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        // Find UI automatically if not assigned.
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

        // Make sure result camera is OFF at game start.
        DisableResultCamera();

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

    // =========================================================
    // PASS
    // =========================================================

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

        int coinsCollected = GetCoinsCollectedThisLevel();

        // Add collected coins to total currency.
        if (coinsCollected > 0)
            CurrecnyManager.instance?.AddCurrency(coinsCollected);

        UpdateRewardUI(reward, coinsCollected);

        // Stop gameplay first.
        StopAllGameAudio();

        // Disable the level before enabling the result camera.
        DisableLevel();

        // IMPORTANT:
        // Enable result camera AFTER disabling the level.
        EnableResultCamera();

        // Play result sound.
        PlayResultSound(winClip);

        if (passPanel != null)
            passPanel.SetActive(true);

        Time.timeScale = 0f;
    }

    // =========================================================
    // FAIL
    // =========================================================

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

        int coinsCollected = GetCoinsCollectedThisLevel();

        // Collected coins still count when player fails.
        if (coinsCollected > 0)
            CurrecnyManager.instance?.AddCurrency(coinsCollected);

        UpdateRewardUI(0, coinsCollected);

        // Stop gameplay audio.
        StopAllGameAudio();

        // Disable level BEFORE enabling result camera.
        DisableLevel();

        // IMPORTANT:
        // Enable result camera LAST.
        EnableResultCamera();

        // Play lose sound.
        PlayResultSound(loseClip);

        if (failPanel != null)
            failPanel.SetActive(true);

        Time.timeScale = 0f;
    }

    // =========================================================
    // COINS
    // =========================================================

    int GetCoinsCollectedThisLevel()
    {
        CoinCollector coinCollector =
            FindObjectOfType<CoinCollector>();

        return coinCollector != null
            ? coinCollector.GetLevelCoins()
            : 0;
    }

    // =========================================================
    // REWARD UI
    // =========================================================

    void UpdateRewardUI(int reward, int coinsCollected)
    {
        // Example:
        // 2 coins collected + 100 level reward
        // Displays: 2+100

        string combinedText =
            coinsCollected + "+" + reward;

        if (winRewardText != null)
            winRewardText.text = combinedText;

        if (loseRewardText != null)
            loseRewardText.text = combinedText;
    }

    // =========================================================
    // LEVEL UNLOCK
    // =========================================================

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
                currentLevelIndex + 1
            );

            PlayerPrefs.Save();
        }
    }

    // =========================================================
    // RESULT CAMERA
    // =========================================================

    void EnableResultCamera()
    {
        if (resultCamera == null)
        {
            Debug.LogWarning(
                "GameManager: Result Camera is not assigned!"
            );

            return;
        }

        Debug.Log("GameManager: Enabling Result Camera.");

        // Disable all other cameras first.
        Camera[] allCameras =
            FindObjectsOfType<Camera>();

        foreach (Camera cam in allCameras)
        {
            if (cam == null)
                continue;

            if (cam != resultCamera)
            {
                cam.enabled = false;
            }
        }

        // Enable the GameObject.
        resultCamera.gameObject.SetActive(true);

        // Enable the Camera component.
        resultCamera.enabled = true;

        Debug.Log(
            "Result Camera enabled: " +
            resultCamera.gameObject.activeInHierarchy +
            " | Camera enabled: " +
            resultCamera.enabled
        );
    }

    void DisableResultCamera()
    {
        if (resultCamera == null)
            return;

        resultCamera.enabled = false;
        resultCamera.gameObject.SetActive(false);
    }

    // =========================================================
    // LEVEL
    // =========================================================

    void DisableLevel()
    {
        if (currentLevel != null)
        {
            currentLevel.SetActive(false);

            Debug.Log(
                "GameManager: Current Level disabled."
            );
        }
    }

    // =========================================================
    // AUDIO
    // =========================================================

    void StopAllGameAudio()
    {
        AudioSource[] sources =
            FindObjectsOfType<AudioSource>();

        foreach (AudioSource source in sources)
        {
            if (source == null)
                continue;

            source.Stop();
        }
    }

    void PlayResultSound(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning(
                "GameManager: Result AudioClip is not assigned."
            );

            return;
        }

        GameObject obj =
            new GameObject("ResultAudio");

        AudioSource source =
            obj.AddComponent<AudioSource>();

        source.clip = clip;

        // Result sound must play even though
        // AudioListener.pause becomes true.
        source.ignoreListenerPause = true;

        source.playOnAwake = false;
        source.loop = false;
        source.volume = 1f;

        source.Play();

        Debug.Log(
            "GameManager: Playing result audio: " +
            clip.name
        );

        Destroy(obj, clip.length + 0.1f);
    }

    // =========================================================
    // RESTART
    // =========================================================

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    // =========================================================
    // HOME
    // =========================================================

    public void GoHome()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(0);
    }

    // =========================================================
    // DESTROY
    // =========================================================

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}