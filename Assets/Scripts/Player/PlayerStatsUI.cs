using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting.Antlr3.Runtime.Misc;

public class PlayerStatsUI : MonoBehaviour
{
    public Image hpBar;
    public TextMeshProUGUI expText;
    public TextMeshProUGUI killText;
    public TextMeshProUGUI bulletDamageText;
    public TextMeshProUGUI bulletSpeedText;
    public StageData nowStage;
    public Animator anim;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI moveSpeedText;
    

    PlayerStats stats;

    void OnEnable()
    {
        GameEvents.OnPlayerSpawned += SetPlayer;
        GameEvents.OnStageProgress += UpdateStageProgress;
        GameEvents.OnBulletDamageChanged += UpdateBulletDamage;
        GameEvents.OnMoveSpeedChanged += UpdateMoveSpeed;
        GameEvents.OnBulletSpeedChanged += UpdateBulletSpeed;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerSpawned -= SetPlayer;
        GameEvents.OnStageProgress -= UpdateStageProgress;
        GameEvents.OnBulletDamageChanged -= UpdateBulletDamage;
        GameEvents.OnMoveSpeedChanged -= UpdateMoveSpeed;
        GameEvents.OnBulletSpeedChanged -= UpdateBulletSpeed;

        if (stats != null)
        {
            stats.OnHpChanged -= UpdateHp;
            stats.OnHpChanged -= UpdateHpText;
            stats.OnExpChanged -= UpdateExp;
        }
    }

    void Start()
    {
        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }

        UpdateStageProgress(0, nowStage.killTarget);
    }

    void SetPlayer(Transform t)
    {
        stats = t.GetComponent<PlayerStats>();

        if (stats == null) return;

        stats.OnHpChanged += UpdateHp;
        stats.OnHpChanged += UpdateHpText;
        stats.OnExpChanged += UpdateExp;

        // 초기 UI 업데이트
        UpdateHp(stats.currentHp, stats.maxHp);
        UpdateHpText(stats.currentHp, stats.maxHp);
        UpdateExp(stats.currentExp);

        Gun gun = t.GetComponentInChildren<Gun>();
        if (gun != null)
        {
            UpdateBulletDamage(gun.bulletDamage);
            UpdateBulletSpeed(gun.bulletSpeed);
        }

        PlayerMove move = t.GetComponent<PlayerMove>();
        if (move != null)
        {
            UpdateMoveSpeed(move.CurrentSpeed);   
        }
    }

    void UpdateHp(int hp, int max)
    {
        if (hpBar == null) return;

        hpBar.fillAmount = (float)hp / max;

        if (anim != null)
        {
            anim.Play("HPBar");
        }
    }
    void UpdateHpText(int hp, int max)
    {
        if(hpText == null) return;

        hpText.text = $"{hp} / {max}";
    }

    void UpdateExp(int exp)
    {
        if (expText == null) return;

        expText.text = "EXP : " + exp;
    }
    void UpdateMoveSpeed(float speed)
    {
        if (moveSpeedText == null) return;

        moveSpeedText.text = "Speed : " + speed.ToString("F1");
    }

    void UpdateStageProgress(int current, int target)
    {
        if (killText == null) return;

        killText.text = current + " / " + target;
    }

    void UpdateBulletDamage(float damage)
    {
        if (bulletDamageText == null) return;

        bulletDamageText.text = "BulletPower : " + damage;
    }
    
    void UpdateBulletSpeed(float speed)
    {
        if(bulletSpeedText == null) return;

        bulletSpeedText.text = "GunSpeed : " + speed;
    }
}