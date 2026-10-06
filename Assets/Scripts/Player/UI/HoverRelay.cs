using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>마우스가 올라가고 나갈 때를 알린다 — 정비 창 내 장비 판의 교체 미리보기에 쓴다.</summary>
public class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public event Action Entered;
    public event Action Exited;

    public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();

    public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();
}
