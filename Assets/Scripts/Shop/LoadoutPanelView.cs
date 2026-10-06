using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정비 창 오른쪽 "내 장비" 판 (커스터마이징-구현계획.md 7장).
///
///  · 가진 손 무기마다 한 칸 — 그림 · 이름 · 레벨 + 부착물 5칸(그림) + 단 부착물 이름 한 줄
///  · 서브 능력 칸이 있는 캐릭터(검사)는 아래에 E · F 줄
///  · 부착물을 사는 중이면(대상 고르기) 달 수 있는 총만 밝아지고 클릭할 수 있다. 마우스를 올리면 바뀌는 수치를 알린다
/// </summary>
public class LoadoutPanelView : MonoBehaviour
{
    [Serializable]
    public class WeaponBlock
    {
        public GameObject root;
        public Button button;
        public Image highlight;
        public Image icon;
        public TMP_Text title;
        public TMP_Text parts;
        public Image[] slotFrames = new Image[AttachmentData.SlotCount];
        public Image[] slotIcons = new Image[AttachmentData.SlotCount];
        public CanvasGroup group;
        public HoverRelay hover;
        [NonSerialized] public IWeapon weapon;
    }

    [SerializeField] WeaponBlock[] blocks;
    [SerializeField] TMP_Text emptySlotText;
    [SerializeField] GameObject abilitySection;
    [SerializeField] EquipmentRowView[] abilityRows;

    [SerializeField] Color emptyFrame = new Color(1f, 1f, 1f, 0.18f);
    [SerializeField] Color highlightColor = new Color(0.6f, 1f, 0.9f, 1f);

    /// <summary>대상 고르기 중 총을 클릭했을 때.</summary>
    public event Action<IWeapon> Picked;

    /// <summary>대상 고르기 중 마우스가 올라간 총 (나가면 null).</summary>
    public event Action<IWeapon> Hovered;

    readonly HashSet<IWeapon> candidates = new HashSet<IWeapon>();
    int highlightSlot = -1;
    bool targeting;
    float blink;

    void Awake()
    {
        if (blocks == null) return;

        foreach (WeaponBlock block in blocks)
        {
            WeaponBlock b = block;
            if (b.button != null) b.button.onClick.AddListener(() => OnBlockClicked(b));
            if (b.hover != null)
            {
                b.hover.Entered += () => { if (targeting && b.weapon != null && candidates.Contains(b.weapon)) Hovered?.Invoke(b.weapon); };
                b.hover.Exited += () => { if (targeting) Hovered?.Invoke(null); };
            }
        }
    }

    public void Refresh(ShopContext ctx)
    {
        IReadOnlyList<IWeapon> held = ctx?.Weapons != null ? ctx.Weapons.HeldWeapons : null;
        int count = held != null ? held.Count : 0;

        for (int i = 0; blocks != null && i < blocks.Length; i++)
        {
            WeaponBlock b = blocks[i];
            if (b == null || b.root == null) continue;

            if (i >= count) { b.weapon = null; b.root.SetActive(false); continue; }

            b.weapon = held[i];
            b.root.SetActive(true);
            FillBlock(b);
        }

        int free = ctx?.Weapons != null ? Mathf.Max(0, ctx.Weapons.MaxHeldSlots - count) : 0;
        if (emptySlotText != null)
        {
            emptySlotText.gameObject.SetActive(free > 0);
            emptySlotText.text = $"빈 무기 슬롯 {free}칸";
        }

        RefreshAbilities(ctx);
        ApplyTargetLook();
    }

    void FillBlock(WeaponBlock b)
    {
        WeaponData data = b.weapon.Data;

        if (b.icon != null) { b.icon.sprite = data.icon; b.icon.enabled = data.icon != null; }
        if (b.title != null) b.title.text = $"{data.weaponName}  Lv{b.weapon.Level}";

        WeaponFamily family = AttachmentData.FamilyOf(data);
        bool attachable = family != WeaponFamily.None && family != WeaponFamily.Melee;
        var names = new StringBuilder();

        for (int s = 0; s < AttachmentData.SlotCount; s++)
        {
            AttachmentData a = attachable ? b.weapon.GetAttachment((AttachmentSlot)s) : null;

            if (b.slotFrames != null && s < b.slotFrames.Length && b.slotFrames[s] != null)
            {
                b.slotFrames[s].transform.parent.gameObject.SetActive(attachable);
                b.slotFrames[s].color = a != null ? AttachmentData.TierColor(a.tier) : emptyFrame;
            }

            if (b.slotIcons != null && s < b.slotIcons.Length && b.slotIcons[s] != null)
            {
                b.slotIcons[s].sprite = a != null ? a.icon : null;
                b.slotIcons[s].enabled = a != null && a.icon != null;
            }

            if (a != null) names.Append(names.Length > 0 ? " · " : string.Empty).Append(a.displayName);
        }

        if (b.parts != null)
            b.parts.text = !attachable ? "근접 무기 — 부착물 없음" : names.Length > 0 ? names.ToString() : "부착물 없음";
    }

    void RefreshAbilities(ShopContext ctx)
    {
        SubAbilitySlot slot = ctx?.Player != null ? ctx.Player.GetComponent<SubAbilitySlot>() : null;
        bool show = slot != null && slot.SlotCount > 0;

        if (abilitySection != null) abilitySection.SetActive(show);
        if (!show || abilityRows == null) return;

        KeyBindings keys = KeyBindings.Current;
        for (int i = 0; i < abilityRows.Length; i++)
        {
            if (abilityRows[i] == null) continue;
            if (i < slot.SlotCount) abilityRows[i].ShowAbility(KeyBindings.Label(keys.AbilityKey(i)), slot.Get(i), false);
            else abilityRows[i].Hide();
        }
    }

    // ───────── 대상 고르기 ─────────

    public void BeginTargeting(IEnumerable<IWeapon> weapons, int slot)
    {
        candidates.Clear();
        foreach (IWeapon w in weapons) candidates.Add(w);

        highlightSlot = slot;
        targeting = true;
        blink = 0f;
        ApplyTargetLook();
    }

    public void EndTargeting()
    {
        targeting = false;
        candidates.Clear();
        highlightSlot = -1;
        ApplyTargetLook();
    }

    void OnBlockClicked(WeaponBlock b)
    {
        if (!targeting || b.weapon == null || !candidates.Contains(b.weapon)) return;
        Picked?.Invoke(b.weapon);
    }

    void ApplyTargetLook()
    {
        if (blocks == null) return;

        foreach (WeaponBlock b in blocks)
        {
            if (b == null || b.root == null || !b.root.activeSelf) continue;

            bool eligible = targeting && b.weapon != null && candidates.Contains(b.weapon);
            if (b.group != null) b.group.alpha = targeting && !eligible ? 0.35f : 1f;
            if (b.button != null) b.button.interactable = eligible;
            if (b.highlight != null) b.highlight.enabled = eligible;
        }
    }

    void Update()
    {
        if (!targeting || blocks == null || highlightSlot < 0) return;

        // 고르는 중인 부위 칸을 깜빡인다
        blink += Time.unscaledDeltaTime * 4f;
        float t = (Mathf.Sin(blink) + 1f) * 0.5f;

        foreach (WeaponBlock b in blocks)
        {
            if (b == null || b.weapon == null || !candidates.Contains(b.weapon)) continue;
            if (b.slotFrames == null || highlightSlot >= b.slotFrames.Length || b.slotFrames[highlightSlot] == null) continue;

            AttachmentData a = b.weapon.GetAttachment((AttachmentSlot)highlightSlot);
            Color baseColor = a != null ? AttachmentData.TierColor(a.tier) : emptyFrame;
            b.slotFrames[highlightSlot].color = Color.Lerp(baseColor, highlightColor, t);
        }
    }
}
