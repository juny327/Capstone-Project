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

    // 부착물 칸 자리 (Tools / 재화 · 상점 이 만든 칸 — 첫 칸 x 92, 52 간격)
    const float SlotX0 = 92f, SlotStep = 52f;
    [SerializeField] Color highlightColor = new Color(0.6f, 1f, 0.9f, 1f);

    /// <summary>대상 고르기 중 총을 클릭했을 때.</summary>
    public event Action<IWeapon> Picked;

    /// <summary>대상 고르기 중 마우스가 올라간 총 (나가면 null).</summary>
    public event Action<IWeapon> Hovered;

    readonly HashSet<IWeapon> candidates = new HashSet<IWeapon>();
    int highlightSlot = -1;
    bool targeting;
    float blink;

    // 강화한 무기 칸 반짝임 (2026-11-17 강화 연출) — 정비 창은 시간이 멈춰 있어 실제 시간으로
    const float UpgradeFlashTime = 0.8f;
    static readonly Color UpgradeFlashColor = new Color(1f, 0.83f, 0.35f, 0.6f);
    readonly List<WeaponBase> flashPending = new List<WeaponBase>();
    readonly Dictionary<WeaponBlock, (Image image, float age)> flashing = new Dictionary<WeaponBlock, (Image, float)>();

    /// <summary>지금 반짝이는 칸 수 (시험용).</summary>
    public int FlashingCount => flashing.Count;

    void OnEnable() => WeaponBase.LevelRaised += OnLevelRaised;
    void OnDisable()
    {
        WeaponBase.LevelRaised -= OnLevelRaised;
        foreach (var pair in flashing)
        {
            if (pair.Value.image != null) pair.Value.image.enabled = false;
            if (pair.Key.root != null) pair.Key.root.transform.localScale = Vector3.one;
        }
        flashing.Clear();
        flashPending.Clear();
    }

    void OnLevelRaised(WeaponBase weapon, int from)
    {
        if (weapon != null && !flashPending.Contains(weapon)) flashPending.Add(weapon);
    }

    void UpdateUpgradeFlash()
    {
        // 강화 직후 판이 다시 그려지면(Refresh) 그 무기 칸을 찾아 반짝인다
        for (int i = flashPending.Count - 1; i >= 0; i--)
        {
            WeaponBlock b = blocks == null ? null : System.Array.Find(blocks, x => x != null && x.root != null && x.root.activeInHierarchy && ReferenceEquals(x.weapon, flashPending[i]));
            if (b == null) continue;
            flashPending.RemoveAt(i);
            flashing[b] = (FlashImage(b), 0f);
        }

        if (flashing.Count == 0) return;
        var done = new List<WeaponBlock>();
        foreach (WeaponBlock b in new List<WeaponBlock>(flashing.Keys))
        {
            (Image image, float age) = flashing[b];
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / UpgradeFlashTime);
            if (image != null)
            {
                image.enabled = t < 1f;
                image.color = new Color(UpgradeFlashColor.r, UpgradeFlashColor.g, UpgradeFlashColor.b, UpgradeFlashColor.a * (1f - t) * (1f - t));
            }
            if (b.root != null) b.root.transform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Mathf.Min(1f, t * 2.5f) * Mathf.PI));
            if (t >= 1f) done.Add(b);
            else flashing[b] = (image, age);
        }
        foreach (WeaponBlock b in done)
        {
            if (b.root != null) b.root.transform.localScale = Vector3.one;
            flashing.Remove(b);
        }
    }

    /// <summary>칸 위에 덮는 금빛 판 (처음 쓸 때 만든다).</summary>
    static Image FlashImage(WeaponBlock b)
    {
        Transform existing = b.root.transform.Find("UpgradeFlash");
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        if (image == null)
        {
            var go = new GameObject("UpgradeFlash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(b.root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            image = go.GetComponent<Image>();
            image.raycastTarget = false;
        }
        image.transform.SetAsLastSibling();
        image.enabled = true;
        return image;
    }

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

        // 보조 무기 방식(사수) — 빈 칸은 보조 칸 (드론도 같은 칸을 쓴다)
        bool support = ctx?.Weapons != null && ctx.Weapons.UsesSupportWeapons;
        int free = ctx?.Weapons == null ? 0
                 : support ? Mathf.Max(0, ctx.Weapons.SupportSlots - ctx.Weapons.SupportCount)
                 : Mathf.Max(0, ctx.Weapons.MaxHeldSlots - count);
        if (emptySlotText != null)
        {
            emptySlotText.gameObject.SetActive(free > 0);
            emptySlotText.text = support ? $"빈 보조 칸 {free}칸" : $"빈 무기 슬롯 {free}칸";
        }

        RefreshAbilities(ctx);
        ApplyTargetLook();
    }

    void FillBlock(WeaponBlock b)
    {
        WeaponData data = b.weapon.Data;

        if (b.icon != null) { b.icon.sprite = data.icon; b.icon.enabled = data.icon != null; }
        WeaponBase wb = b.weapon as WeaponBase;
        bool support = wb != null && wb.IsSupport;
        // 보조 무기 방식이면 손에 든 무기에 "주무기" — HUD 슬롯 표시와 같게 (2026-11-11)
        WeaponController owner = wb != null ? wb.GetComponentInParent<WeaponController>() : null;
        bool main = !support && owner != null && owner.UsesSupportWeapons;
        string role = support ? (data.SupportOnly ? "<color=#9AA3BD>드론</color>  " : "<color=#9AA3BD>보조</color>  ")
                    : main ? "<color=#F4CD75>주무기</color>  " : string.Empty;
        if (b.title != null) b.title.text = $"{role}{data.weaponName}  Lv{b.weapon.Level}";

        WeaponFamily family = AttachmentData.FamilyOf(data);
        bool attachable = family != WeaponFamily.None && family != WeaponFamily.Melee;
        var names = new StringBuilder();

        int shownSlots = 0;
        for (int s = 0; s < AttachmentData.SlotCount; s++)
        {
            AttachmentData a = attachable ? b.weapon.GetAttachment((AttachmentSlot)s) : null;

            if (b.slotFrames != null && s < b.slotFrames.Length && b.slotFrames[s] != null)
            {
                // 보조 무기는 개조 칸만 보인다 (다른 부위는 달 수 없다).
                // 정비 창에서 이 무기에 파는 부위만 보이고(비어 있을 때), 보이는 칸을 앞으로 당긴다 (2026-11-17)
                bool show = attachable && (!support || s == (int)AttachmentSlot.Mod)
                            && !(a == null && !ShopCatalog.SellsAttachment(data, (AttachmentSlot)s));
                var cell = (RectTransform)b.slotFrames[s].transform.parent;
                cell.gameObject.SetActive(show);
                if (show) cell.anchoredPosition = new Vector2(SlotX0 + shownSlots++ * SlotStep, cell.anchoredPosition.y);
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
            b.parts.text = !attachable ? "근접 무기 — 부착물 없음"
                         : names.Length > 0 ? names.ToString()
                         : support ? (ShopCatalog.SellsAttachment(data, AttachmentSlot.Mod) ? "보조 무기 — 전용 개조 칸만" : "보조 무기 — 부착물 없음")
                         : "부착물 없음";
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
        UpdateUpgradeFlash();
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
