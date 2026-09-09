using UnityEngine;
using UnityEngine.UI;

public class BossHPUI : MonoBehaviour
{
    public Image redBar;
    public Image orangeBar;
    public Image greenBar;

    public GameObject root;

    int maxHp;

    void OnEnable()
    {
        GameEvents.OnBossSpawned += Init;
    }

    void OnDisable()
    {
        GameEvents.OnBossSpawned -= Init;
    }

    void Start()
    {
        root.SetActive(false);
    }

    public void Init(BossHealth boss)
    {
        maxHp = boss.maxHp;

        boss.OnHpChanged += UpdateHP;
        boss.OnBossDamaged += ShowUI;
        boss.OnBossDead += HideUI;
    }

    void ShowUI()
    {
        root.SetActive(true);
    }

    void HideUI()
    {
        root.SetActive(false);
    }

    void UpdateHP(int hp, int max)
    {
        float section = max / 3f;

        float red = Mathf.Clamp01((hp - section * 2) / section);
        float orange = Mathf.Clamp01((hp - section * 1) / section);
        float green = Mathf.Clamp01(hp / section);

        redBar.fillAmount = red;
        orangeBar.fillAmount = orange;
        greenBar.fillAmount = green;
    }
}