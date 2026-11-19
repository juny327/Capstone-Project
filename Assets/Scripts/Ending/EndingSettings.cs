using UnityEngine;

/// <summary>
/// 엔딩 설정 (2026-11-15 엔딩 개선) — Resources/Ending/EndingSettings 한 곳에서 정한다.
/// 에셋이 없으면 전부 꺼져 예전 그대로다 (쓰러지는 동작 → 엔딩 씬 → "You Win" 창).
///
/// 흐름: ① 마지막 한 방 (보스 체력 0 — 큰 순간) → ② 그 자리의 여운 (몸에서 빛이 새다 터지고, 빛 조각이 플레이어에게 온다)
///      → 화면이 하얗게 → 엔딩 씬의 타임라인 (그대로) → ③ 결과 화면 → ④ 크레딧 → ⑤ 로비로
/// </summary>
[CreateAssetMenu(menuName = "Ending/Ending Settings", fileName = "EndingSettings")]
public class EndingSettings : ScriptableObject
{
    public const string ResourcePath = "Ending/EndingSettings";

    static EndingSettings cached;
    static bool loaded;

    public static EndingSettings Current
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<EndingSettings>(ResourcePath);
                loaded = true;
            }
            return cached;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        cached = null;
        loaded = false;
    }

    [Header("② 그 자리의 여운 — 보스가 쓰러지는 동안")]
    [Tooltip("끄면 쓰러지는 동작만 나오고 바로 넘어간다 (예전 그대로)")]
    public bool deathShow = true;
    [Tooltip("빛 · 불꽃 색")]
    public Color lightColor = new Color(1f, 0.82f, 0.45f, 1f);
    [Tooltip("몸에서 빛이 새어 나오는 간격(초) — x 처음 · y 끝. 쓰러지는 동안 점점 잦아진다")]
    public Vector2 crackInterval = new Vector2(0.34f, 0.1f);
    [Tooltip("쓰러진 뒤 빛이 모이는 시간(초)")]
    [Min(0f)] public float chargeSeconds = 1.2f;
    [Tooltip("터진 뒤 빛 조각이 플레이어에게 날아오는 시간(초)")]
    [Min(0f)] public float moteSeconds = 1.3f;
    [Tooltip("빛 조각 수")]
    [Range(0, 30)] public int motes = 14;
    [Tooltip("터질 때 보스 몸을 감춘다")]
    public bool hideBody = true;
    [Tooltip("쓰러지기 시작할 때 배경음을 이 시간(초)에 걸쳐 끈다. 0 이면 그대로")]
    [Min(0f)] public float musicFade = 2.2f;

    [Header("연출 카메라 — 보스를 비췄다가 빛 조각을 따라 돌아온다 (그동안 조작 잠금)")]
    [Tooltip("끄면 카메라는 플레이어를 그대로 따라가고 조작도 잠기지 않는다 (멀리서 잡으면 보스가 화면 밖일 수 있다)")]
    public bool deathCamera = true;
    [Tooltip("마지막 한 방 뒤 카메라가 움직이기 시작할 때까지(초) — 히트 스톱 · 줌이 지나갈 시간")]
    [Min(0f)] public float cameraDelay = 0.3f;
    [Tooltip("보스 쪽으로 옮겨 가는 시간(초)")]
    [Min(0.05f)] public float cameraMove = 0.9f;
    [Tooltip("보스를 비출 때의 거리 배율 (1 = 게임 시점 그대로, 크면 멀리서)")]
    [Range(0.7f, 1.8f)] public float cameraPull = 1.15f;
    [Tooltip("터진 뒤 빛 조각이 떠나기 전에 머무는 시간(초)")]
    [Min(0f)] public float burstHold = 0.35f;

    [Header("차원 균열 엔딩 (2026-11-15) — 방 가운데에 균열이 열리고 캐릭터가 걸어 들어가는 장면이 자동으로 재생된다")]
    [Tooltip("끄면 예전 흐름: 하얀 화면 → 엔딩 씬 타임라인(지구 장면, 보류 중) → 결과")]
    public bool riftEnding = true;
    [Tooltip("균열이 열리는 자리의 오브젝트 이름 (보스 스테이지의 코어). 못 찾으면 보스가 쓰러진 자리")]
    public string riftAnchorName = "VOID / CORE";
    [Tooltip("균열 크기 (폭, 높이 m)")]
    public Vector2 riftSize = new Vector2(3.2f, 5.4f);
    [Tooltip("균열 안쪽 — 거의 검정")]
    public Color riftCoreColor = new Color(0.012f, 0.008f, 0.03f, 1f);
    [Tooltip("균열 가장자리 안쪽 — 짙은 남보라")]
    public Color riftBodyColor = new Color(0.17f, 0.07f, 0.33f, 1f);
    [Tooltip("균열 테두리의 빛 — 어두운 보라 (밝기는 아래 값)")]
    public Color riftEdgeColor = new Color(0.46f, 0.22f, 0.95f, 1f);
    [Tooltip("테두리 · 금 · 입자의 밝기 (1 = 색 그대로). 어두운 느낌이면 낮게")]
    [Range(0.1f, 2f)] public float riftGlow = 0.7f;
    [Tooltip("균열이 열리는 동안 방 조명 (1 = 그대로, 낮을수록 어둡다)")]
    [Range(0.05f, 1f)] public float riftRoomDim = 0.3f;
    [Tooltip("균열이 열리는 시간(초)")]
    [Min(0.3f)] public float riftOpenSeconds = 2.4f;
    [Tooltip("캐릭터가 균열을 바라보는 컷(초)")]
    [Min(0f)] public float riftLookSeconds = 1.4f;
    [Tooltip("걸어가는 거리(m) — 이만큼 떨어진 자리에서 걷기 시작한다")]
    [Min(3f)] public float riftWalkDistance = 11f;
    [Tooltip("걷는 속도(m/초) — 동작 빠르기 0.8 에 발이 미끄러지지 않는 값이 약 2.8")]
    [Min(0.5f)] public float riftWalkSpeed = 2.8f;
    [Tooltip("걷는 동작의 빠르기 (1 = 게임 속도)")]
    [Range(0.3f, 1.5f)] public float riftWalkAnimSpeed = 0.8f;
    [Tooltip("캐릭터가 들어간 뒤 카메라가 균열로 밀려 들어가는 시간(초)")]
    [Min(0.2f)] public float riftEnterSeconds = 1.8f;
    [Tooltip("영화 화면 띠의 두께 (화면 높이 1080 기준 px). 0 이면 없음")]
    [Min(0f)] public float letterbox = 110f;
    [Tooltip("들어간 뒤 어두운 화면에 뜨는 한 줄 (열린 결말). 비우면 없음")]
    public string riftCaption = "그리고, 다음 균열로";
    [Min(0f)] public float riftCaptionSeconds = 2.6f;
    [Tooltip("균열 엔딩에서 화면을 덮는 색 · 결과 화면의 바탕")]
    public Color riftCoverColor = new Color(0.012f, 0.007f, 0.024f, 1f);
    [Tooltip("균열 엔딩일 때 결과 화면 부제 (비우면 위의 resultSubtitle)")]
    public string riftResultSubtitle = "용은 쓰러졌지만, 균열은 아직 열려 있다";
    [Tooltip("균열 몸통 재질 — 어둡게 덮는 알파 블렌드 (도구가 만든다: Resources/Ending/RiftDark)")]
    public Material riftBodyMaterial;
    [Tooltip("입자 재질 — 부드러운 점 (도구가 만든다: Resources/Ending/RiftMote)")]
    public Material riftMoteMaterial;
    [Tooltip("균열이 찢어지는 소리")]
    public AudioClip riftOpenSound;
    [Range(0f, 1f)] public float riftOpenVolume = 0.8f;
    [Tooltip("열린 균열의 낮은 울림 (이어지는 소리)")]
    public AudioClip riftHumSound;
    [Range(0f, 1f)] public float riftHumVolume = 0.5f;
    [Tooltip("캐릭터가 들어간 뒤 빨려 드는 소리")]
    public AudioClip riftEnterSound;
    [Range(0f, 1f)] public float riftEnterVolume = 0.85f;

    [Header("소리 — 비워 두면 나지 않는다 (지금 것은 임시로 넣은 합성음: Logs/ending_snd/ending_snd.py)")]
    [Tooltip("쓰러진 보스에 빛이 모이는 동안 (chargeSeconds 와 길이를 맞춘다)")]
    public AudioClip chargeSound;
    [Range(0f, 1f)] public float chargeVolume = 0.7f;
    [Tooltip("빛으로 터지는 순간")]
    public AudioClip burstSound;
    [Range(0f, 1f)] public float burstVolume = 0.9f;
    [Tooltip("빛 조각이 플레이어에게 닿을 때 (높이를 바꿔 가며 여러 번)")]
    public AudioClip moteSound;
    [Range(0f, 1f)] public float moteVolume = 0.4f;
    [Tooltip("결과 화면이 뜰 때")]
    public AudioClip resultSound;
    [Range(0f, 1f)] public float resultVolume = 0.7f;

    [Header("화면 전환")]
    [Tooltip("엔딩 씬으로 넘어가기 전에 화면이 하얗게 덮이는 시간(초)")]
    [Min(0f)] public float fadeOut = 0.8f;
    [Tooltip("엔딩 씬에서 하얀 화면이 걷히는 시간(초)")]
    [Min(0f)] public float fadeIn = 1f;
    public Color fadeColor = Color.white;

    [Header("③ 결과 화면")]
    public string resultTitle = "임무 완료";
    public string resultSubtitle = "공허를 삼키던 용이 쓰러졌다";
    [Tooltip("숫자가 올라가는 시간(초)")]
    [Min(0f)] public float countUp = 0.9f;

    [Header("④ 크레딧 — 줄 앞에 '# ' 을 붙이면 제목")]
    [Tooltip("만든 사람 — 한 줄에 한 명 (역할을 같이 적어도 된다). 비워 두면 이 묶음은 나오지 않는다")]
    [TextArea(3, 10)] public string team = "";
    [Tooltip("음악(Torone · TeknoAXE · Aila Scott)은 CC BY 4.0 이라 표기가 의무다 — Assets/AUDIO_LICENSES.md 의 문구 그대로. 그 줄들은 지우지 말 것")]
    [TextArea(10, 40)] public string credits =
        "# 심우주\n캡스톤 디자인 2\n\n" +
        "# 사용한 에셋\nCethiel Dragon 3D\nSciFi Warrior PBR HP Polyart\nFutura Weapon Pack\nGunPack\nKayKit Character Animations\nKevin Iglesias Animations\nLowPoly Fantasy Monsters Pack\nMini Simple Characters Skeleton\nGabriel Aguiar Productions — Free Quick Effects Vol.1\nSolodream — Black Hole Effect\nShurikens Pack\nLightning Bolt\nSpace Exploration GUI Kit\n3D Sci-fi Asteroid Belt\nSkySeries Freebie\n\n" +
        "# 음악\n\"Music loops pack 2 - Sci-Fi\" by Chris \"Torone\" CB (torone.itch.io) — CC BY 4.0\n" +
        "\"Running Rage\" by TeknoAXE (teknoaxe.com) — CC BY 4.0, 반복용으로 잘라 씀\n" +
        "\"Sear The Time Vortex\" © 2024 by Aila Scott (ailascott.com) — CC BY 4.0\n" +
        "\"Rock City Ransom\" · \"Escape from the Planet of the Lobsters\" by Ragnar Random (ragnarrandom.itch.io) — CC0\n\n" +
        "# 효과음\nKenney (kenney.nl) — CC0\nSnakeF8 (f8studios.itch.io) — CC0\nBrian MacIntosh, faxcorp (opengameart.org) — CC0\nBikkit99, Breviceps, RobHorror240, SamsterBirdies, LilMati, sidequesting, _stubb (freesound.org) — CC0\n\n" +
        "# 도구\nUnity 6, Cinemachine, TextMesh Pro\n\n" +
        "# 플레이해 주셔서 감사합니다";
    [Tooltip("크레딧이 올라가는 속도 (화면 높이 1080 기준 px/초)")]
    [Min(10f)] public float creditsSpeed = 90f;
    [Tooltip("빨리감기 배율 — Space · Enter · 아래 화살표 · 마우스 왼쪽을 누르고 있는 동안 (2026-11-16)")]
    [Range(1f, 20f)] public float creditsFastMultiplier = 6f;
    [Tooltip("크레딧 화면 아래의 안내 글")]
    public string creditsHint = "Space를 누르고 있으면 빨리감기";

    [Header("⑤ 마지막 화면")]
    public string thanks = "플레이해 주셔서 감사합니다";
    public string lobbyLabel = "로비로";
}
