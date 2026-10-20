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

    [Header("Ammo")]
    [Tooltip("탄약 표시. 탄창을 쓰지 않는 무기(검·드론)를 들면 자동으로 숨긴다")]
    public TextMeshProUGUI ammoText;

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

    [Header("Ammo / Stamina / Roll Colors")]
    public Color ammoPipFullColor = new Color(1f, 0.93f, 0.70f, 1f);
    public Color ammoPipEmptyColor = new Color(1f, 1f, 1f, 0.18f);

    public Color staminaColor = new Color(0.45f, 0.80f, 1f, 1f);

    [Tooltip("스태미나가 바닥나 달릴 수 없는 동안")]
    public Color staminaExhaustedColor = new Color(0.95f, 0.45f, 0.35f, 1f);

    public Color rollReadyColor = Color.white;
    public Color rollCoolingColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Parry")]
    [Tooltip("패링을 헛쳤을 때 회피 아이콘을 잠깐 이 색으로. 5초가 왜 묶였는지 바로 알 수 있게 한다")]
    public Color parryWhiffColor = new Color(1f, 0.35f, 0.3f, 1f);

    [Tooltip("패링에 성공했을 때")]
    public Color parrySuccessColor = new Color(0.55f, 0.95f, 1f, 1f);

    [Min(0f)] public float parryFlashDuration = 0.3f;

    PlayerStats stats;

    // 회피 아이콘 — 캐릭터마다 다르다 (구르기 / 방패). 원래 스프라이트를 기억해 두고 되돌린다
    Sprite defaultRollSprite;
    bool defaultRollSpriteCached;
    ParryController parry;
    float parryFlashTimer;
    Color parryFlashColor;

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
        }

        if (parry != null)
            parry.OnParryResolved -= OnParryResolved;
    }

    void Start()
    {
        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }

        if (nowStage != null) UpdateStageProgress(0, nowStage.killTarget);
        else if (killText != null) killText.gameObject.SetActive(false);
    }

    void SetPlayer(Transform t)
    {
        if (stats != null)
        {
            stats.OnHpChanged -= UpdateHp;
            stats.OnHpChanged -= UpdateHpText;
            stats.OnExpChanged -= UpdateExp;
        }
        stats = t != null ? t.GetComponent<PlayerStats>() : null;

        if (stats == null) return;

        stats.OnHpChanged += UpdateHp;
        stats.OnHpChanged += UpdateHpText;
        stats.OnExpChanged += UpdateExp;

        // 초기 UI 업데이트
        UpdateHp(stats.currentHp, stats.maxHp);
        UpdateHpText(stats.currentHp, stats.maxHp);
        UpdateExp(stats.currentExp);

        WeaponController weapons = t.GetComponent<WeaponController>();
        if (weapons != null && weapons.ActiveWeapon != null)
        {
            UpdateBulletDamage(weapons.ActiveWeapon.Stats.Damage);
            UpdateBulletSpeed(weapons.ActiveWeapon.Stats.ProjectileSpeed);
        }
        else if (weapons == null)
        {
            Gun gun = t.GetComponentInChildren<Gun>();
            if (gun != null)
            {
                UpdateBulletDamage(gun.bulletDamage);
                UpdateBulletSpeed(gun.bulletSpeed);
            }
        }

        playerMove = t.GetComponent<PlayerMove>();

        if (playerMove != null)
        {
            UpdateMoveSpeed(playerMove.CurrentSpeed);
        }

        ApplyDodgeIcon(t);
    }

    /// <summary>
    /// 회피 아이콘을 캐릭터에 맞춘다. 쿨타임 표시는 PlayerMove.RollCooldownRemaining 이
    /// 회피 종류에 맞는 값을 돌려주므로 그대로 쓴다 — 여기서는 그림과 깜빡임만 바꾼다.
    /// </summary>
    void ApplyDodgeIcon(Transform player)
    {
        if (parry != null)
            parry.OnParryResolved -= OnParryResolved;

        parry = null;
        parryFlashTimer = 0f;

        if (rollIcon != null && !defaultRollSpriteCached)
        {
            defaultRollSprite = rollIcon.sprite;
            defaultRollSpriteCached = true;
        }

        CharacterLoadout loadout = player != null ? player.GetComponent<CharacterLoadout>() : null;
        CharacterData character = loadout != null ? loadout.Data : null;

        if (rollIcon != null)
            rollIcon.sprite = character != null && character.dodgeIcon != null ? character.dodgeIcon : defaultRollSprite;

        if (character == null || character.dodge != DodgeType.Parry) return;

        parry = player.GetComponent<ParryController>();

        if (parry != null)
            parry.OnParryResolved += OnParryResolved;
    }

    void OnParryResolved(bool success)
    {
        parryFlashTimer = parryFlashDuration;
        parryFlashColor = success ? parrySuccessColor : parryWhiffColor;
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

        if (parryFlashTimer > 0f)
            parryFlashTimer -= Time.deltaTime;

        if (rollIcon != null)
        {
            if (parryFlashTimer > 0f)
                rollIcon.color = parryFlashColor;
            else
                rollIcon.color = remaining > 0f ? rollCoolingColor : rollReadyColor;
        }

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
