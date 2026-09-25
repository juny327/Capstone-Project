# 오디오 에셋 출처 · 라이선스

프로젝트에 들어온 소리 파일의 출처를 기록한다.

> **왜 적어 두는가**
> 파일이 20개만 넘어가도 어디서 받았는지 기억나지 않는다.
> 출처 표기가 필요한 라이선스(CC-BY)나 상업 이용이 막힌 것(CC-BY-NC)이 섞이면
> 나중에 전부 다시 찾아야 한다. **새 소리를 넣을 때마다 이 표에 한 줄 추가할 것.**

| 폴더 | 팩 | 출처 | 라이선스 | 표기 의무 | 받은 날 |
|---|---|---|---|---|---|
| `Kenney/ImpactSounds` | Impact Sounds (130) | [kenney.nl](https://kenney.nl/assets/impact-sounds) | **CC0** | 없음 | 2026-09-25 |
| `Kenney/InterfaceSounds` | Interface Sounds (100) | [kenney.nl](https://kenney.nl/assets/interface-sounds) | **CC0** | 없음 | 2026-09-25 |
| `Kenney/SciFiSounds` | Sci-fi Sounds (73) | [kenney.nl](https://kenney.nl/assets/sci-fi-sounds) | **CC0** | 없음 | 2026-09-25 |
| `Kenney/RPGAudio` | RPG Audio (51) | [kenney.nl](https://kenney.nl/assets/rpg-audio) | **CC0** | 없음 | 2026-09-25 |
| `SnakeF8/GunSounds` | Snake's Authentic Gun Sounds (71 wav) | [f8studios.itch.io](https://f8studios.itch.io/snakes-authentic-gun-sounds) | **CC0** (퍼블릭 도메인) | 없음 | 2026-09-25 |
| `SnakeF8/GunSounds2` | Snake's SECOND Authentic Gun Sounds (84 wav) | [f8studios.itch.io](https://f8studios.itch.io/snakes-second-authentic-gun-sounds-pack) | **CC0** (퍼블릭 도메인) | 없음 | 2026-09-25 |
| `Abstraction/MusicLoops` | Music Loop Bundle 2026 Q2 (29곡) | [tallbeard.itch.io](https://tallbeard.itch.io/music-loop-bundle) | **CC0** | 없음 | 2026-09-25 |
| `OpenGameArt/Electricity` | Electricity Sound Effects (`continuousspark`) | [opengameart.org](https://opengameart.org/content/electricity-sound-effects-0) | **CC0** | 없음 | 2026-09-25 |
| `OpenGameArt/Electricity` | Electricity Game Sound Pack (`chargestart` · `hit`) | [opengameart.org](https://opengameart.org/content/electricity-game-sound-pack) | **CC0** | 없음 | 2026-09-25 |

**현재 전부 CC0 다.** 상업 이용 가능하고 크레딧 표기 의무가 없다.
다만 제작자들이 표기를 반기므로, 게임 크레딧에 아래 정도를 넣어 두면 좋다.

```
Sound Effects
  Kenney (kenney.nl) — CC0
  SnakeF8 (f8studios.itch.io) — CC0

Music
  Abstraction (tallbeard.itch.io) — CC0

Electricity SFX
  Brian MacIntosh, faxcorp (opengameart.org) — CC0
```

> Abstraction 제작자는 **NFT · AI 학습 · 원본 재판매**에 쓰지 말 것을 명시했다.
> 게임에 쓰는 것은 문제없다.

---

## 앞으로 넣을 때 주의

| 라이선스 | 판단 |
|---|---|
| **CC0** | 제약 없음. 가장 안전하다 |
| **CC-BY** | 출처 표기 **필수**. 크레딧에 반드시 넣어야 한다 |
| **CC-BY-NC** | **비상업 한정.** 캡스톤은 괜찮지만 상용화하면 통째로 교체해야 한다 |
| **Unity Asset Store** | 좌석(seat)당 라이선스. 파일을 팀원에게 전달하지 말고 **각자 계정으로 받을 것** |
| Sonniss GDC 번들 | 상업 이용 · 표기 불필요. 단 **AI/ML 학습 용도는 금지** |

무단 재배포 사이트(에셋을 퍼다 올려 둔 곳)에서 받지 말 것. 라이선스 위반이고 제출물에 문제가 된다.

---

## 파일 형식 메모

- **MP3 는 가져오지 않았다.** Snake 팩은 mp3·wav 가 같은 내용으로 중복되어 있어 wav 만 넣었다.
  원본은 바탕화면 `캡스톤 사운드 집` 에 그대로 있다.
- `.22LR` · `.308 (7.62x51)` 폴더는 **앞의 점을 뗐다.** Unity 는 점으로 시작하는 폴더를 무시해서
  그대로 두면 파일이 아예 안 보인다.
- 임포트 설정은 `Tools/Audio/오디오 임포트 설정 일괄 적용` 으로 맞춘다
  (`Assets/Editor/AudioImportSetup.cs`).
