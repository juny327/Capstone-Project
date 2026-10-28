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
    bool clicked;

    public void Setup(UpgradeData upgrade)
    {
        Setup(upgrade, 1);
    }

    public void Setup(UpgradeData upgrade, int nextLevel)
    {
        data = upgrade;
        clicked = false;

        if (upgrade == null)
            return;

        if (icon != null)
            icon.sprite = upgrade.icon;

        if (title != null)
            title.text = $"{upgrade.upgradeName} Lv.{Mathf.Max(1, nextLevel)}";

        if (description != null)
            description.text = upgrade.description;

        if (cost != null)
        {
            cost.gameObject.SetActive(true);
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
        if (data == null || clicked)
            return;

        clicked = true;
        GameEvents.OnUpgradeSelected?.Invoke(data);
    }

    void OnSuccess(UpgradeData d)
    {
        if (d != data)
            return;

        if (animator != null)
            animator.Play("Success");

        if (audioSource && successSound)
            audioSource.PlayOneShot(successSound);
    }

    void OnFailed(UpgradeData d)
    {
        if (d != data)
            return;

        // 경험치 부족 등으로 실패하면 다시 선택할 수 있게 한다.
        clicked = false;

        if (animator != null)
            animator.Play("Fail");

        if (audioSource && failSound)
            audioSource.PlayOneShot(failSound);
    }
}
