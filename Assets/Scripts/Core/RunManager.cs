using UnityEngine;
using UnityEngine.SceneManagement;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance;

    public GameObject playerPrefab;
    GameObject currentPlayer;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public GameObject GetPlayer()
    {
        return currentPlayer;
    }

    public void SpawnPlayer(Vector3 pos)
    {
        if (currentPlayer == null)
        {
            currentPlayer = Instantiate(playerPrefab, pos, Quaternion.identity);
            DontDestroyOnLoad(currentPlayer);
        }
    }

    public void DestroyPlayer()
    {
        if (currentPlayer != null)
        {
            Destroy(currentPlayer);
            currentPlayer = null;
        }
    }

    public void OnClick()
    {
        SceneManager.LoadScene("Stage1");
    }
}