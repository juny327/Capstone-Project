using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class EndingSequenceController : MonoBehaviour
{
    public PlayableDirector endingTimeline;
    public GameObject endingPanel;

    void Start()
    {
        endingTimeline.stopped += OnEndingFinished;
        endingTimeline.Play();
    }

    void OnEndingFinished(PlayableDirector dir)
    {
        endingPanel.SetActive(true);
    }

    public void OnLobbyClicked()
    {
        GameAppManager.Instance.ReturnToLobby();
    }
}