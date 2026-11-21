# 모델 에셋 출처 · 라이선스

프로젝트에 들어온 3D 모델 · 애니메이션 · 입자 그림의 출처를 기록한다. 소리는 [AUDIO_LICENSES.md](AUDIO_LICENSES.md) 에 따로 있다.

> **왜 적어 두는가**
> 무료 팩이라도 라이선스가 제각각이다. 정식 라이선스 문구가 없는 팩은 특히
> **어디서 · 누가 · 무엇을 허락했는지**를 남겨 두지 않으면 나중에 확인할 방법이 없다.
> **새 모델을 넣을 때마다 이 표에 한 줄 추가할 것.**

## 확인된 것

| 폴더 | 팩 | 출처 | 라이선스 | 표기 의무 | 받은 날 |
|---|---|---|---|---|---|
| `FuturaWeapons` | Futura Weapon Pack (10종 84개 중 11개 사용) | [moisturizedfish.itch.io](https://moisturizedfish.itch.io/futura-weapon-pack) | ⚠ **정식 문구 없음** — 아래 참고 | 권장 | 2026-09-14 · 09-26 · 10-04 · 10-11 |
| `KayKit/Character Animations 1.1` | KayKit Character Animations 1.1 — Free (근접 모션 FBX 1개, 22개 중 8구간 사용) | [kaylousberg.itch.io](https://kaylousberg.itch.io/kaykit-character-animations) | **CC0 1.0** (폴더의 `License.txt`) | 없음 (권장) | 2026-09-26 |
| `Arts/FxKit/Textures` | Kenney Particle Pack 1.1 — 입자 그림 80장 중 48장을 채널별 묶음(2×2 등)으로 짠 18장 (`FxKit_Scorch` · `FxKit_Spark` 두 장은 직접 만든 것). 연출 공통 재료(FxKit)가 쓴다. 다시 만들기: `Logs/fxkit/tools/make_kenney_sheets.ps1` | [kenney.nl](https://kenney.nl/assets/particle-pack) | **CC0 1.0** (팩의 `License.txt`) | 없음 (권장) | 2026-11-20 |
| `Arts/MeleeWeapons/Hammer` | "SCI-FI Hammer" — Qubzy (FBX 1개). 전투 망치 (`W_Hammer`) · 묠니르 (`W_EVO_Mjolnir`, 같은 모델을 하늘빛으로 다시 칠함). 재질은 `Arts/MeleeWeapons/Materials` 에서 따로 만든다 | [poly.pizza/m/MsNEnPrE69](https://poly.pizza/m/MsNEnPrE69) | **CC BY 3.0** | **있음** — 크레딧에 넣음 | 2026-11-20 |
| `Arts/MeleeWeapons/Baton` | "Security Baton" — Dylan Kowertz (GLB 를 OBJ + 텍스처 3장으로 바꿔 넣음 — 프로젝트에 glTF 가져오기가 없어서). 방망이 (`W_Bat`) | [sketchfab.com/3d-models/security-baton-412e3c2828d1434f972e250099f9d74d](https://sketchfab.com/3d-models/security-baton-412e3c2828d1434f972e250099f9d74d) | **CC BY 4.0** | **있음** — 크레딧에 넣음 | 2026-11-20 |
| `Arts/MeleeWeapons/Tetsubo` | "Low-poly Tetsubo" — Yavuz Temel (FBX 1개, 614면). 도깨비 방망이 (`W_EVO_Dokkaebi`, 징을 연두 도깨비불색으로 다시 칠함) | [sketchfab.com/3d-models/low-poly-tetsubo-58e81d5de7b9475ba99140a38cf23ccf](https://sketchfab.com/3d-models/low-poly-tetsubo-58e81d5de7b9475ba99140a38cf23ccf) | **CC BY 4.0** (Sketchfab 'CC Attribution') | **있음** — 크레딧에 넣음 | 2026-11-20 |

**Futura Weapon Pack** — 무료 팩이고, 페이지에 라이선스 문구가 없다.
상업적 이용을 묻는 댓글에 제작자가 **"Sure, go for it!"** 이라고 답한 것이 허락의 전부다.
(2026-09-26 확인. 페이지가 바뀌거나 댓글이 지워질 수 있으니, 출시 전 한 번 더 확인하고 가능하면 캡처해 둘 것.)

| 파일 | 원본 zip 경로 | 쓰는 곳 |
|---|---|---|
| `Models/Sword_A_Orange.fbx` | `Futura Weapons/Sword/Style A/orange.fbx` | 검 (`W_Sword`) |
| `Models/Sword_A_Red.fbx` | `Futura Weapons/Sword/Style A/red.fbx` | 대검 (`W_Greatsword`) |
| `Models/Halberd_Blue.fbx` | `Futura Weapons/Halberd/blue.fbx` | 창 (`W_Spear`) |
| `Models/Fist_A_Blue.fbx` | `Futura Weapons/Fists/Style A/blue.fbx` | 그랩 훅 갈고리 머리 · 그림 (`SA_Grapple`) — 2026-10-04 |
| `Models/Shield_A_BlueWhite.fbx` | `Futura Weapons/Shield/Style A/blue-white.fbx` | 에너지 방패 그림 (`SA_Shield`) — 2026-10-04 |
| `Models/Sword_B_Blue.fbx` | `Futura Weapons/Sword/Style B/blue.fbx` | 상위 무기 거울칼날 (`W_EVO_MirrorEdge`) — 2026-10-08 |
| `Models/Sword_B_Purple.fbx` | `Futura Weapons/Sword/Style B/purple.fbx` | 상위 무기 중력 대검 (`W_EVO_GravityCleaver`) — 2026-10-08 |
| `Models/Sword_B_Green.fbx` | `Futura Weapons/Sword/Style B/green.fbx` | 상위 무기 사슬검 (`W_EVO_ChainBlade`) — 2026-10-11 |
| `Models/Sword_B_Yellow.fbx` | `Futura Weapons/Sword/Style B/yellow.fbx` | 상위 무기 성채 대검 (`W_EVO_BastionCleaver`) — 2026-10-11 |
| `Models/Staff_Blue.fbx` | `Futura Weapons/Staff/blue.fbx` | 상위 무기 천공창 (`W_EVO_PiercingLance`) — 2026-10-11 |
| `Models/Halberd_Red.fbx` | `Futura Weapons/Halberd/red.fbx` | 상위 무기 팔랑크스 (`W_EVO_Phalanx`) — 2026-10-11 |

**KayKit Character Animations** — 공공 영역 기증(CC0)이라 표기 의무는 없다 (제작자는 "Kay Lousberg, www.kaylousberg.com" 표기를 권함).
`Animations/fbx/Rig_Medium/Rig_Medium_CombatMelee.fbx` 에 근접 모션 22개가 들어 있고, `CharacterSetup` 이 세 구간만
Humanoid 클립으로 잘라 근접 양손 모션에 쓴다 — `KK_2H_Chop`(내려찍기) · `KK_2H_Slice`(대검 가로베기) · `KK_2H_Stab`(창 찌르기).
패링(`KK_1H_Parry` · `KK_Guard_Brace`)과 서브 능력(`KK_Shield_Hold` 방패 들기 · `KK_Shield_Bash` 방패 밀치기 · `KK_Hook_Throw` 와이어 발사, 2026-10-04)도 같은 파일에서 자른다.

게임 크레딧 예시:

```
Melee Weapon Models
  Futura Weapon Pack — moisturizedfish (itch.io)
  SCI-FI Hammer — Qubzy (poly.pizza, CC BY 3.0)
  Security Baton — Dylan Kowertz (sketchfab.com, CC BY 4.0)
  Low-poly Tetsubo — Yavuz Temel (sketchfab.com, CC BY 4.0)
Melee Animations
  KayKit Character Animations — Kay Lousberg (CC0)
```

## 확인 필요

아래 폴더는 출처 기록이 없었다. **2026-11-22 조사** — 팩 안 파일과 웹에서 찾은 것. "추정"은 받은 사람이 확인할 것.

| 폴더 | 쓰는 곳 | 출처 | 라이선스 | 상태 |
|---|---|---|---|---|
| `Kevin Iglesias` | 플레이어 근접 · 구르기 애니메이션 | Kevin Iglesias — Human Melee Animations 2.0 FREE ([keviniglesias.com](https://www.keviniglesias.com/#license)) | **Standard Unity Asset Store EULA · Fab Standard License** — 상업 이용 가능 · 재판매 금지 · **출처 표기 불필요** (팩 안 `Human Melee Animations 2.0 FREE.pdf`) | ✅ 확인 |
| `SciFiWarriorPBRHPPolyart` | 플레이어 모델 · 몸 머티리얼 (HP / Polyart) | Unity Asset Store 무료 에셋 [#106154](https://assetstore.unity.com/packages/3d/characters/humanoids/sci-fi-warrior-pbr-hp-106154) "Sci-Fi Warrior PBR HP" (지금 페이지 이름은 "Robot Hero : PBR HP Polyart" 로 보임) | Standard Unity Asset Store EULA (스토어 무료 에셋 공통) | 추정 — 제작자 이름 미확인 |
| `Space_Exploration_GUI_Kit` | HUD 아이콘 (패링 방패 아이콘 포함) | 같은 이름의 스토어 페이지를 찾지 못함 — 가장 가까운 후보 Unity Asset Store [Space Game GUI Kit #298577](https://assetstore.unity.com/packages/space-game-gui-kit/298577) (무료, 상점 · 아이콘 포함) | 스토어 것이면 Standard EULA · 안의 글꼴 Asap · Righteous 는 **SIL OFL 1.1** (팩 안 `Fonts/OFL.txt` — 확인) | 미확인 — 팀원(가져온 사람)에게 |
| `GunPack` | 총기 모델 · 부착물 그림(`Parts/Scope_1` · `Barrel_Single` → 2배율 스코프 · 확장 총열 아이콘) | 찾지 못함 (파일 이름 AR_3 · AR_6 · SMG_1 · Grenade 로 검색해도 일치하는 팩 없음) | ? | 미확인 — 받은 사람에게 (9-14 커밋 "추가 총기, 드론시스템") |
| `LowPolyWeapons_LITE` | **안 씀** — 폴더 밖에서 참조 0 (11-22 확인). 지워도 된다 | 찾지 못함 | ? | 지우면 기록도 필요 없음 |

> Unity Asset Store 에서 받은 팩이라면 Standard Unity Asset Store EULA 를 따르며,
> **좌석(seat) 단위** 라이선스라 팀원이 각자 받아야 하는 경우가 있다. 팩마다 확인할 것.
