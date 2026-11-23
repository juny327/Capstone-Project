using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 보상 카드 한 장. `UpgradeCard` 와 같은 구조다.
///
/// 카드는 <see cref="StageReward"/> 만 안다. 무기인지 부착물인지 묻지 않으므로
/// 보상 종류가 늘어도 이 스크립트는 그대로다.
/// </summary>
public class StageRewardCard : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public TextMeshProUGUI title;
    public TextMeshProUGUI description;

    // 예전 이름 tag 는 GameObject 태그(Component.tag)와 겹쳐 CS0108 경고가 났다 — 씬에 저장된 값은 그대로 읽힌다 (2026-11-06)
    [Tooltip("이미 가진 무기일 때 '레벨 업' 을 띄운다. 없어도 동작한다")]
    [FormerlySerializedAs("tag")]
    public TextMeshProUGUI tagLabel;

    [Header("Sound")]
    public AudioSource audioSource;
    public AudioClip selectSound;

    private StageReward reward;
    private GameObject owner;
    private bool clicked;

    // '상세 설명' 토글이 바뀌면 글만 다시 (2026-11-22)
    void OnEnable() => DescriptionMode.Changed += OnDescriptionMode;
    void OnDisable() => DescriptionMode.Changed -= OnDescriptionMode;

    void OnDescriptionMode(bool detail)
    {
        if (reward == null || description == null) return;
        CardDetailLayout.Apply(icon, title, description, detail);
        description.text = UiTextFit.KeepWords(DescriptionFor(reward, owner));
        if (tagLabel != null) UiTextFit.KeepAbove(description, tagLabel.rectTransform);
    }

    /// <summary>
    /// 카드 설명 — 이미 가진 무기('레벨 업' 카드)면 무기 소개 대신 오르는 값 ("피해 +1"), 아니면 보상의 설명 (2026-11-22).
    /// </summary>
    static string DescriptionFor(StageReward data, GameObject player)
    {
        if (data is WeaponReward wr && wr.weapon != null && player != null && player.TryGetComponent(out WeaponController wc))
        {
            IWeapon owned = wc.Find(wr.weapon);
            if (owned != null) return DescText.WeaponLevelUp(wr.weapon, owned.Level);
        }

        return data.Description;
    }

    public void Setup(StageReward data, GameObject player)
    {
        reward = data;
        owner = player;
        clicked = false;

        if (data == null) return;

        if (icon != null)
        {
            icon.sprite = data.Icon;

            // 스프라이트가 없으면 흰 사각형이 보인다
            icon.enabled = data.Icon != null;
        }

        CardDetailLayout.Apply(icon, title, description, DescriptionMode.Detail);   // 상세 설명이면 그림을 접고 설명 칸을 넓힌다 (2026-11-22)
        if (title != null) { UiTextFit.Line(title); title.text = data.Title; }   // 긴 제목이 두 줄로 꺾여 설명을 덮지 않게
        if (description != null) { UiTextFit.Box(description); description.text = UiTextFit.KeepWords(DescriptionFor(data, player)); }   // 긴 설명이 아래 종류 표시를 덮지 않게

        UpdateTag(data, player);
        if (description != null && tagLabel != null) UiTextFit.KeepAbove(description, tagLabel.rectTransform);   // 설명 칸이 종류 표시 자리까지 내려와 있다
    }

    /// <summary>
    /// "새로 얻음 / 레벨 업" 표시.
    ///
    /// 이미 가진 무기가 후보로 나오면 레벨업이 되는데,
    /// 표시가 없으면 같은 무기를 또 주는 것처럼 보인다.
    /// </summary>
    void UpdateTag(StageReward data, GameObject player)
    {
        if (tagLabel == null) return;

        // 종류를 묻는 유일한 곳이다. 모르는 종류면 그냥 표시하지 않는다.
        bool owned = data is WeaponReward weaponReward && weaponReward.IsOwnedBy(player);
        string text = owned ? "레벨 업" : SupportTag(data, player);

        tagLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        if (!string.IsNullOrEmpty(text)) tagLabel.text = text;
    }

    /// <summary>보조 무기 방식(사수)에서 새 무기 카드 — 받으면 보조 칸에 들어간다는 표시.</summary>
    static string SupportTag(StageReward data, GameObject player)
    {
        if (!(data is WeaponReward wr) || wr.weapon == null || player == null) return null;

        WeaponController wc = player.GetComponent<WeaponController>();
        if (wc == null || !wc.UsesSupportWeapons || wc.MainWeapon == null) return null;

        return wr.weapon.SupportOnly ? "보조 · 드론" : "보조 무기";
    }

    /// <summary>버튼 OnClick 에 연결한다.</summary>
    public void OnClick()
    {
        if (reward == null || clicked) return;

        clicked = true;

        if (audioSource != null && selectSound != null)
            audioSource.PlayOneShot(selectSound);

        GameEvents.OnStageRewardSelected?.Invoke(reward);
    }
}
