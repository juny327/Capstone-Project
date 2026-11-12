using UnityEngine;

/// <summary>이 버튼이 낼 UI 소리를 정한다 — 없으면 UISoundBinder 가 연결된 메서드 · 이름으로 고른다.</summary>
public class UISoundRole : MonoBehaviour
{
    public UISoundKind kind = UISoundKind.Click;
}
