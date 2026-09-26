# 모델 에셋 출처 · 라이선스

프로젝트에 들어온 3D 모델 · 애니메이션의 출처를 기록한다. 소리는 [AUDIO_LICENSES.md](AUDIO_LICENSES.md) 에 따로 있다.

> **왜 적어 두는가**
> 무료 팩이라도 라이선스가 제각각이다. 정식 라이선스 문구가 없는 팩은 특히
> **어디서 · 누가 · 무엇을 허락했는지**를 남겨 두지 않으면 나중에 확인할 방법이 없다.
> **새 모델을 넣을 때마다 이 표에 한 줄 추가할 것.**

## 확인된 것

| 폴더 | 팩 | 출처 | 라이선스 | 표기 의무 | 받은 날 |
|---|---|---|---|---|---|
| `FuturaWeapons` | Futura Weapon Pack (10종 84개 중 3개 사용) | [moisturizedfish.itch.io](https://moisturizedfish.itch.io/futura-weapon-pack) | ⚠ **정식 문구 없음** — 아래 참고 | 권장 | 2026-09-14 · 09-26 |
| `KayKit/Character Animations 1.1` | KayKit Character Animations 1.1 — Free (근접 모션 FBX 1개, 22개 중 3구간 사용) | [kaylousberg.itch.io](https://kaylousberg.itch.io/kaykit-character-animations) | **CC0 1.0** (폴더의 `License.txt`) | 없음 (권장) | 2026-09-26 |

**Futura Weapon Pack** — 무료 팩이고, 페이지에 라이선스 문구가 없다.
상업적 이용을 묻는 댓글에 제작자가 **"Sure, go for it!"** 이라고 답한 것이 허락의 전부다.
(2026-09-26 확인. 페이지가 바뀌거나 댓글이 지워질 수 있으니, 출시 전 한 번 더 확인하고 가능하면 캡처해 둘 것.)

| 파일 | 원본 zip 경로 | 쓰는 곳 |
|---|---|---|
| `Models/Sword_A_Orange.fbx` | `Futura Weapons/Sword/Style A/orange.fbx` | 검 (`W_Sword`) |
| `Models/Sword_A_Red.fbx` | `Futura Weapons/Sword/Style A/red.fbx` | 대검 (`W_Greatsword`) |
| `Models/Halberd_Blue.fbx` | `Futura Weapons/Halberd/blue.fbx` | 창 (`W_Spear`) |

**KayKit Character Animations** — 공공 영역 기증(CC0)이라 표기 의무는 없다 (제작자는 "Kay Lousberg, www.kaylousberg.com" 표기를 권함).
`Animations/fbx/Rig_Medium/Rig_Medium_CombatMelee.fbx` 에 근접 모션 22개가 들어 있고, `CharacterSetup` 이 세 구간만
Humanoid 클립으로 잘라 근접 양손 모션에 쓴다 — `KK_2H_Chop`(내려찍기) · `KK_2H_Slice`(대검 가로베기) · `KK_2H_Stab`(창 찌르기).

게임 크레딧 예시:

```
Melee Weapon Models
  Futura Weapon Pack — moisturizedfish (itch.io)
Melee Animations
  KayKit Character Animations — Kay Lousberg (CC0)
```

## 확인 필요

아래 폴더는 출처 기록이 없다. 받은 사람이 채워 넣을 것.

| 폴더 | 쓰는 곳 | 출처 | 라이선스 |
|---|---|---|---|
| `SciFiWarriorPBRHPPolyart` | 플레이어 모델 · 몸 머티리얼 (HP / Polyart) | ? | ? |
| `Kevin Iglesias` | 플레이어 근접 · 구르기 애니메이션 | ? | ? |
| `GunPack` | 총기 모델 | ? | ? |
| `LowPolyWeapons_LITE` | (지금은 안 씀) | ? | ? |
| `Space_Exploration_GUI_Kit` | HUD 아이콘 (패링 방패 아이콘 포함) | ? | ? |

> Unity Asset Store 에서 받은 팩이라면 Standard Unity Asset Store EULA 를 따르며,
> **좌석(seat) 단위** 라이선스라 팀원이 각자 받아야 하는 경우가 있다. 팩마다 확인할 것.
