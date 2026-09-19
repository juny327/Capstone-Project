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
        data = upgrade;
        clicked = false;

        icon.sprite = upgrade.icon;
        title.text = upgrade.upgradeName;
        description.text = upgrade.description;

        if (cost != null)
            cost.gameObject.SetActive(false);
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
        if (d != data) return;

        if (animator != null)
            animator.Play("Success");

        if (audioSource && successSound)
            audioSource.PlayOneShot(successSound);
    }

    void OnFailed(UpgradeData d)
    {
        if (d != data) return;

        clicked = false;

        if (animator != null)
            animator.Play("Fail");

        if (audioSource && failSound)
            audioSource.PlayOneShot(failSound);
    }
}
