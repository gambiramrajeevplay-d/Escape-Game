using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI")]
    public GameObject passPanel;
    public GameObject failPanel;

    [Header("Win")]
    [Tooltip("Delay before showing the pass panel.")]
    public float passDelay = 5f;

    private bool gameEnded;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (passPanel != null)
            passPanel.SetActive(false);

        if (failPanel != null)
            failPanel.SetActive(false);
    }

    public void LevelPassed()
    {
        if (gameEnded)
            return;

        gameEnded = true;
        StartCoroutine(PassRoutine());
    }

    public void LevelFailed()
    {
        if (gameEnded)
            return;

        gameEnded = true;

        if (failPanel != null)
            failPanel.SetActive(true);

        Time.timeScale = 0f;
    }

    IEnumerator PassRoutine()
    {
        yield return new WaitForSeconds(passDelay);

        if (passPanel != null)
            passPanel.SetActive(true);

        Time.timeScale = 0f;
    }

    // Restart the current scene
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}