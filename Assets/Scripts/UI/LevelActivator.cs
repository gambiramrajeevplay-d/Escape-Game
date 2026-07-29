using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelActivator : MonoBehaviour
{
    public GameObject[] levels;

    private void Awake()
    {
        int levelToLoad = PlayerPrefs.GetInt(StringsData.levelToLoad, 0);

        if (levelToLoad < 0 || levelToLoad >= levels.Length)
        {
            Debug.LogWarning($"levelToLoad ({levelToLoad}) is out of range. Defaulting to 0.");
            levelToLoad = 0;
        }

        foreach (var level in levels)
        {
            level.SetActive(false);
        }

        levels[levelToLoad].SetActive(true);
    }
}