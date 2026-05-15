using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseScript : MonoBehaviour
{
    public GameObject PauseMenu;
    public static bool pausecheck = false;

    public void Pause()
    {
        PauseMenu.SetActive(true);
        Time.timeScale = 0;
        pausecheck = true;
    }

    public void Resume()
    {
        PauseMenu.SetActive(false);
        Time.timeScale = 1;
        pausecheck = false;
    }

    public void Home()
    {
        SceneManager.LoadSceneAsync(0);
        Time.timeScale = 1;
        pausecheck = false;
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        Time.timeScale = 1;
        pausecheck = false;
    }
}
