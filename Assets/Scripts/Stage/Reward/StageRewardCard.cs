using TMPro;
using UnityEngine;
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

    [Tooltip("이미 가진 무기일 때 '레벨 업' 을 띄운다. 없어도 동작한다")]
    public TextMeshProUGUI tag;

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
        if (tag == null) return;

        // 종류를 묻는 유일한 곳이다. 모르는 종류면 그냥 표시하지 않는다.
        bool owned = data is WeaponReward weaponReward && weaponReward.IsOwnedBy(player);

        tag.gameObject.SetActive(owned);

        if (owned) tag.text = "레벨 업";
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
