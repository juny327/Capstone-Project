using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비의 주 무기 선택 표시 (13번 7-1).
///
/// 고르는 동작 자체는 버튼 OnClick 이 LobyManager.SelectWeapon(int) 을 직접 부른다.
/// 이 스크립트는 "지금 무엇이 골라져 있는지"를 보여주는 역할만 한다.
/// </summary>
public class LobyWeaponSelectUI : MonoBehaviour
{
    [SerializeField] private LobyManager manager;

    [Tooltip("LobyManager 의 selectableWeapons 와 같은 순서여야 한다")]
    [SerializeField] private Button[] buttons;

    [Tooltip("고른 무기의 이름과 설명을 보여준다")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [SerializeField] private Color selectedColor = new Color(1f, 0.82f, 0.30f, 1f);
    [SerializeField] private Color normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);

    void Awake()
    {
        if (manager == null)
            manager = FindFirstObjectByType<LobyManager>();
    }

    void OnEnable()
    {
        if (manager == null) return;

        // LobyManager.Start() 가 기본값(소총)을 고르며 이벤트를 쏜다.
        // OnEnable 은 Start 보다 먼저 돌므로 그 이벤트를 놓치지 않는다.
        manager.OnSelectionChanged += Refresh;

        Refresh(manager.SelectedIndex);
    }

    void OnDisable()
    {
        if (manager == null) return;

        manager.OnSelectionChanged -= Refresh;
    }

    void Refresh(int index)
    {
        if (buttons != null)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;

                Image image = buttons[i].GetComponent<Image>();

                if (image != null)
                    image.color = i == index ? selectedColor : normalColor;
            }
        }

        if (descriptionText == null) return;

        WeaponData weapon = manager.Selected;

        descriptionText.text = weapon != null
            ? $"{weapon.weaponName}\n{weapon.description}"
            : string.Empty;
    }
}
