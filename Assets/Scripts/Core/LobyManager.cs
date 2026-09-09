using UnityEngine;
using UnityEngine.SceneManagement;

public class LobyManager : MonoBehaviour
{
    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Stage1");
    }
}
