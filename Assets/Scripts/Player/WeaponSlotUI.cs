using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보유 무기 슬롯 표시 (13번 5장 · 6장).
///
/// 손 무기는 스왑 대상이라 활성 칸을 강조하고, 서브유닛은 전부 항상 동작하므로 강조하지 않는다.
/// 아이콘이 없는 무기는 이름 텍스트로 대체한다 — 지금 WeaponData.icon 은 전부 비어 있다.
/// </summary>
public class WeaponSlotUI : MonoBehaviour
{
    public enum SlotKind
    {
        Held,
        SubUnit,
    }

    [Serializable]
    public class Slot
    {
        public GameObject root;

        [Tooltip("흰 테두리")]
        public Image frame;

        [Tooltip("테두리 안쪽 반투명 배경. 뒤의 맵이 비친다")]
        public Image background;

        [Tooltip("지금 들고 있는 무기를 가리키는 노란 테두리. 흰 테두리 바깥에 있다")]
        public GameObject activeMark;

        public Image icon;
        public TextMeshProUGUI label;
        public TextMeshProUGUI level;
    }

    [SerializeField] private SlotKind kind = SlotKind.Held;
    [SerializeField] private Slot[] slots;

    [Header("Colors")]
    [Tooltip("테두리 안쪽 배경. 반투명이라 뒤의 맵이 비친다")]
    [SerializeField] private Color slotBackground = new Color(0f, 0f, 0f, 0.28f);

    [Tooltip("무기가 있는 칸의 흰 테두리")]
    [SerializeField] private Color frameColor = new Color(1f, 1f, 1f, 0.9f);

    [Tooltip("빈 칸의 테두리. 흐리게 해서 채워진 칸과 구분한다")]
    [SerializeField] private Color emptyFrameColor = new Color(1f, 1f, 1f, 0.3f);

    WeaponController controller;

    void OnEnable()
    {
        GameEvents.OnPlayerSpawned += SetPlayer;
        GameEvents.OnWeaponsChanged += Refresh;
        GameEvents.OnWeaponSwapped += OnWeaponEvent;
        GameEvents.OnWeaponStatsChanged += OnWeaponEvent;

        Refresh();
    }

    void OnDisable()
    {
        GameEvents.OnPlayerSpawned -= SetPlayer;
        GameEvents.OnWeaponsChanged -= Refresh;
        GameEvents.OnWeaponSwapped -= OnWeaponEvent;
        GameEvents.OnWeaponStatsChanged -= OnWeaponEvent;
    }

    void SetPlayer(Transform player)
    {
        controller = player != null ? player.GetComponent<WeaponController>() : null;

        Refresh();
    }

    void OnWeaponEvent(IWeapon _) => Refresh();

    void Refresh()
    {
        if (slots == null) return;

        IReadOnlyList<IWeapon> weapons = null;
        int activeIndex = -1;
        int slotCount = 0;

        if (controller != null)
        {
            if (kind == SlotKind.Held)
            {
                weapons = controller.HeldWeapons;
                activeIndex = controller.ActiveIndex;
                slotCount = controller.MaxHeldSlots;
            }
            else
            {
                weapons = controller.SubUnits;
                slotCount = controller.MaxSubUnitSlots;
            }
        }

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];

            if (slot == null || slot.root == null) continue;

            // 슬롯 수보다 많이 만들어 뒀으면 남는 칸은 숨긴다
            bool inRange = i < slotCount;
            slot.root.SetActive(inRange);

            if (!inRange) continue;

            IWeapon weapon = weapons != null && i < weapons.Count ? weapons[i] : null;

            if (weapon == null || weapon.Data == null)
            {
                Fill(slot, null, string.Empty, string.Empty, false);
                continue;
            }

            // 노란 표시는 손에 든 무기에만 붙인다.
            // 서브유닛은 전부 항상 동작하므로 하나를 골라 가리킬 대상이 없다.
            bool isActive = kind == SlotKind.Held && i == activeIndex;

            // 아이콘이 있으면 모델 그림만 보여준다.
            // 아이콘이 없는 무기만 이름으로 대체하므로, 나중에 아이콘을 채워도 코드 수정이 필요 없다.
            Sprite sprite = weapon.Data.icon;

            Fill(slot,
                sprite,
                sprite != null ? string.Empty : weapon.Data.weaponName,
                weapon.Level > 1 ? $"Lv.{weapon.Level}" : string.Empty,
                isActive);
        }
    }

    void Fill(Slot slot, Sprite sprite, string label, string level, bool isActive)
    {
        bool occupied = sprite != null || !string.IsNullOrEmpty(label);

        if (slot.background != null)
        {
            slot.background.color = slotBackground;
            slot.background.enabled = slotBackground.a > 0.001f;
        }

        if (slot.frame != null)
            slot.frame.color = occupied ? frameColor : emptyFrameColor;

        // 노란 테두리는 지금 들고 있는 칸에만
        if (slot.activeMark != null)
            slot.activeMark.SetActive(isActive && occupied);

        if (slot.icon != null)
        {
            slot.icon.sprite = sprite;
            slot.icon.color = Color.white;
            slot.icon.enabled = sprite != null;   // 스프라이트가 없으면 흰 사각형이 보인다
        }

        if (slot.label != null)
            slot.label.text = label;

        if (slot.level != null)
            slot.level.text = level;
    }
}
