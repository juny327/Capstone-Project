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
///
/// 보조 무기 방식 (사수 · 검사, 2026-11-11) — 주무기와 보조 무기를 한눈에 갈라 보인다.
///   · 첫 칸 = 주무기 (손에 든 무기) — 노란 테두리 · 위에 "주무기"
///   · 나머지 = 보조 칸 — 조금 작게 · 틈을 두고 · 하늘색 테두리 · 위에 "보조" (드론 무기 · 드론은 "드론")
///   · 칸 수 = 1 + 캐릭터의 보조 칸 수 (사수 3 · 검사 2). 드론(서브유닛)도 보조 칸에 함께 보인다
/// 칸 배치는 런타임에만 바꾼다 — 장면(스테이지 · 테스트 룸)의 칸 오브젝트는 그대로다.
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

    // ───────── 보조 무기 방식 표시 ─────────

    [Header("Support Weapons (보조 무기)")]
    [Tooltip("주무기 칸 위 글자 · 색")]
    [SerializeField] private string mainTag = "주무기";
    [SerializeField] private Color mainTagColor = new Color(1f, 0.82f, 0.15f, 1f);

    [Tooltip("보조 칸 위 글자 · 드론 무기 글자 · 색")]
    [SerializeField] private string supportTag = "보조";
    [SerializeField] private string droneTag = "드론";
    [SerializeField] private Color supportColor = new Color(0.45f, 0.85f, 1f, 1f);

    [Tooltip("보조 칸 크기 (주무기 칸 대비) · 주무기 칸과의 틈 (px)")]
    [Range(0.5f, 1f)] [SerializeField] private float supportScale = 0.8f;
    [SerializeField] private float supportGap = 18f;

    private readonly List<IWeapon> ordered = new List<IWeapon>();
    private TextMeshProUGUI[] roleTags;
    private Vector2[] basePositions;
    private Vector3[] baseScales;
    private bool supportLayout;

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

        bool support = kind == SlotKind.Held && controller != null && controller.UsesSupportWeapons;

        if (controller != null)
        {
            if (support)
            {
                // 첫 칸 = 주무기, 그 뒤 = 보조 (떠 있는 무기 → 드론 순)
                ordered.Clear();
                IWeapon main = controller.MainWeapon;
                if (main != null) ordered.Add(main);
                foreach (IWeapon w in controller.HeldWeapons) if (w != null && w != main) ordered.Add(w);
                foreach (IWeapon w in controller.SubUnits) if (w != null) ordered.Add(w);

                weapons = ordered;
                activeIndex = main != null ? 0 : -1;
                slotCount = Mathf.Min(slots.Length, 1 + controller.SupportSlots);
            }
            else if (kind == SlotKind.Held)
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

        ApplyLayout(support);

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
                StyleRole(i, slot, null, support);
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
                LevelLabel(weapon),
                isActive);
            StyleRole(i, slot, weapon, support);
        }
    }

    /// <summary>
    /// 칸 배치 — 보조 무기 방식이면 첫 칸(주무기)은 그대로, 나머지는 작게 · 틈을 두고 바닥을 맞춰 늘어놓는다.
    /// 처음 본 자리 · 크기를 기억해 두었다가 예전 방식이면 되돌린다.
    /// </summary>
    void ApplyLayout(bool support)
    {
        if (slots == null || slots.Length == 0) return;

        if (basePositions == null)
        {
            basePositions = new Vector2[slots.Length];
            baseScales = new Vector3[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                RectTransform rt = slots[i] != null && slots[i].root != null ? slots[i].root.transform as RectTransform : null;
                basePositions[i] = rt != null ? rt.anchoredPosition : Vector2.zero;
                baseScales[i] = rt != null ? rt.localScale : Vector3.one;
            }
        }

        if (support == supportLayout && support) return;
        supportLayout = support;

        RectTransform first = slots[0] != null && slots[0].root != null ? slots[0].root.transform as RectTransform : null;
        float size = first != null ? first.rect.width : 80f;
        float smallSize = size * supportScale;
        float smallGap = 6f;

        for (int i = 0; i < slots.Length; i++)
        {
            RectTransform rt = slots[i] != null && slots[i].root != null ? slots[i].root.transform as RectTransform : null;
            if (rt == null) continue;

            if (!support || i == 0)
            {
                rt.anchoredPosition = basePositions[i];
                rt.localScale = baseScales[i];
                continue;
            }

            float x = basePositions[0].x + size * 0.5f + supportGap + smallSize * 0.5f + (i - 1) * (smallSize + smallGap);
            float y = basePositions[0].y - (size - smallSize) * 0.5f;   // 바닥을 맞춘다
            rt.anchoredPosition = new Vector2(x, y);
            rt.localScale = baseScales[i] * supportScale;
        }
    }

    /// <summary>칸 위 글자 · 테두리 색 — 주무기 / 보조 / 드론. 예전 방식이면 글자를 숨긴다.</summary>
    void StyleRole(int index, Slot slot, IWeapon weapon, bool support)
    {
        TextMeshProUGUI tag = RoleTag(index, slot);
        if (tag == null) return;

        if (!support)
        {
            tag.gameObject.SetActive(false);
            return;
        }

        tag.gameObject.SetActive(true);

        if (index == 0)
        {
            tag.text = mainTag;
            tag.color = mainTagColor;
            tag.fontSize = 14f;
            return;
        }

        bool occupied = weapon != null && weapon.Data != null;
        bool drone = occupied && (weapon.Data.SupportOnly || weapon.Data.IsAlwaysActive);

        tag.text = drone ? droneTag : supportTag;
        tag.color = new Color(supportColor.r, supportColor.g, supportColor.b, occupied ? 1f : 0.4f);
        tag.fontSize = 16f;   // 칸이 작아진 만큼 글자를 키워 주무기 글자와 비슷하게 보이게

        if (slot.frame != null)
            slot.frame.color = new Color(supportColor.r, supportColor.g, supportColor.b, occupied ? 0.95f : 0.35f);
    }

    /// <summary>칸 위 글자 — 장면에 없으므로 처음 쓸 때 만든다 (글꼴은 그 칸의 이름 글자 것).</summary>
    TextMeshProUGUI RoleTag(int index, Slot slot)
    {
        if (slot == null || slot.root == null) return null;
        if (roleTags == null || roleTags.Length != slots.Length) roleTags = new TextMeshProUGUI[slots.Length];
        if (roleTags[index] != null) return roleTags[index];

        var go = new GameObject("RoleTag", typeof(RectTransform));
        go.transform.SetParent(slot.root.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 3f);
        rt.sizeDelta = new Vector2(120f, 22f);

        var text = go.AddComponent<TextMeshProUGUI>();
        TextMeshProUGUI source = slot.label != null ? slot.label : slot.level;
        if (source != null)
        {
            text.font = source.font;
            text.fontSharedMaterial = source.fontSharedMaterial;
        }
        text.alignment = TextAlignmentOptions.Bottom;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        roleTags[index] = text;
        return text;
    }

    /// <summary>
    /// 칸 아래 글자 — 상위 무기는 ★, 지금 진화할 수 있으면 "진화!", 아니면 레벨 (상위무기-계획.md 3-5).
    /// </summary>
    string LevelLabel(IWeapon weapon)
    {
        if (weapon.Data.IsEvolved) return "★";

        EvolutionCatalog catalog = EvolutionCatalog.Instance;
        if (catalog != null && controller != null &&
            catalog.ReadyFor(weapon, controller, controller.GetComponent<SubAbilitySlot>()) != null)
            return "진화!";

        return weapon.Level > 1 ? $"Lv.{weapon.Level}" : string.Empty;
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
