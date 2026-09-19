using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeCard : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public TextMeshProUGUI title;
    public TextMeshProUGUI description;
    public TextMeshProUGUI cost;

    [Header("Visual")]
    public Animator animator; // unscale 모드로 해서 timeScale 0 이여도 재생

    [Header("Sound")]
    public AudioSource audioSource;
    public AudioClip successSound;
    public AudioClip failSound;

    UpgradeData data;

    public void Setup(UpgradeData upgrade)
    {
        data = upgrade;

        if (upgrade == null) return;

        // 문구는 효과에게 묻는다. 서브유닛 카드는 WeaponData 의 이름·설명을 돌려주고,
        // 스탯 카드는 UpgradeData 의 값을 그대로 돌려준다 (13번 7-3).
        UpgradeEffect effect = upgrade.effect;

        if (title != null)
            title.text = effect != null ? effect.GetTitle(upgrade) : upgrade.upgradeName;

        if (description != null)
            description.text = effect != null ? effect.GetDescription(upgrade) : upgrade.description;

        if (icon != null)
        {
            Sprite sprite = effect != null ? effect.GetIcon(upgrade) : upgrade.icon;

            icon.sprite = sprite;
            icon.enabled = sprite != null;   // 아이콘이 아직 없으면 빈 네모가 보이지 않게 끈다
        }

        // 레벨업 보상이라 비용이 없다. 0 이면 표시 자체를 숨긴다
        if (cost != null)
        {
            bool hasCost = upgrade.costExp > 0;

            cost.gameObject.SetActive(hasCost);

            if (hasCost)
                cost.text = $"cost Exp : {upgrade.costExp}";
        }
    }

    void OnEnable()
    {
        GameEvents.OnUpgradeSuccess += OnSuccess;
        GameEvents.OnUpgradeFailed += OnFailed;
    }

    void OnDisable()
    {
        GameEvents.OnUpgradeSuccess -= OnSuccess;
        GameEvents.OnUpgradeFailed -= OnFailed;
    }

    public void OnClick()
    {
        GameEvents.OnUpgradeSelected?.Invoke(data);
    }

    void OnSuccess(UpgradeData d)
    {
        if (d != data) return;

        animator.Play("Success");

        if (audioSource && successSound)
            audioSource.PlayOneShot(successSound);
    }

    void OnFailed(UpgradeData d)
    {
        if (d != data) return;

        animator.Play("Fail");

        if (audioSource && failSound)
            audioSource.PlayOneShot(failSound);
    }
}