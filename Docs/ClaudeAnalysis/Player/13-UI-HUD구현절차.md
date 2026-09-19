# 13. UI · HUD 설계 · 구현 절차

무기 슬롯 · 서브유닛 슬롯 · 탄약 표시 · 업그레이드 카드까지, 지금까지 만든 시스템을 화면에 올리는 작업을 정리한다.
여기에 **시작 시 주 무기 선택**(7-1), **기존 HUD 의 폰트 · 가독성 개선**(10장), **레벨업 기반 업그레이드로의 전환**(11장)을 함께 다룬다.

코드는 대부분 준비돼 있고 **화면에 붙는 쪽만 비어 있다.**

선행 문서: [07-무기종류확장설계](07-무기종류확장설계.md) · [11-서브유닛구현절차](11-서브유닛구현절차.md) · [12-탄창장전구현절차](12-탄창장전구현절차.md)

---

## 한눈에 보기

| 항목 | 상태 | 비고 |
|---|---|---|
| HP 바 · 킬 수 · EXP | 있음 | 단 화면 밖으로 밀리는 문제 있음 (2장) |
| 데미지 · 탄속 · 이속 표시 | 있음 | **옛 `Gun` 값과 무기 값이 서로 덮어씀** (3장) |
| 탄약 표시 | 코드만 | `PlayerStatsUI.ammoText` 가 씬에서 미연결 (4장) |
| 손 무기 슬롯 (3칸) | 없음 | 표시에 쓸 이벤트는 준비됨 (5장) |
| 서브유닛 슬롯 (3칸) | 없음 | 6장 |
| 시작 무기 선택 | 없음 | 소총 · SMG · 스나이퍼 · 검 중 선택. 지금은 프리팹에 소총 고정 (7-1) |
| 업그레이드 카드 | 있음 | 스탯 · 서브유닛 카드가 없어 서브유닛을 얻을 방법이 없다 (7-2) |
| 무기 아이콘 | 없음 | `WeaponData.icon` 이 12종 모두 비어 있음 |
| 폰트 · 가독성 | 개선 필요 | 체력 15pt / 킬 36pt 로 크기 체계가 없고 외곽선이 없다 (10장) |
| 레벨업 업그레이드 | 미구현 | `PlayerStats.level` 이 죽은 필드. 지금은 클리어 시 EXP 를 소비한다 (11장) |
| 레벨 · EXP 바 | 없음 | 레벨업이 목표가 되면 진행도 표시가 필요하다 (11-7) |

**작업 순서의 핵심**: 슬롯 UI 를 먼저 만들어도 채울 내용이 없다.
주 무기는 **시작할 때 선택**(7-1), 서브유닛은 **레벨업 카드**(7-2 · 11장)로 들어온다.
이 두 경로가 뚫려야 슬롯 UI 가 의미를 가진다.

---

## 1. 현재 HUD 구조 (실측)

### 1-1. 캔버스 설정

Stage1 기준 (Stage2 · Stage3 · StageBoss 동일 구조).

```
CanvasScaler
  m_ReferenceResolution : 1920 x 1080
  m_ScreenMatchMode     : 0  (Match Width Or Height)
  m_MatchWidthOrHeight  : 0  (= 너비에 100% 맞춤)
```

### 1-2. HUD 요소 좌표

전부 부모 `PlayerStateUI` 아래에 있고, **앵커가 전부 (0.5, 0.5) 중앙 고정** 이며 좌표는 픽셀 오프셋이다.

| 오브젝트 | 위치 (x, y) | 크기 | 용도 |
|---|---|---|---|
| `HPBG` | (0, 452) | 1000 x 20 | 체력 바 배경 |
| `hpText` | (0, 452) | 200 x 20 | 체력 수치 |
| `killCount` | (0, 400) | 200 x 50 | 처치 수 / 목표 |
| `exp` | (725, 355) | 350 x 40 | 경험치 |
| `bulletPower` | (725, 305) | 350 x 40 | 공격력 |
| `bulletSpeed` | (725, 255) | 350 x 40 | 탄속 |
| `moveSpeed` | (725, 203) | 350 x 40 | 이동 속도 |

### 1-3. `PlayerStatsUI` 연결 현황

씬에 직렬화된 필드: `hpBar` · `expText` · `killText` · `bulletDamageText` · `bulletSpeedText` · `nowStage` · `anim` · `hpText` · `moveSpeedText`.

**`ammoText` 는 직렬화 항목 자체가 없다** = 미연결(null). 12번에서 코드만 넣고 씬 작업을 하지 않았기 때문이다.

---

## 2. 문제 1 — HUD 가 화면 밖으로 밀린다

### 2-1. 원인

`MatchWidthOrHeight = 0` 은 **너비만 보고 스케일을 정한다.**

```
scaleFactor        = 화면너비 / 1920
보이는 기준 높이   = 화면높이 / scaleFactor = 1920 / 화면비
보이는 기준 반높이 = 960 / 화면비
```

`HPBG` 는 중앙에서 y=452, 자기 높이의 절반이 10 이므로 **462 이상의 반높이**가 필요하다.

```
960 / 화면비 >= 462   ->   화면비 <= 2.08
```

| 화면비 | 보이는 반높이 | HPBG(462 필요) |
|---|---|---|
| 16:9 (1.78) | 540 | 보임 (여유 78) |
| 16:10 (1.60) | 600 | 보임 |
| 2.08 | 462 | 경계 |
| 2.29 (예: 1600x700 게임뷰) | 419 | **잘림** |
| 21:9 (2.33) | 412 | **잘림** |

즉 **창이 16:9 보다 옆으로 길어지는 순간 위쪽 HUD 부터 사라진다.** 에디터 Game 뷰를 가로로 넓게 도킹해 두면 바로 재현된다.

가로 방향은 안전하다 — `exp` 등이 x=725, 폭 350 이라 오른쪽 끝이 900 이고, 반너비는 화면비와 무관하게 항상 960 이다.

### 2-2. 해결안

| 안 | 내용 | 평가 |
|---|---|---|
| **A. 앵커 재설정 (권장)** | 체력/킬은 상단 중앙 앵커(0.5, 1), 스탯 묶음은 우측 상단 앵커(1, 1)로 바꾸고 좌표를 **가장자리 기준 오프셋**으로 다시 잡는다 | 화면비와 무관하게 항상 정위치. 근본 해결 |
| B. `MatchWidthOrHeight` 0 -> 1 | 높이 기준으로 바꾼다 | 넓은 창은 해결되지만 **세로로 긴 창에서 반대로 깨진다** (화면비 1.67 미만이면 x=900 이 잘림). 임시 방편 |
| C. 기본 해상도를 16:9 로 고정 | Game 뷰/빌드 해상도를 고정 | 문제를 감출 뿐, 빌드 후 창 크기를 바꾸면 재발 |

**A 로 진행하되**, 탄약·슬롯 UI 를 새로 배치하는 김에 같이 처리하는 것이 효율적이다.
지금 상태에서 새 UI 를 먼저 추가하면 그 UI 도 같은 이유로 화면 밖에 놓인다.

### 2-3. A 안 배치 초안

```
상단 중앙 (anchor 0.5, 1)
    HPBG / hpText        y = -40
    killCount            y = -80

우측 상단 (anchor 1, 1)
    exp / bulletPower / bulletSpeed / moveSpeed   x = -40, y = -40 부터 아래로 50 간격

하단 중앙 (anchor 0.5, 0)
    손 무기 슬롯 3칸      y = 80
    탄약 표시            y = 150

좌측 하단 (anchor 0, 0)
    서브유닛 슬롯 3칸     x = 40, y = 80
```

---

## 3. 문제 2 — HUD 수치가 옛 `Gun` 과 새 무기 사이에서 덮어써진다

`OnBulletDamageChanged` 를 두 곳이 발행한다.

| 발행처 | 시점 | 값 |
|---|---|---|
| `Gun.Start()` | 씬 시작 | 프리팹의 `bulletDamage: 2` |
| `Gun.AddDamage()` | 데미지 업그레이드 카드 | `Gun` 누적값 |
| `WeaponController.RaiseLegacyStats()` | 무기 획득·레벨업·스왑 | 활성 무기의 실제 `Stats.Damage` |

문제는 **`Gun` 이 실제 발사에 관여하지 않는다는 점**이다. `Gun.Shoot()` 을 부르는 곳은 `AutoAttack` 뿐인데 Player 프리팹에서 꺼져 있다(`m_Enabled: 0`). 실제 발사는 `WeaponController` -> `ProjectileWeapon` 이 한다.

결과적으로 데미지 카드를 먹으면 **HUD 숫자만 오르고 피해량은 그대로**다. HUD 가 거짓말을 하는 상태다.

**해결**: 업그레이드 효과를 `WeaponModifier` 기반으로 옮기고(`WeaponController.ApplyModifier`), `Gun` 컴포넌트를 프리팹에서 제거한다. 그러면 `OnBulletDamageChanged` 발행처가 `WeaponController` 하나로 정리된다.

이 작업은 UI 가 아니라 무기/업그레이드 쪽이지만, **HUD 표시의 정확성이 여기에 달려 있으므로** UI 작업과 함께 잡는 것이 맞다.

---

## 4. 구현 대상 1 — 탄약 표시

코드는 12번에서 끝났다. 남은 것은 씬 작업이다.

### 4-1. 이미 있는 것

```csharp
// PlayerStatsUI
public TextMeshProUGUI ammoText;

void UpdateAmmo(int ammo, int magazine)
{
    if (magazine <= 0) { ammoText.gameObject.SetActive(false); return; }  // 검·드론
    ammoText.gameObject.SetActive(true);
    ammoText.text = ammo <= 0 ? "Reloading..." : $"{ammo} / {magazine}";
}
```

`GameEvents.OnAmmoChanged(현재, 탄창)` 을 구독한다. 탄창 0 인 무기(근접·서브유닛)를 들면 자동으로 숨는다.

### 4-2. 할 일

1. `PlayerStateUI` 아래에 TMP 오브젝트 `ammoText` 를 만든다. 배치는 2-3 초안의 하단 중앙 y=150.
2. `PlayerStatsUI.ammoText` 슬롯에 연결한다.
3. **4개 씬 전부**에 적용한다 — `Stage1` · `Stage2` · `Stage3` · `StageBoss`.

> 씬이 4개로 갈라져 있어 같은 작업을 네 번 해야 한다. HUD 를 프리팹으로 묶으면 이후 UI 작업이 한 번으로 끝난다 — 9장 참고.

### 4-3. 표시안

| 상태 | 표시 |
|---|---|
| 일반 | `24 / 30` |
| 장전 중 | `Reloading...` |
| 탄창 없는 무기 | 숨김 |

잔탄이 적을 때(예: 20% 이하) 색을 바꾸면 체감이 좋아진다. `UpdateAmmo` 안에서 `ammoText.color` 를 바꾸면 된다.

---

## 5. 구현 대상 2 — 손 무기 슬롯 (3칸)

### 5-1. 보여줄 것

`WeaponController` 는 손 무기 3칸을 갖고 `Q` / 마우스 휠로 스왑한다. 슬롯 UI 는 이 상태를 그대로 비춘다.

> ⚠ **설계 변경으로 이 장의 전제가 흔들린다.**
> 주 무기를 시작할 때 1개만 고르고 카드로는 주 무기가 나오지 않으므로(7-1 · 7-2), **손 무기는 게임 내내 1개**다.
> 슬롯 수를 어떻게 할지는 U13 에서 정한다. 아래 내용은 3칸을 유지한다는 전제로 쓰여 있다.

| 요소 | 내용 |
|---|---|
| 칸 수 | 3 (`maxHeldSlots`) |
| 각 칸 | 무기 아이콘 + 레벨 (`Lv.3`) |
| 활성 칸 | 테두리 강조 · 확대 · 밝기 |
| 빈 칸 | 흐린 배경만 |

### 5-2. 붙을 이벤트

```csharp
GameEvents.OnWeaponSwapped       // Action<IWeapon> — 활성 무기가 바뀔 때
GameEvents.OnWeaponStatsChanged  // Action<IWeapon> — 레벨업·모디파이어 적용 시
```

두 이벤트 모두 **지금 구독자가 0명**이다. 발행은 이미 되고 있으므로 듣는 쪽만 만들면 된다.

### 5-3. ⚠ 빠져 있는 이벤트 — 목록 변경 알림

`Acquire()` 로 **새 무기가 추가될 때** 슬롯 UI 가 알 방법이 없다.

- 손 무기를 새로 얻으면 `SwapTo` 가 불려 `OnWeaponSwapped` 가 나가므로 우연히 갱신된다.
- 그러나 **서브유닛을 얻으면** 활성 무기가 바뀌지 않아 `OnWeaponStatsChanged(ActiveWeapon)` 만 나간다. 서브유닛 슬롯 UI 는 자기가 늘어난 줄 모른다.

**제안**: 목록이 바뀔 때 쏘는 이벤트를 하나 추가한다.

```csharp
// GameEvents
/// <summary>보유 무기 목록이 바뀔 때 (획득·초기화). UI 가 슬롯을 다시 그린다.</summary>
public static Action OnWeaponsChanged;
```

`WeaponController.Acquire()` 의 장착 성공 지점과 `ResetWeapons()` 에서 발행한다.
UI 는 이 이벤트를 받으면 `HeldWeapons` / `SubUnits` 를 처음부터 다시 그린다 (목록이 3개뿐이라 전체 갱신이 더 단순하고 안전하다).

### 5-4. 읽을 API

```csharp
IReadOnlyList<IWeapon> HeldWeapons   // 손 무기
int ActiveIndex                      // 현재 활성 칸 (-1 이면 없음)
IWeapon ActiveWeapon
```

> 빈 칸 개수를 그리려면 슬롯 수가 필요한데 `maxHeldSlots` / `maxSubUnitSlots` 는 현재 `private [SerializeField]` 다.
> `public int MaxHeldSlots => maxHeldSlots;` / `public int MaxSubUnitSlots => maxSubUnitSlots;` 를 추가한다.

### 5-5. ⚠ 아이콘이 없다

`WeaponData.icon` 필드는 있지만 **12종 전부 비어 있다**(`icon: {fileID: 0}`).

선택지:
1. 무기별 아이콘 스프라이트를 만들거나 구한다 (가장 좋음)
2. 아이콘이 없으면 무기 이름 텍스트로 대체한다 (임시)

UI 코드를 `icon == null` 이면 이름을 쓰도록 짜 두면, 아이콘을 나중에 채워도 코드 수정이 필요 없다.

---

## 6. 구현 대상 3 — 서브유닛 슬롯 (3칸)

손 무기 슬롯과 같은 구조지만 차이가 있다.

| 항목 | 손 무기 | 서브유닛 |
|---|---|---|
| 칸 수 | 3 | 3 |
| 활성 강조 | 필요 (1개만 활성) | **불필요** — 전부 항상 동작 |
| 탄약 | 있음 | 없음 (`MagazineSize == 0`) |
| 갱신 계기 | `OnWeaponSwapped` | `OnWeaponsChanged` (5-3 에서 추가) |

서브유닛은 스왑 개념이 없으므로 **아이콘 + 레벨만** 보여주면 된다.
쿨다운 진행도를 원형 게이지로 보여주는 것도 가능하지만, `IWeapon` 에 남은 쿨다운을 노출하는 API 가 없어 별도 작업이 필요하다.

---

## 7. 구현 대상 4 — 시작 무기 선택과 업그레이드 카드 구성

무기를 **어떻게 얻는가**를 정한다. 설계가 바뀌었다.

| | 이전 계획 | **확정된 방향** |
|---|---|---|
| 주 무기 | 업그레이드 카드로 획득 | **게임 시작 시 4종 중 1개 선택** |
| 업그레이드 카드 | 무기 + 스탯 | **스탯 증가 또는 서브유닛** 중 랜덤 3장, 1개 선택 |

### 7-1. 게임 시작 — 주 무기 선택

소총 · 기관단총 · 스나이퍼 · 검 중 하나를 고르고 시작한다.

| 무기 | 데이터 | 성격 |
|---|---|---|
| 소총 | `WD_Rifle` | 균형. 탄창 30 / 장전 2.0초 |
| 기관단총 | `WD_SMG` | 연사. 탄창 45 / 장전 1.6초 |
| 스나이퍼 | `WD_Sniper` | 관통 3. 탄창 5 / 장전 2.6초 |
| 검 | `WD_Sword` | 근접 광역. 탄창 없음 |

#### 지금 어떻게 되어 있나 (실측)

```
Loby 씬의 GameAppManager (DontDestroyOnLoad)
  -> 씬이 로드될 때 SetupPlayer()
  -> Instantiate(playerPrefab)
  -> WeaponController.Start(): if (starterWeapon != null) Acquire(starterWeapon)
```

`starterWeapon` 은 Player 프리팹에 `WD_Rifle` 로 **고정**돼 있다.
로비의 시작 버튼(`LobyManager.StartGame()`)은 `SceneManager.LoadScene("Stage1")` 만 한다.

> ⚠ **`RunManager` 와 `PlayerSpawnPoint` 는 건드리지 말 것.**
> 이름만 보면 스폰 담당 같지만 **어느 씬에도 배치돼 있지 않은 죽은 코드**다.
> 실제로 플레이어를 만드는 건 Loby 에 있는 `GameAppManager` 하나뿐이다.

#### 선택값을 어떻게 넘기나

로비에서 고른 값이 **씬을 넘어** 플레이어 생성 시점까지 살아 있어야 한다.
`GameAppManager` 가 `DontDestroyOnLoad` 싱글턴이므로 여기에 담는 것이 자연스럽다.

```csharp
// GameAppManager
[Header("Run")]
public WeaponData SelectedWeapon { get; private set; }

public void SelectWeapon(WeaponData data) => SelectedWeapon = data;
```

```csharp
// WeaponController.Start()
void Start()
{
    WeaponData starter =
        GameAppManager.Instance != null && GameAppManager.Instance.SelectedWeapon != null
            ? GameAppManager.Instance.SelectedWeapon
            : starterWeapon;                 // 폴백

    if (starter != null)
        Acquire(starter);
}
```

**폴백이 꼭 필요하다.** 에디터에서 Stage1 을 직접 Play 하면 로비를 거치지 않아 선택값이 없다.
이때 프리팹의 `starterWeapon`(소총)으로 시작해야 테스트가 끊기지 않는다.

> ⚠ **생성 직후에 무기를 갈아 끼우는 방식은 피할 것.**
> `Instantiate` 는 `Awake` 만 즉시 실행하고 `Start` 는 그 프레임 끝에 돈다.
> 생성 직후 `ResetWeapons()` 로 바꿔 놔도, 뒤늦게 돈 `Start()` 가 `starterWeapon` 을 또 획득해 버린다.
> 위처럼 **`Start()` 가 직접 선택값을 읽게** 하는 편이 안전하다.

#### 선택 UI

로비에 무기 4개 버튼을 두고, 고른 뒤 시작 버튼을 누른다.

- 각 버튼: 무기 이름 + 설명 — `WeaponData.weaponName` · `description` 에 **이미 채워져 있다**
- 선택된 버튼은 강조. 아무것도 고르지 않았으면 소총을 기본 선택해 두거나 시작 버튼을 비활성화한다
- `LobyManager.StartGame()` 에서 `GameAppManager.Instance.SelectWeapon(선택값)` 을 호출한 뒤 씬을 로드한다

### 7-2. 업그레이드 카드 구성

레벨업 시(11장) 카드 3장이 뜨고 그중 하나를 고른다. 후보는 두 갈래다.

| 갈래 | 내용 | 적용 경로 |
|---|---|---|
| 스탯 증가 | 공격력 · 이동 속도 · 최대 체력 · 회복 등 | `WeaponModifier` 또는 `PlayerStats` |
| 서브유닛 | 8종 — 드론 · 플라즈마 오브 링 · EMP 방전장 · 수리 나노봇 · 테슬라 연쇄 코일 · 나노 산성 장판 · 유도 미사일 포드 · 궤도 폭격 위성 | `WeaponController.Acquire()` |

**주 무기(소총 · SMG · 스나이퍼 · 검)는 카드에 나오지 않는다.** 시작할 때 이미 정했기 때문이다.

#### 서브유닛 카드 효과

```csharp
[CreateAssetMenu(menuName = "Upgrade/Effects/AcquireSubUnit")]
public class AcquireSubUnitUpgrade : UpgradeEffect
{
    public WeaponData subUnit;

    public override void Apply(GameObject player)
    {
        var controller = player.GetComponent<WeaponController>();
        if (controller == null) return;

        controller.Acquire(subUnit);   // 이미 있으면 레벨업된다
    }
}
```

`Acquire()` 는 이미 완성돼 있다 — 보유 중이면 레벨업, 없으면 빈 슬롯에 장착.
**호출하는 곳만 없었다.**

#### 후보 걸러내기

서브유닛 카드는 **지금 받을 수 있는 것만** 나와야 한다. `CanAcquire()` 가 정확히 이 용도로 만들어져 있다.

```
CanAcquire(data)
  보유 중이면  -> 레벨 < maxLevel 일 때만 true
  미보유면     -> 서브유닛 슬롯에 빈 칸이 있을 때만 true
```

걸러내지 않으면 슬롯 3칸이 다 찬 뒤에도 새 서브유닛 카드가 떠서, 골라도 아무 일이 일어나지 않는다.

#### 3장 뽑기

현재 `GetRandomUpgrades()` 는 풀에서 중복 없이 뽑지만 **후보가 카드 수보다 적으면 예외가 난다.**

```csharp
// 지금: pool 이 비면 Random.Range(0, 0) = 0 -> pool[0] -> 예외
for (int i = 0; i < count; i++) { ... }

// 고칠 것
for (int i = 0; i < count && pool.Count > 0; i++) { ... }
```

필터를 넣으면 풀이 줄어들 수 있으므로 **필터와 반드시 함께 고쳐야 한다.**
서브유닛을 다 모으면 후보가 스탯 카드만 남는데, 스탯 카드는 항상 뽑을 수 있게 두면 3장이 빌 일이 없다.

### 7-3. 카드에 표시할 내용

| 갈래 | 제목 | 설명 | 부가 표시 |
|---|---|---|---|
| 서브유닛 (신규) | `플라즈마 오브 링` | `플레이어 주위를 도는 플라즈마 …` | `NEW` |
| 서브유닛 (보유) | `플라즈마 오브 링` | 같은 설명 | `Lv.2 -> Lv.3` |
| 스탯 | `공격력 증가` | `공격력 +2` | — |

서브유닛 8종과 검은 `weaponName` · `description` 이 **이미 한글로 채워져 있다.** 카드에서 그대로 읽어 쓰면 된다.

#### 같은 문구를 두 번 적지 않으려면

카드는 `UpgradeData`(이름 · 설명 · 아이콘)를 읽는데, 서브유닛 설명은 `WeaponData` 에 있다.
그대로 두면 **같은 문구를 두 군데 적어야 하고**, 한쪽만 고치면 설명이 어긋난다.

`UpgradeEffect` 가 표시 문구를 스스로 답하게 만드는 편이 낫다.

```csharp
// UpgradeEffect (기반 클래스) — 기본은 UpgradeData 의 값을 그대로 쓴다
public virtual string GetTitle(UpgradeData data)       => data.upgradeName;
public virtual string GetDescription(UpgradeData data) => data.description;
public virtual Sprite GetIcon(UpgradeData data)        => data.icon;

// AcquireSubUnitUpgrade 가 재정의해 WeaponData 쪽을 쓴다
public override string GetTitle(UpgradeData data)       => subUnit.weaponName;
public override string GetDescription(UpgradeData data) => subUnit.description;
public override Sprite GetIcon(UpgradeData data)        => subUnit.icon;
```

`UpgradeCard.Setup()` 이 `data.effect.GetTitle(data)` 를 부르게 바꾼다.
스탯 카드는 기본 구현이 그대로 쓰이므로 **기존 카드 동작은 바뀌지 않는다.**

> 아이콘은 `WeaponData.icon` 이 12종 모두 비어 있다(5-5).
> 아이콘이 `null` 이면 제목을 크게 보여주도록 카드를 짜 두면, 나중에 아이콘을 채워도 코드 수정이 필요 없다.

레벨업 표시(`Lv.2 -> Lv.3`)는 `WeaponController.Find(subUnit)` 로 현재 레벨을 읽어 만든다.
보유하지 않았으면 `NEW` 를 띄운다.

### 7-4. 파급 — 손 무기 슬롯이 1칸만 찬다

주 무기를 시작할 때 하나만 고르고 카드로는 주 무기가 나오지 않으므로, **손 무기는 게임 내내 1개**다.

그 결과:

- `maxHeldSlots = 3` 중 **2칸이 항상 비어 있다**
- `Q` 키 / 마우스 휠 **스왑이 의미를 잃는다** — 바꿀 대상이 없다
- 손 무기 슬롯 UI(5장)도 1칸만 채워진 채로 남는다

슬롯을 1칸으로 줄일지, 3칸을 유지하고 나중에 주 무기 카드를 추가할지 정해야 한다 — U13.

---

## 8. 이벤트 계약 정리

UI 가 붙을 지점을 한곳에 모은다.

| 이벤트 | 시그니처 | 발행 시점 | 구독자 |
|---|---|---|---|
| `OnAmmoChanged` | `(int 현재, int 탄창)` | 발사·장전 시작/완료·스왑 | `PlayerStatsUI` |
| `OnWeaponSwapped` | `(IWeapon)` | 활성 무기 변경 | **없음** -> 손 무기 슬롯 |
| `OnWeaponStatsChanged` | `(IWeapon)` | 레벨업·모디파이어 | **없음** -> 슬롯 레벨 표시 |
| `OnWeaponsChanged` | `()` | **추가 예정** (5-3) | 손 무기 · 서브유닛 슬롯 |
| `OnBulletDamageChanged` | `(float)` | `Gun` 과 `WeaponController` **양쪽** | `PlayerStatsUI` — 3장 참고 |
| `OnOpenUpgradeUI` | `()` | 스테이지 클리어 | `UpgradeUI` |

---

## 9. 씬 4개 문제 — HUD 프리팹화

HUD 가 `Stage1` · `Stage2` · `Stage3` · `StageBoss` 에 **각각 복제돼 있다.** 지금 구조로는 이 문서의 모든 작업을 네 번 반복해야 하고, 한 곳만 빠뜨리면 그 스테이지에서만 UI 가 다르게 동작한다.

**권장**: HUD 루트(`PlayerStateUI`)를 프리팹으로 만들고 각 씬은 그 인스턴스만 둔다. 이후 UI 변경은 프리팹 한 번 수정으로 끝난다.

주의할 점:
- 씬마다 다른 참조(`nowStage` 의 `StageData`)는 프리팹에 넣을 수 없다. 프리팹 인스턴스별 오버라이드로 두거나, `StageData` 를 런타임에 찾도록 바꾼다.
- 프리팹화는 씬 4개를 모두 수정하므로 **브랜치 머지 충돌 위험**이 있다. 다른 작업자가 씬을 건드리고 있지 않은 시점에 한다.

---

## 10. 기존 HUD 표시 개선 (폰트 · 가독성)

체력처럼 중요한 정보가 잘 안 보이는 문제를 다룬다. 배치(2장)와는 별개로 **글자 자체의 크기 · 대비 · 포맷** 문제다.

### 10-1. 실측 — 지금 설정

| 요소 | 폰트 크기 | 색 | 정렬 | 외곽선 |
|---|---|---|---|---|
| `hpText` | **15** | 순백 (1,1,1,1) | 가운데 | 없음 |
| `killCount` | **36** | 순백 | 가운데 | 없음 |
| `exp` | 22 | 순백 | 왼쪽 | 없음 |
| `bulletPower` | 22 | 순백 | 왼쪽 | 없음 |
| `bulletSpeed` | 22 | 순백 | 왼쪽 | 없음 |
| `moveSpeed` | 22 | 순백 | 왼쪽 | 없음 |

체력 바(`hpBar`)는 빨강 `(1, 0, 0)` 단색, Filled 타입이다.

### 10-2. 무엇이 문제인가

**1. 가장 중요한 정보가 가장 작다.**
`hpText` 15pt 는 1920 기준 값이라 실제 화면에서는 더 작게 보인다. 킬 수(36pt)의 절반도 안 된다.

**2. 빨간 바 위의 흰 글씨.**
체력 텍스트와 체력 바가 **같은 자리(둘 다 y=452)** 에 겹쳐 있다. 흰색 위 빨강은 대비가 낮고, 체력이 줄어 바가 비면 이번엔 게임 화면 위에 그대로 뜬다 — 배경에 따라 읽힘이 달라진다.

**3. 외곽선 · 그림자가 전혀 없다.**
3D 맵 위에 바로 얹히므로 밝은 바닥에서 흰 글씨가 묻힌다.

**4. 크기 체계가 없다.**
15 / 22 / 36 세 값이 정보 중요도와 무관하게 섞여 있다.

**5. 숫자 포맷이 제각각이다.**

| 표시 | 현재 코드 | 결과 |
|---|---|---|
| 체력 | `$"{hp} / {max}"` | `73 / 100` |
| 킬 | `current + " / " + target` | `12 / 30` |
| 이동 속도 | `speed.ToString("F1")` | `5.5` |
| 공격력 | `"BulletPower : " + damage` | **`10.000001`** 처럼 나올 수 있다 |
| 탄속 | `"GunSpeed : " + speed` | 위와 같음 |

`float` 를 포맷 없이 문자열에 붙이면 소수점이 길게 나온다. 업그레이드로 값이 누적되면 실제로 발생한다.

**6. 라벨이 코드에 박혀 있다.**
`"BulletPower : "` 같은 영문 라벨이 문자열 리터럴이라, 한글로 바꾸려면 코드를 고쳐야 한다.

### 10-3. 개선안

**크기 체계 — 3단계로 정리**

| 등급 | 크기 | 대상 |
|---|---|---|
| 주요 | 28 | 체력, 탄약 |
| 보조 | 22 | 킬 수, 레벨 · EXP |
| 부가 | 18 | 공격력 · 탄속 · 이속 |

**가독성**

- TMP 의 Outline 을 0.15~0.2, 색은 검정으로 준다. 어떤 배경에서도 읽힌다.
- 또는 스탯 묶음 뒤에 반투명 검정 패널(알파 0.4)을 깐다. 외곽선보다 깔끔하다.
- 체력 텍스트는 바 **안**이 아니라 바 오른쪽 끝이나 위로 뺀다. 겹치면 어느 쪽도 잘 안 보인다.

**체력 바 색 — 구간별로**

단색 빨강 대신 비율에 따라 바꾸면 위급함이 즉시 읽힌다.

| 비율 | 색 |
|---|---|
| 60% 초과 | 녹색 |
| 30 ~ 60% | 노랑 |
| 30% 이하 | 빨강 (+ 깜빡임) |

`PlayerStatsUI.UpdateHp` 안에서 `hpBar.color` 를 바꾸면 된다.

**포맷 통일**

```csharp
hpText.text           = $"{hp} / {max}";
killText.text         = $"{current} / {target}";
bulletDamageText.text = $"공격력 {damage:F1}";
bulletSpeedText.text  = $"탄속 {speed:F0}";
moveSpeedText.text    = $"이동 {speed:F1}";
```

자릿수를 명시해 `10.000001` 같은 표시를 막는다.

**폰트**

한글 라벨을 쓰려면 **한글 글리프가 포함된 TMP Font Asset** 이 필요하다. 지금 HUD 는 영문 라벨이라 문제가 드러나지 않았을 뿐이다. 한글화를 한다면 폰트 에셋부터 확인한다.

### 10-4. 작업 범위

크기 · 색 · 외곽선은 **씬 작업**이라 4개 씬에 반복된다 — 9장의 프리팹화를 먼저 하면 한 번으로 끝난다.
포맷 문자열과 체력 바 색 전환은 `PlayerStatsUI` **코드 수정**이라 씬과 무관하다.

---

## 11. 레벨업 기반 업그레이드로 전환

### 11-1. 지금 흐름

```
적 처치 -> ExpOrb 드랍 -> 주우면 PlayerStats.AddExp(expValue)
킬 수가 StageData.killTarget 도달 -> StageManager -> OnStageClear
  -> GameManager.StageClearFlow(): 클리어 UI 3초 -> timeScale 0 -> OnOpenUpgradeUI
  -> 카드 클릭 -> UpgradeManager.TryUpgrade(): costExp 만큼 EXP 차감 -> 효과 적용
  -> Next 버튼 -> OnNextStage -> timeScale 1 -> 다음 씬
```

EXP 가 **"스테이지 클리어 시점에 쓰는 화폐"** 다. 레벨 개념이 없다.
`PlayerStats.level` 필드는 선언돼 있지만 **아무도 증가시키지 않는다.** 사실상 죽은 필드다.

### 11-2. 바꿀 흐름

```
적 처치 -> EXP 획득 -> 필요량 도달 -> 레벨업 -> 즉시 카드 3장 -> 무기 · 강화 선택
스테이지 클리어는 다음 씬으로 넘어가는 역할만
```

핵심 변화는 두 가지다.

- 업그레이드 계기: **스테이지 클리어 -> 레벨업**
- 카드 비용: **EXP 차감 -> 무료** (레벨업 자체가 대가)

### 11-3. `PlayerStats` 에 넣을 것

```csharp
public int level = 1;
public int currentExp;

[Header("Level")]
[Tooltip("1->2 에 필요한 EXP")]
[SerializeField] int baseExp = 5;

[Tooltip("레벨이 오를 때마다 필요량에 더해지는 값")]
[SerializeField] int expGrowth = 3;

/// <summary>다음 레벨까지 필요한 EXP.</summary>
public int ExpToNext => baseExp + expGrowth * (level - 1);

public event Action<int> OnLevelUp;   // 새 레벨

public void AddExp(int amount)
{
    currentExp += amount;

    // 한 번에 여러 레벨이 오를 수 있다 (한꺼번에 몰살했을 때)
    while (currentExp >= ExpToNext)
    {
        currentExp -= ExpToNext;   // 차감이 먼저다 — level 을 올리면 ExpToNext 가 바뀐다
        level++;
        OnLevelUp?.Invoke(level);
    }

    OnExpChanged?.Invoke(currentExp);
}
```

**순서가 중요하다.** `ExpToNext` 가 `level` 에 의존하므로 차감을 먼저 하고 그다음 `level++` 해야 한다. 순서를 바꾸면 필요량이 한 레벨씩 밀린다.

곡선 (base 5, growth 3, 몬스터 1마리 = EXP 1 기준):

| 레벨 | 필요 EXP | 누적 처치 수 |
|---|---|---|
| 1 -> 2 | 5 | 5 |
| 2 -> 3 | 8 | 13 |
| 3 -> 4 | 11 | 24 |
| 4 -> 5 | 14 | 38 |
| 5 -> 6 | 17 | 55 |

`StageData.killTarget` 과 비교하면 스테이지당 레벨업 횟수를 가늠할 수 있다. 스테이지당 3~5회가 목표라면 `baseExp` · `expGrowth` 를 여기서 맞춘다.

### 11-4. 레벨업 -> 카드 UI

```csharp
// GameEvents
/// <summary>레벨업 시. 업그레이드 카드를 띄운다.</summary>
public static Action<int> OnLevelUp;
```

`UpgradeUI` 가 기존 `OnOpenUpgradeUI` 와 함께 이것도 구독한다.

**주의할 점 네 가지**

1. **전투 중에 뜬다.** 스테이지 클리어와 달리 적이 살아 있는 상태에서 열리므로 `Time.timeScale = 0` 이 필수다.
   `UpgradeCard.animator` 는 이미 unscaled 모드라 정지 중에도 재생된다(코드 주석에 명시돼 있다).
   문제는 **복구**다 — 현재 `timeScale = 1` 로 되돌리는 곳은 `GameManager.LoadNextStage` 뿐이다. 레벨업 경로는 다음 스테이지로 가지 않으므로 **별도 복구 지점이 필요하다.** 빠뜨리면 카드를 고른 뒤 게임이 멈춘 채로 남는다.
2. **레벨업 중첩.** 한 번에 2레벨이 오르면 카드도 두 번 떠야 한다. `OnLevelUp` 이 연속으로 발행되므로 **큐에 쌓고 하나씩** 처리한다. 큐가 없으면 두 번째 레벨업이 첫 번째 카드를 덮어쓴다.
3. **사망과 겹칠 때.** 마지막 적을 처치하며 같이 죽는 경우, 사망 연출과 카드가 동시에 뜨지 않도록 가드가 필요하다.
4. **보스전.** 보스 연출 중 `timeScale = 0` 이 걸려도 문제가 없는지 확인한다.

### 11-5. 비용(`costExp`) 처리

`UpgradeData.costExp` 와 `UpgradeManager.TryUpgrade` 의 EXP 차감은 레벨업 방식과 충돌한다. EXP 를 레벨 진행도로 쓰면서 동시에 화폐로 빼면 **레벨이 뒤로 밀린다.**

| 안 | 내용 |
|---|---|
| **A. 카드 무료 (권장)** | 레벨업 카드는 비용 없음. `TryUpgrade` 에서 차감 제거, `costExp` 미사용 |
| B. 두 체계 병행 | 레벨업 카드는 무료, 스테이지 클리어 카드는 기존대로 EXP 소비 |
| C. 별도 화폐 도입 | EXP 는 레벨 전용, 상점용 화폐를 따로 만든다 |

A 가 가장 단순하고 뱀서류 관행에도 맞다. B 는 EXP 가 레벨 진행도이면서 화폐라 플레이어가 헷갈린다.

### 11-6. 스테이지 클리어는 어떻게 되나

레벨업이 업그레이드를 담당하면 클리어 시 카드는 역할이 겹친다.

| 안 | 내용 |
|---|---|
| **A. 클리어는 이동만 (권장)** | 클리어 UI -> Next 버튼 -> 다음 스테이지. 카드 없음 |
| B. 클리어에도 카드 유지 | 보너스 성격. 단 레벨업 카드와 구분이 안 되면 지루해진다 |
| C. 클리어 시 보너스 EXP | 클리어하면 EXP 를 얹어 준다. 간접적으로 레벨업을 유도 |

### 11-7. HUD 변경 — EXP 바가 필요하다

레벨업이 목표가 되면 **다음 레벨까지 얼마나 남았는지**가 보여야 한다. 지금은 `EXP : 12` 라는 숫자뿐이라 진행도를 알 수 없다.

- `Lv.3` 텍스트 + 진행 바 (`currentExp / ExpToNext`)
- 체력 바 바로 아래, 또는 화면 하단 전체 폭이 흔한 배치다
- `PlayerStatsUI` 에 `levelText` · `expBar` 를 추가하고 `OnLevelUp` 을 구독한다

### 11-8. 씬 간 유지 문제

플레이어는 `DontDestroyOnLoad` 라 **레벨과 EXP 가 스테이지 간 그대로 유지된다.** 의도한 동작인지 확인이 필요하다.

- 유지한다면: 후반 스테이지일수록 레벨이 높아 난이도 곡선이 완만해진다
- 리셋한다면: `PlayerStats.ResetState()` 에 레벨 · EXP 초기화를 넣는다 (현재는 사망 가드와 애니메이터만 되돌린다)

사망 후 재시작 시 레벨을 유지할지도 같은 자리에서 정한다.

---

## 12. 구현 순서

1. **시작 무기 선택** (7-1) — 로비 선택 UI, `GameAppManager.SelectedWeapon`, `WeaponController.Start()` 폴백.
   다른 작업과 독립적이고, 이것만으로도 플레이 경험이 바로 바뀐다.
2. **레벨업 체계** (11장) — `PlayerStats` 레벨업 로직, `OnLevelUp`, 레벨업 큐, `timeScale` 복구 지점.
   카드가 뜨는 **계기**가 생긴다.
3. **서브유닛 카드** (7-2 · 7-3) — `AcquireSubUnitUpgrade` 추가, `CanAcquire` 필터, 뽑기 버그 수정, 카드 표시 문구.
   카드에 담길 **내용물**이 생긴다.

   > 2 와 3 이 모두 끝나야 "레벨업하면 서브유닛을 얻는다"가 성립한다. 둘 중 하나만으로는 체감되는 변화가 없다.

4. **업그레이드를 `WeaponModifier` 로 이전 + `Gun` 제거** (3장).
   HUD 수치가 정확해진다.
5. **HUD 앵커 재배치** (2장 A안) + **HUD 프리팹화** (9장).
   이후 UI 추가가 한 번으로 끝난다.
6. **폰트 · 포맷 · 체력 바 색 정리** (10장).
7. **탄약 표시 연결** (4장) + **레벨 · EXP 바 추가** (11-7).
8. **손 무기 슬롯** (5장) — U13 결정 후. `OnWeaponsChanged` 추가, `MaxHeldSlots` 공개.
9. **서브유닛 슬롯** (6장).
10. 아이콘 채우기 · 잔탄 경고색 등 다듬기.

1~4 는 스크립트 작업, 5~9 는 씬 · 프리팹 작업이 섞인다.
5(프리팹화)를 6~9 보다 먼저 두는 이유는, 그러지 않으면 같은 UI 작업을 씬 4개에 네 번 반복해야 하기 때문이다.

---

## 13. 테스트 체크리스트

- [ ] 게임 뷰를 가로로 길게(2.3:1) 늘여도 체력 바와 킬 수가 보인다
- [ ] 세로로 길게(4:3) 줄여도 우측 스탯이 잘리지 않는다
- [ ] 소총으로 쏘면 탄약이 줄고, 0 이 되면 `Reloading...` 이 뜬다
- [ ] 검을 들면 탄약 표시가 사라진다
- [ ] `Q` 로 무기를 바꾸면 활성 슬롯 강조가 따라 움직인다
- [ ] 탄창이 빈 무기로 다시 스왑하면 빈 상태 그대로 표시된다 (12번 R4)
- [ ] 카드로 서브유닛을 얻으면 서브유닛 슬롯이 즉시 갱신된다 (`OnWeaponsChanged` 확인)
- [ ] 데미지 업그레이드 후 **실제 피해량**이 오른다 (HUD 숫자만 오르는 게 아니라)
- [ ] 4개 스테이지 씬 전부에서 동일하게 동작한다

**시작 무기 선택 · 카드 (7장)**

- [ ] 로비에서 4종을 각각 골라 시작하면 그 무기를 들고 시작한다
- [ ] 검을 고르면 근접 모션이 나오고 탄약 표시가 숨는다
- [ ] **Stage1 을 에디터에서 직접 Play 해도 소총으로 정상 시작한다** (폴백 — 로비를 거치지 않는 경로)
- [ ] 레벨업 카드 3장이 서로 중복되지 않는다
- [ ] 카드에 서브유닛 이름과 설명이 나온다
- [ ] 이미 가진 서브유닛은 `Lv.2 -> Lv.3`, 새 것은 `NEW` 로 구분된다
- [ ] 서브유닛 슬롯 3칸이 다 차면 새 서브유닛 카드가 후보에서 빠진다
- [ ] 최대 레벨에 도달한 서브유닛도 후보에서 빠진다
- [ ] 후보가 3개 미만이어도 예외 없이 카드가 뜬다
- [ ] 주 무기(소총 · SMG · 스나이퍼 · 검)는 카드에 나오지 않는다

**폰트 · 표시 (10장)**

- [ ] 밝은 바닥 위에서도 체력·킬 수가 읽힌다
- [ ] 체력이 줄어 바가 비어도 체력 숫자가 읽힌다
- [ ] 공격력·탄속에 소수점이 길게 나오지 않는다 (업그레이드 여러 번 먹은 뒤 확인)
- [ ] 체력 비율에 따라 바 색이 바뀐다

**레벨업 (11장)**

- [ ] 필요 EXP 를 채우면 레벨이 오르고 카드가 뜬다
- [ ] 카드를 고른 뒤 **게임이 정상 속도로 돌아온다** (`timeScale` 복구 — 가장 빠뜨리기 쉬운 부분)
- [ ] 한 번에 2레벨이 오르면 카드가 두 번 뜬다
- [ ] 레벨업과 동시에 사망해도 카드와 사망 연출이 겹치지 않는다
- [ ] 보스전 중 레벨업해도 보스 연출이 깨지지 않는다
- [ ] EXP 바가 `currentExp / ExpToNext` 를 정확히 반영한다
- [ ] 스테이지를 넘어가도 레벨·EXP 가 의도한 대로 유지(또는 리셋)된다

---

## 14. 결정해야 할 것

| # | 항목 | 선택지 | 권장 |
|---|---|---|---|
| U1 | HUD 배치 | A 앵커 재설정 / B Match 0->1 / C 해상도 고정 | **A**. B·C 는 조건이 바뀌면 재발한다 |
| U2 | HUD 프리팹화 | 지금 한다 / 나중에 | **지금**. 미룰수록 네 번 반복하는 작업이 쌓인다 |
| U3 | 무기 아이콘 | 스프라이트 제작 / 이름 텍스트로 임시 | 임시로 시작하되 코드는 둘 다 지원 |
| U4 | 슬롯 위치 | 하단 중앙(손) + 좌하단(서브유닛) / 한곳에 모음 | 분리. 성격이 다르다 (스왑 대상 vs 항상 동작) |
| U5 | 서브유닛 쿨다운 게이지 | 표시 / 생략 | 생략. `IWeapon` 에 API 추가가 필요해 비용 대비 효과가 낮다 |
| U6 | 슬롯 클릭으로 스왑 | 지원 / 키보드만 | 키보드만. 뱀서류는 조작이 단순한 편이 낫다 |
| U7 | 카드 비용 | A 무료 / B 병행 / C 별도 화폐 | **A**. EXP 가 진행도이면서 화폐면 레벨이 뒤로 밀린다 (11-5) |
| U8 | 스테이지 클리어 보상 | A 이동만 / B 카드 유지 / C 보너스 EXP | **A**. 레벨업 카드와 역할이 겹친다 (11-6) |
| U9 | 레벨 곡선 | `baseExp` · `expGrowth` 값 | 5 / 3 으로 시작해 `killTarget` 과 맞춰 조정 (11-3) |
| U10 | 씬 간 레벨 유지 | 유지 / 스테이지마다 리셋 | 유지. 단 후반 난이도 곡선을 함께 봐야 한다 (11-8) |
| U11 | 사망 후 레벨 | 유지 / 리셋 | 런 단위 로그라이크면 리셋이 자연스럽다 |
| U12 | 한글 라벨 | 적용 / 영문 유지 | 적용한다면 한글 글리프가 든 TMP 폰트 에셋이 먼저 필요하다 (10-3) |
| U13 | 손 무기 슬롯 수 | 1칸으로 축소 / 3칸 유지 | 주 무기가 1개뿐이면 스왑이 무의미해진다. **1칸 축소**가 솔직하다. 3칸을 유지하려면 주 무기를 늘릴 다른 경로가 필요하다 (7-4) |
| U14 | 시작 무기 미선택 시 | 소총 기본 선택 / 시작 버튼 비활성 | **기본 선택**. 버튼을 막으면 처음 하는 사람이 멈춘다 (7-1) |
| U15 | 카드 구성 비율 | 스탯 · 서브유닛 완전 랜덤 / 비율 고정 | 초반에 서브유닛이 잘 나오게 가중치를 주면 빌드가 빨리 잡힌다. 수치는 플레이로 조정 |

---

## 15. 구현 결과 (2026-09-19)

구현 순서(12장)의 **1~3단계**를 끝냈다. 결정된 값은 아래와 같다.

| # | 결정 | 적용 |
|---|---|---|
| U13 | 손 무기 슬롯 | **2칸** (`maxHeldSlots`, 프리팹 포함) |
| U14 | 시작 무기 미선택 시 | **소총 기본 선택** (`LobyManager.Start()`) |
| U7 | 카드 비용 | **무료** — 차감 로직 제거 + 기존 에셋 `costExp` 0 |
| U8 | 스테이지 클리어 | **이동만** — 클리어 시 카드를 띄우지 않는다 |

### 15-1. 수정한 스크립트

| 파일 | 내용 |
|---|---|
| `Core/GameEvents.cs` | `OnWeaponsChanged` · `OnLevelUp` 추가 |
| `Core/GameAppManager.cs` | `SelectedWeapon` · `SelectWeapon()` — 씬을 넘어 선택을 전달 |
| `Core/LobyManager.cs` | `selectableWeapons` · `SelectWeapon(int)` · `OnSelectionChanged`, 시작 시 첫 무기 기본 선택 |
| `Core/LobyWeaponSelectUI.cs` | **신규** — 선택 강조와 이름 · 설명 표시 |
| `Core/GameManager.cs` | 클리어 시 카드 대신 바로 다음 스테이지 |
| `Player/WeaponController.cs` | 슬롯 2칸, `MaxHeldSlots` · `MaxSubUnitSlots` 공개, `OnWeaponsChanged` 발행, `Start()` 가 선택값을 읽고 없으면 폴백 |
| `Player/PlayerStats.cs` | `baseExp` · `expGrowth` · `ExpToNext` · `OnLevelChanged`, `AddExp` 가 레벨업 처리 |
| `Upgrade/UpgradeEffect.cs` | `GetTitle` · `GetDescription` · `GetIcon` 가상 메서드 |
| `Upgrade/Effects/AcquireSubUnitUpgrade.cs` | **신규** — 서브유닛 지급 + `WeaponData` 문구 사용 |
| `Upgrade/UpgradeManager.cs` | `CanAcquire` 후보 필터, 뽑기 예외 수정, EXP 차감 제거 |
| `Upgrade/UpgradeUI.cs` | 레벨업 큐, `timeScale` 정지 · **복구**, 후보 부족 시 남는 카드 숨김 |
| `Upgrade/UpgradeCard.cs` | 효과에서 문구를 받아 표시, 비용 0 이면 숨김 |

### 15-2. 생성한 에셋 · 씬 변경

- `Assets/Scripts/Upgrade/Data/SubUnits/` 에 서브유닛 카드 **8종** (`UP_*` + `EF_*`)
- `Stage1` · `Stage2` · `Stage3` · `StageBoss` 의 `upgradePool` → 각 **12개** (스탯 4 + 서브유닛 8)
- `Loby` 에 `WeaponSelect` (버튼 4개 + 설명), `LobyManager.selectableWeapons` 연결
- 기존 업그레이드 7종의 `costExp` → 0

도구: `Tools/Upgrade Card Setup/전체 실행` · `Tools/Loby Setup/주 무기 선택 UI 만들기` (둘 다 멱등)

### 15-3. 검증

- 고정 버전 **6000.3.7f1** 로 컴파일 — `error CS` 0건, 정상 종료
- 로그가 아니라 씬 파일에서 확인 — 버튼 4개가 `SelectWeapon` 에 인자 0 · 1 · 2 · 3 으로 연결됐고,
  `selectableWeapons` 의 GUID 가 소총 · SMG · 스나이퍼 · 검과 순서까지 일치
- 인게임 플레이 확인은 사용자가 수행 (레벨업 카드 · 클리어 이동 정상)

### 15-4. 폰트 문제 — 원인은 머티리얼이었다

플레이 중 두 가지 증상이 보고됐다. **같은 원인의 두 얼굴**이었다.

| 증상 | 위치 |
|---|---|
| 글자가 빈 상자(□)로 나온다 | 로비 |
| 글자가 뭉개지고 **글자마다 하얀 뒷배경**이 보인다 | HUD · 업그레이드 카드 |

**빈 상자**는 새로 만든 로비 텍스트가 TMP 기본 폰트(LiberationSans)를 쓴 탓이었다. 한글 글리프가 없다.
`Assets/Font/RiaSans-Bold SDF` 를 지정해 해결했다. **스크립트로 TMP 텍스트를 만들 때는 폰트를 반드시 명시할 것.**

**하얀 뒷배경**의 진짜 원인은 **폰트 기본 머티리얼의 `_FaceColor` 가 2.67** 이었다는 것이다.
TMP 기본값은 1.0 인데 HDR 과노출 값이 들어가 있었고, 프로젝트의 URP 볼륨 프로파일 두 곳에서
**Bloom 이 켜져 있어**(`active: 1`) 글자마다 흰 빛이 번졌다.
여기에 `_OutlineWidth: 0.44` (패딩 4 · `_GradientScale 5` 대비 과도)가 겹쳐 윤곽까지 뭉갰다.

| 값 | 전 | 후 |
|---|---|---|
| 기본 머티리얼 `_FaceColor` | 2.67 | 1.0 |
| 기본 머티리얼 `_OutlineWidth` | 0.44 | 0 |
| HUD 외곽선 머티리얼 | (없음) | `_FaceColor` 1.0 + 검은 외곽선 0.12 |

크기도 10-3 의 체계대로 조정했다 — 체력 15 → **28**, 킬 36 → 34, 스탯 22 → 24.
체력 수치가 가장 중요한데 가장 작았다.

> ⚠ 이 값은 **제가 만든 머티리얼이 아니라 폰트의 기본 머티리얼**에 원래 있던 것이다.
> 그래서 손대지 않은 업그레이드 카드에서도 같은 증상이 나왔다.
> 기본 머티리얼은 카드 · 보스 UI 등도 함께 쓰므로, 과노출이 의도한 발광 연출이었다면
> 기본값을 되돌리고 **타이틀 전용 머티리얼을 분리**해야 한다.

**진단 중 틀렸던 가설 두 가지** (같은 길로 다시 가지 않기 위해 남긴다)

- *아틀라스 과부하* — 4096² 에 글리프 11,621 개는 물리적으로 안 맞지만, 실제로 구워진 건 3,228 개이고
  우리가 쓰는 글자(소총 · 공격력 · 탄속 …)는 모두 정상 좌표를 갖고 있었다.
- *머티리얼에 아틀라스 누락* — `_MainTex` 가 비면 SDF 가 전부 흰색으로 읽혀 딱 그 증상이 나오지만,
  확인해 보니 제대로 물려 있었다.

### 15-5. 옛 `Gun` 제거와 업그레이드 이전 (3장)

데미지 카드를 먹어도 **HUD 숫자만 오르고 실제 피해량은 그대로**이던 문제를 고쳤다.

| 파일 | 내용 |
|---|---|
| `Upgrade/Effects/DamageUpgrade.cs` | `Gun.bulletDamage` → `WeaponModifier.damageAdd` |
| `Upgrade/Effects/GunSpeedUp.cs` | `Gun.bulletSpeed` → `WeaponModifier.projectileSpeedAdd` |
| `Player/WeaponController.cs` | 범용 강화 누적분(`globalModifier`) 보관 |
| `Player/PlayerStatsUI.cs` | `Gun` 참조 제거, 활성 무기 스탯을 직접 읽음 |
| `Editor/LegacyGunCleanup.cs` | **신규** — 프리팹에서 컴포넌트 제거 |

**함께 고친 구조적 허점**: `ApplyGlobalModifier` 는 *지금 보유한* 무기에만 적용됐다.
서브유닛을 카드로 계속 얻는 구조에서는 "공격력 강화를 먼저 먹고 나중에 얻은 서브유닛"만 약해진다.
누적분을 보관해 `Acquire` 시 새 무기에도 얹도록 했다.

`Player.prefab` 에서 `Gun`(AssaultRifle 하위) 과 `AutoAttack`(Player) 컴포넌트를 제거했다.
**스크립트 파일은 남겨 뒀다** — `WeaponController` 의 이중 발사 경고와 `WeaponAssetSetup` 이
`AutoAttack` 타입을 참조하고, `Assets/_Recovery` 의 백업 씬 3개도 두 스크립트를 물고 있다.
파일까지 지우려면 그 참조들을 먼저 정리해야 한다.

### 15-6. HUD 마무리 (2026-09-19)

12장의 남은 항목을 끝냈다. 전부 `Tools/HUD Layout/` 메뉴로 4개 씬에 일괄 적용했다(멱등).

| 항목 | 내용 |
|---|---|
| 앵커 재배치 | 부모 `PlayerStateUI` 를 캔버스 전체로 늘리고 자식을 가장자리에 앵커. **이게 핵심** — 부모가 100x100 중앙 박스여서 자식 앵커가 의미를 갖지 못했다 |
| 탄약 · 레벨 · EXP 바 | `ammoText` · `levelText` · `expBar` 생성 후 `PlayerStatsUI` 에 연결 |
| 슬롯 UI | 손 무기 2칸(하단 중앙, 활성 강조) · 서브유닛 3칸(좌측 하단) |
| 체력 바 색 | 60% 초과 녹색 / 30~60% 노랑 / 30% 이하 빨강 |
| 무기 아이콘 | 12종에 GUI 킷 픽토 아이콘 할당 — 슬롯과 업그레이드 카드에 함께 나온다 |
| 총알 모양 표시 | 프로젝트에 총알 아이콘이 없어 **탄두 + 몸통 모양을 코드로 그려 생성**(`Assets/Sprites/UI/BulletPip.png`) |

**총알 칸 설계**: 칸 수를 10으로 고정하고 비율로 채운다.
탄창이 10발 이하인 스나이퍼(5)는 한 칸 = 한 발이 되고, 소총(30) · 기관단총(45)은 한 칸이 여러 발을 대표한다.
45개를 그리면 읽히지 않기 때문이다.

**프리팹화는 하지 않았다.** 목적이 "같은 UI 작업을 4번 반복하지 않기"였는데,
에디터 스크립트가 4개 씬을 순회하므로 이미 확보된다. 반면 씬 구조 변경 · 씬마다 다른
`nowStage` 참조 · 머지 충돌 위험이 남아 이득 대비 위험이 맞지 않는다.

### 15-7. 남은 일

- **사운드** — 보류. [14-사운드보류.md](14-사운드보류.md) 에 현황과 붙일 지점을 정리해 뒀다
- 슬롯 위치 · 크기 등 배치 다듬기 (플레이해 보고 조정)
- 우측 스탯 패널 `BG` 높이 — 줄 간격을 40 으로 바꿨는데 배경은 원래 크기 그대로다

### 15-5. ⚠ 에디터 스크립트 함정

`EditorSceneManager.OpenScene(Single)` 은 **참조되지 않은 에셋을 언로드한다.**
씬을 열기 전에 `LoadAssetAtPath` 로 받아 둔 참조는 그 순간 무효가 되어
`MissingReferenceException: The object of type 'X' has been destroyed` 가 난다.

**씬을 먼저 열고, 그다음에 에셋을 로드할 것.** 로비 UI 생성 스크립트가 이것 때문에 한 번 중단됐다
(씬 저장 전이라 변경은 남지 않았다).
