using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼에 마우스를 올리면 '틱' · 슬라이더를 누르고 있는지 알려 준다 (<see cref="UISoundBinder"/> 가 붙인다).
/// 같은 오브젝트의 Selectable 과 함께 포인터 이벤트를 받는다.
/// </summary>
public class UISoundHover : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler
{
    public Selectable target;

    [Tooltip("올렸을 때 소리를 낼지 (슬라이더는 끈다)")]
    public bool hoverSound = true;

    /// <summary>마우스로 누르고 있는 중 — 슬라이더 틱은 이때만 낸다 (코드가 값을 맞출 때 조용히).</summary>
    public bool Pressed { get; private set; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hoverSound || target == null || !target.IsInteractable() || !target.isActiveAndEnabled) return;
        UISound.Play(UISoundKind.Hover);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) Pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) Pressed = false;
    }

    void OnDisable() => Pressed = false;

    /// <summary>누름 상태를 바꾼다 (테스트).</summary>
    public void SetPressedForTest(bool pressed) => Pressed = pressed;
}
