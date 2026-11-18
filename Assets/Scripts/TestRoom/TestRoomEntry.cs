using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Lobby button adapter. Keeps the shared LobyManager and GameAppManager unchanged.</summary>
public class TestRoomEntry : MonoBehaviour
{
    bool loading;

    public void Enter()
    {
        if (loading) return;
        loading = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(TestRoomManager.SceneName);
    }
}
