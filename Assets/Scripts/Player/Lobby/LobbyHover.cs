using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>마우스가 올라가고 내려갈 때 알린다 (로비 조합식 줄 → 가운데 모델이 상위 무기를 든다).</summary>
public class LobbyHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>true = 올라감, false = 내려감.</summary>
    public event Action<bool> Changed;

    public void OnPointerEnter(PointerEventData eventData) => Changed?.Invoke(true);

    public void OnPointerExit(PointerEventData eventData) => Changed?.Invoke(false);

    void OnDisable() => Changed?.Invoke(false);
}
