using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// 보스 인트로 연출. 타임라인이 끝나면 진행 버튼을 띄운다.
///
/// 버튼은 씬에서 꺼진 채로 시작하므로, 켜 주는 경로가 하나라도 동작해야 진행할 수 있다.
/// stopped 이벤트만 믿으면 타임라인이 끝나지 않을 때 영영 막히므로 보조 타이머와 스킵을 함께 둔다.
/// </summary>
public class SequenceController : MonoBehaviour
{
    public PlayableDirector introTimeline;
    public GameObject button;

    [Tooltip("타임라인이 끝나지 않아도 이 시간이 지나면 버튼을 띄운다(초). 0 이면 길이 + 1초")]
    [SerializeField] private float fallbackSeconds = 0f;

    bool shown;

    void Start()
    {
        // 업그레이드 창이 timeScale 0 으로 둔 채 씬을 넘어왔을 수 있다.
        // 타임라인이 GameTime 으로 돌기 때문에 0 이면 영원히 진행되지 않는다.
        Time.timeScale = 1f;

        if (button != null)
            button.SetActive(false);

        if (introTimeline == null)
        {
            ShowButton();
            return;
        }

        introTimeline.stopped += OnIntroFinished;
        introTimeline.Play();

        StartCoroutine(FallbackShow());
    }

    void OnDestroy()
    {
        if (introTimeline != null)
            introTimeline.stopped -= OnIntroFinished;
    }

    void Update()
    {
        if (shown) return;

        // 이 프로젝트는 신 Input System 전용이다 (activeInputHandler: 1).
        // UnityEngine.Input 을 쓰면 런타임에 예외가 난다.
        bool pressed =
            (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (pressed)
            ShowButton();
    }

    IEnumerator FallbackShow()
    {
        float wait = fallbackSeconds > 0f
            ? fallbackSeconds
            : (float)introTimeline.duration + 1f;

        // timeScale 에 영향받지 않아야 한다
        yield return new WaitForSecondsRealtime(wait);

        if (!shown)
            Debug.LogWarning("[SequenceController] 타임라인이 끝나지 않아 보조 타이머로 버튼을 띄웁니다.");

        ShowButton();
    }

    void OnIntroFinished(PlayableDirector dir)
    {
        ShowButton();
    }

    void ShowButton()
    {
        if (shown) return;

        shown = true;

        if (button == null)
        {
            Debug.LogError("[SequenceController] button 이 연결되지 않았습니다. 인트로에서 진행할 수 없습니다.", this);
            return;
        }

        button.SetActive(true);
    }

    /// <summary>진행 버튼의 OnClick 에 연결돼 있다.</summary>
    public void OnButtonClick()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("StageBoss");
    }
}
