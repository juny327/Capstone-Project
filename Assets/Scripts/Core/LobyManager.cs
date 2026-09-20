using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 로비. 주 무기를 고르고 게임을 시작한다.
///
/// 고른 무기는 GameAppManager 에 담아 씬을 넘긴다. 플레이어는 Stage 씬이 로드된 뒤
/// 생성되므로, 로비에서 바로 장착할 수는 없다.
/// </summary>
public class LobyManager : MonoBehaviour
{
    [Header("주 무기 선택")]
    [Tooltip("로비에서 고를 수 있는 주 무기. 소총 · 기관단총 · 스나이퍼 · 검 순서로 넣을 것")]
    [SerializeField] private WeaponData[] selectableWeapons;

    /// <summary>현재 고른 무기.</summary>
    public WeaponData Selected { get; private set; }

    /// <summary>선택이 바뀌었을 때 (인덱스). 버튼 강조에 쓴다.</summary>
    public event Action<int> OnSelectionChanged;

    private int selectedIndex = -1;

    public int SelectedIndex => selectedIndex;

    void Start()
    {
        // 아무것도 고르지 않아도 바로 시작할 수 있게 첫 번째(소총)를 기본 선택해 둔다.
        // 처음 하는 사람이 시작 버튼이 안 눌려 멈추는 일을 막는다.
        if (selectableWeapons != null && selectableWeapons.Length > 0)
            SelectWeapon(0);
    }

    /// <summary>무기 버튼의 OnClick 에 연결한다. 인덱스는 selectableWeapons 순서.</summary>
    public void SelectWeapon(int index)
    {
        if (selectableWeapons == null) return;
        if (index < 0 || index >= selectableWeapons.Length) return;

        selectedIndex = index;
        Selected = selectableWeapons[index];

        OnSelectionChanged?.Invoke(index);
    }

    public void StartGame()
    {
        // 고른 무기를 씬 너머까지 들고 갈 수 있는 곳에 맡긴다
        if (GameAppManager.Instance != null)
            GameAppManager.Instance.SelectWeapon(Selected);

        Time.timeScale = 1f;
        SceneManager.LoadScene("Stage1");
    }
}
