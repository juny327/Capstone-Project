using UnityEngine;

public class GameOverUIController : MonoBehaviour
{
    [Header("Game Over UI")]
    [SerializeField] private GameObject gameOverUI;

    [Header("Hide When Game Over")]
    [SerializeField] private GameObject[] hideUIObjects;

    public void ShowGameOver()
    {
        foreach (GameObject ui in hideUIObjects)
        {
            if (ui == null || ui == gameOverUI)
                continue;

            // Never disable the game-over UI or one of its parents.
            if (gameOverUI != null && gameOverUI.transform.IsChildOf(ui.transform))
            {
                Debug.LogWarning("[GameOverUIController] Skipped a parent of GameOverUI: " + ui.name, ui);
                continue;
            }

            // Never disable the object that runs this component.
            if (transform.IsChildOf(ui.transform))
            {
                Debug.LogWarning("[GameOverUIController] Skipped a parent of the controller: " + ui.name, ui);
                continue;
            }

            ui.SetActive(false);
        }

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
            gameOverUI.transform.SetAsLastSibling();
        }
        else
        {
            Debug.LogWarning("[GameOverUIController] Game Over UI is not assigned.", this);
        }
    }
}
