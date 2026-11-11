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
    private bool clicked;

    public void Setup(StageReward data, GameObject player)
    {
        reward = data;
        clicked = false;

        if (data == null) return;

        if (icon != null)
        {
            icon.sprite = data.Icon;

            // 스프라이트가 없으면 흰 사각형이 보인다
            icon.enabled = data.Icon != null;
        }

        if (title != null) title.text = data.Title;
        if (description != null) description.text = data.Description;

        UpdateTag(data, player);
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
