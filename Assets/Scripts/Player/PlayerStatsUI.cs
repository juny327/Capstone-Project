using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Tooltip("탄약 표시. 탄창을 쓰지 않는 무기(검·드론)를 들면 자동으로 숨긴다")]
    public TextMeshProUGUI ammoText;

    [Tooltip("현재 레벨 표시")]
    public TextMeshProUGUI levelText;

    [Tooltip("다음 레벨까지의 진행도 바 (Image Type: Filled)")]
    public Image expBar;

    [Tooltip("총알 모양 표시. 왼쪽부터 채워진다")]
    public Image[] ammoPips;

    [Header("Stamina / Roll")]
    [Tooltip("스태미나 바 (Image Type: Filled)")]
    public Image staminaBar;

    [Tooltip("구르기 아이콘")]
    public Image rollIcon;

    [Tooltip("구르기 쿨타임을 덮는 이미지 (Image Type: Filled, Radial 360)")]
    public Image rollCooldownFill;

    [Tooltip("구르기가 준비되기까지 남은 초")]
    public TextMeshProUGUI rollCooldownText;

    [Header("Colors")]
    [Tooltip("체력 60% 초과")]
    public Color hpHealthyColor = new Color(0.35f, 0.85f, 0.35f, 1f);

    [Tooltip("체력 30~60%")]
    public Color hpWarnColor = new Color(0.95f, 0.80f, 0.25f, 1f);

    [Tooltip("체력 30% 이하")]
    public Color hpDangerColor = new Color(0.90f, 0.25f, 0.25f, 1f);

    public Color ammoPipFullColor = new Color(1f, 0.93f, 0.70f, 1f);
    public Color ammoPipEmptyColor = new Color(1f, 1f, 1f, 0.18f);

    [Tooltip("평소 스태미나 색")]
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
        GameEvents.OnAmmoChanged += UpdateAmmo;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerSpawned -= SetPlayer;
        GameEvents.OnStageProgress -= UpdateStageProgress;
        GameEvents.OnBulletDamageChanged -= UpdateBulletDamage;
        GameEvents.OnMoveSpeedChanged -= UpdateMoveSpeed;
        GameEvents.OnBulletSpeedChanged -= UpdateBulletSpeed;
        GameEvents.OnAmmoChanged -= UpdateAmmo;

        if (stats != null)
        {
            stats.OnHpChanged -= UpdateHp;
            stats.OnHpChanged -= UpdateHpText;
            stats.OnExpChanged -= UpdateExp;
            stats.OnLevelChanged -= UpdateLevel;
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
        stats.OnLevelChanged += UpdateLevel;

        // 초기 UI 업데이트
        UpdateHp(stats.currentHp, stats.maxHp);
        UpdateHpText(stats.currentHp, stats.maxHp);
        UpdateExp(stats.currentExp);

        // 활성 무기의 실제 스탯을 읽는다.
        //
        // WeaponController 도 스왑·획득 때 이벤트를 쏘지만, 스테이지를 넘어오면 플레이어가
        // 이미 살아 있어 Start 가 다시 돌지 않는다. 그래서 여기서 한 번 직접 읽어 줘야
        // 새 씬의 HUD 가 빈 채로 남지 않는다.
        WeaponController weapons = t.GetComponent<WeaponController>();

        if (weapons != null && weapons.ActiveWeapon != null)
        {
            WeaponRuntimeStats weaponStats = weapons.ActiveWeapon.Stats;

            UpdateBulletDamage(weaponStats.Damage);
            UpdateBulletSpeed(weaponStats.ProjectileSpeed);
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

        float ratio = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;

        hpBar.fillAmount = ratio;

        // 구간별 색. 숫자를 읽지 않아도 위급함이 눈에 들어온다 (13번 10-3)
        hpBar.color = ratio > 0.6f ? hpHealthyColor
                    : ratio > 0.3f ? hpWarnColor
                                   : hpDangerColor;

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
        // 레벨업이 목표가 된 이상, 남은 양을 알 수 있어야 한다 (13번 11-7)
        int need = stats != null ? stats.ExpToNext : 0;

        if (expText != null)
            expText.text = need > 0 ? $"{exp} / {need}" : exp.ToString();

        if (expBar != null)
            expBar.fillAmount = need > 0 ? Mathf.Clamp01((float)exp / need) : 0f;

        UpdateLevel(stats != null ? stats.level : 1);
    }

    void UpdateLevel(int level)
    {
        if (levelText == null) return;

        levelText.text = $"Lv.{level}";
    }
    void UpdateMoveSpeed(float speed)
    {
        if (moveSpeedText == null) return;

        moveSpeedText.text = $"이동 {speed:F1}";
    }

    void UpdateStageProgress(int current, int target)
    {
        if (killText == null) return;

        killText.text = current + " / " + target;
    }

    void UpdateBulletDamage(float damage)
    {
        if (bulletDamageText == null) return;

        // 자릿수를 고정하지 않으면 업그레이드가 누적됐을 때 10.000001 처럼 나온다
        bulletDamageText.text = $"공격력 {damage:F1}";
    }

    void UpdateBulletSpeed(float speed)
    {
        if(bulletSpeedText == null) return;

        bulletSpeedText.text = $"탄속 {speed:F0}";
    }

    /// <summary>
    /// 탄약 표시. 탄창 크기가 0 이면 탄창을 쓰지 않는 무기(검·드론)이므로 숨긴다.
    /// 장전 중에는 무기가 현재 탄약을 0 으로 보내므로 "Reloading" 으로 표시한다.
    /// </summary>
    void UpdateAmmo(int ammo, int magazine)
    {
        bool hasMagazine = magazine > 0;

        if (ammoText != null)
        {
            ammoText.gameObject.SetActive(hasMagazine);

            if (hasMagazine)
                ammoText.text = ammo <= 0 ? "Reloading..." : $"{ammo} / {magazine}";
        }

        UpdateAmmoPips(ammo, magazine, hasMagazine);
    }

    /// <summary>
    /// 총알 모양 칸을 채운다.
    ///
    /// 탄창이 칸 수보다 크면(소총 30, 기관단총 45) 한 칸이 여러 발을 대표한다.
    /// 45개를 그리면 읽히지 않으므로, 칸 수를 고정하고 비율로 채우는 편이 낫다.
    /// </summary>
    void UpdateAmmoPips(int ammo, int magazine, bool hasMagazine)
    {
        if (ammoPips == null || ammoPips.Length == 0) return;

        // 탄창이 칸 수보다 적으면(스나이퍼 5발) 실제 발수만큼만 쓴다 — 한 칸이 정확히 한 발이 된다
        int used = hasMagazine ? Mathf.Min(magazine, ammoPips.Length) : 0;

        float ratio = magazine > 0 ? Mathf.Clamp01((float)ammo / magazine) : 0f;

        // 한 발이라도 남았으면 칸 하나는 켜 둔다 (Ceil)
        int filled = Mathf.CeilToInt(ratio * used);

        for (int i = 0; i < ammoPips.Length; i++)
        {
            Image pip = ammoPips[i];

            if (pip == null) continue;

            bool inUse = i < used;

            pip.gameObject.SetActive(inUse);

            if (inUse)
                pip.color = i < filled ? ammoPipFullColor : ammoPipEmptyColor;
        }
    }
}