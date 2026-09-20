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

    [Header("Stamina / Roll")]
    [Tooltip("스태미나 바 (Image Type: Filled)")]
    public Image staminaBar;

    [Tooltip("구르기 아이콘")]
    public Image rollIcon;

    [Tooltip("구르기 쿨타임을 덮는 이미지 (Image Type: Filled, Radial 360)")]
    public Image rollCooldownFill;

    [Tooltip("구르기가 준비되기까지 남은 초")]
    public TextMeshProUGUI rollCooldownText;

    [Header("Stamina / Roll Colors")]
    public Color staminaColor = new Color(0.45f, 0.80f, 1f, 1f);

    [Tooltip("스태미나가 바닥나 달릴 수 없는 동안")]
    public Color staminaExhaustedColor = new Color(0.95f, 0.45f, 0.35f, 1f);

    public Color rollReadyColor = Color.white;
    public Color rollCoolingColor = new Color(1f, 1f, 1f, 0.35f);

    PlayerStats stats;

    // 스태미나와 쿨타임은 매 프레임 연속으로 변한다.
    // 이벤트로 쏘면 초당 수십 번이라, 여기서는 폴링이 더 단순하고 싸다.
    PlayerMove playerMove;

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

        playerMove = t.GetComponent<PlayerMove>();

        if (playerMove != null)
        {
            UpdateMoveSpeed(playerMove.CurrentSpeed);
        }
    }

    void Update()
    {
        if (playerMove == null) return;

        UpdateStamina();
        UpdateRollCooldown();
    }

    void UpdateStamina()
    {
        if (staminaBar == null) return;

        staminaBar.fillAmount = playerMove.StaminaNormalized;

        // 바닥나면 색을 바꿔, 왜 안 달려지는지 바로 알 수 있게 한다
        staminaBar.color = playerMove.IsExhausted ? staminaExhaustedColor : staminaColor;
    }

    void UpdateRollCooldown()
    {
        float remaining = playerMove.RollCooldownRemaining;
        float total = playerMove.RollCooldownTotal;

        if (rollCooldownFill != null)
            rollCooldownFill.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;

        if (rollIcon != null)
            rollIcon.color = remaining > 0f ? rollCoolingColor : rollReadyColor;

        if (rollCooldownText != null)
        {
            bool cooling = remaining > 0.05f;

            rollCooldownText.gameObject.SetActive(cooling);

            if (cooling)
                rollCooldownText.text = remaining.ToString("F1");
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