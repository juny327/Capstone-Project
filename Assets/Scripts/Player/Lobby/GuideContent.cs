using System;
using UnityEngine;

/// <summary>
/// 조작 가이드 창의 글 (2026-11-16, 11-17 그림 방식 · 움직이는 그림 · 캐릭터별 쪽) — 다섯 쪽:
///  1쪽 조작 — 자판 · 마우스 그림 위에 붙는 글 (어느 키가 무엇을 하는지)
///  2쪽 사수 · 3쪽 검사 — 카드마다 움직이는 그림 + 제목 + 설명. 카드를 누르면 크게 본다
///  4쪽 스킬과 상위 무기 — 레벨업 카드로 얻는 스킬 (글 · 그림은 스킬 에셋에서 읽는다) + 상위 무기를 얻는 법
///  5쪽 아이템 — 몬스터를 잡다 보면 바닥에 떨어지는 것들
/// 그림(자판 · 아이콘 · 사진 · 움직이는 그림)은 도구(Tools / 로비 / 조작 가이드 창 만들기)가 프리팹에 넣고, 글만 여기서 고친다.
/// 에셋(Resources/UI/GuideContent)이 없으면 아래 기본 글로 뜬다. 이 글꼴(RiaSans)에는 가운뎃점 · 곱셈표가 없어 쉼표 · 빗금으로 쓴다.
/// </summary>
[CreateAssetMenu(menuName = "UI/Guide Content", fileName = "GuideContent")]
public class GuideContent : ScriptableObject
{
    public const string ResourcePath = "UI/GuideContent";

    /// <summary>자판 · 마우스 그림의 설명 한 칸. id 는 도구가 그린 자리의 이름이라 바꾸지 않는다.</summary>
    [Serializable]
    public class KeyHint
    {
        [Tooltip("자리 이름 — Esc · Move · E · R · Shift · Z · F · Space · MouseLeft · MouseRight · MouseMove (바꾸지 않는다)")]
        public string id;
        [Tooltip("그 키가 하는 일")]
        public string label;

        public KeyHint() { }
        public KeyHint(string id, string label) { this.id = id; this.label = label; }
    }

    /// <summary>캐릭터 쪽의 카드 한 장 — 움직이는 그림 + 위의 작은 줄(무엇 · 어느 키) + 제목 + 설명. id 는 그림 이름이라 바꾸지 않는다.</summary>
    [Serializable]
    public class Card
    {
        [Tooltip("그림 이름 — Roll · GunMain · GunSupport · Grenade · Shock · Bayonet / Parry · SwordMain · SwordSupport · Shield · Grapple (바꾸지 않는다)")]
        public string id;
        [Tooltip("제목 위의 작은 줄 — 무엇인지 · 어느 키인지")]
        public string tag;
        public string title;
        [TextArea(2, 4)] public string text;

        public Card() { }
        public Card(string id, string tag, string title, string text) { this.id = id; this.tag = tag; this.title = title; this.text = text; }
    }

    /// <summary>아이템 카드 한 장. id 는 도구가 넣은 사진의 이름이라 바꾸지 않는다.</summary>
    [Serializable]
    public class Item
    {
        [Tooltip("사진 이름 — Exp · Coin · Magnet · Heal · Beacon (바꾸지 않는다)")]
        public string id;
        public string name;
        [TextArea(2, 4)] public string text;

        public Item() { }
        public Item(string id, string name, string text) { this.id = id; this.name = name; this.text = text; }
    }

    [Tooltip("창 제목")]
    public string windowTitle = "조작 가이드";
    [Tooltip("로비 버튼 글자")]
    public string buttonLabel = "조작 가이드";

    [Header("탭 이름 · 쪽 맨 위의 한 줄 (조작 / 사수 / 검사 / 스킬과 상위 무기 / 아이템 순서)")]
    public string[] tabs = { "조작", "사수", "검사", "스킬과 상위 무기", "아이템" };
    public string[] leads =
    {
        "공격은 자동이다. 움직이고, 조준하고, 피하는 데 집중한다.",
        "총을 들고 멀리서 싸운다. 보조 무기 3칸, 총에 부착물을 달아 키운다. 그림을 누르면 크게 본다.",
        "근접 무기를 들고 가까이서 싸운다. 체력이 많고, 보조 무기 2칸, 서브 능력 두 개를 쓴다. 그림을 누르면 크게 본다.",
        "레벨이 오르면 카드 창이 열린다. 모은 경험치로 스킬을 사고, 같은 스킬을 또 사면 레벨이 오른다 (값도 함께 오른다).",
        "몬스터를 잡다 보면 바닥에 떨어지는 것들이다.",
    };

    [Header("1쪽 — 자판 · 마우스 그림 위의 글")]
    public KeyHint[] keys =
    {
        new KeyHint("Esc", "설정, 일시 정지"),
        new KeyHint("Move", "이동"),
        new KeyHint("Shift", "달리기"),
        new KeyHint("E", "특수 능력 1"),
        new KeyHint("R", "장전"),
        new KeyHint("Z", "장비 정보 보기"),
        new KeyHint("F", "특수 능력 2 (검사)"),
        new KeyHint("Space", "점프"),
        new KeyHint("MouseLeft", "메뉴 선택"),
        new KeyHint("MouseRight", "회피 (구르기 / 패링)"),
        new KeyHint("MouseMove", "움직여서 조준, 공격은 자동"),
    };

    [Header("2쪽 — 사수 (카드 여섯 장)")]
    public Card[] gunnerCards =
    {
        new Card("Roll", "회피, 마우스 오른쪽", "구르기", "구르는 동안은 무적이다. 날아오는 탄과 공격을 그대로 지나친다. 다시 쓰기까지 4초"),
        new Card("GunMain", "주무기", "소총, 기관단총, 스나이퍼", "로비에서 하나를 고른다. 사거리 안의 적을 조준한 쪽으로 스스로 쏜다. R 로 장전"),
        new Card("GunSupport", "보조 무기, 3칸", "곁에 떠서 쏘는 총", "정비 창에서 산 총과 드론이 몸 둘레에 떠서 스스로 쏜다. 손에 든 것보다 약하다"),
        new Card("Grenade", "특수 능력, E (하부 부착물)", "유탄 발사기", "조준한 쪽으로 유탄을 던진다. 떨어진 자리에서 터져 뭉친 적을 한꺼번에 친다"),
        new Card("Shock", "특수 능력, E (하부 부착물)", "충격 방출기", "주변의 적을 밀쳐 내고 잠깐 묶는다. 둘러싸였을 때 숨을 돌린다"),
        new Card("Bayonet", "특수 능력, E (하부 부착물)", "총검 돌격", "조준한 쪽으로 짧게 돌진하며 찌른다. 돌진하는 동안은 무적이다"),
    };

    [Header("3쪽 — 검사 (카드 다섯 장 + 서브 능력 안내 한 칸)")]
    public Card[] swordsmanCards =
    {
        new Card("Parry", "회피, 마우스 오른쪽", "패링", "때를 맞춰 막으면 날아오는 탄을 되돌려 보내고 주변의 적을 밀쳐 낸다. 성공하면 떠 있는 무기가 반격한다"),
        new Card("SwordMain", "주무기", "검, 전투 망치, 방망이", "로비에서 하나를 고른다. 가까이 온 적을 스스로 친다. 망치는 기절시키고, 방망이는 멀리 날려 보낸다"),
        new Card("SwordSupport", "보조 무기, 2칸", "곁에 떠 있는 칼", "정비 창에서 산 근접 무기(대검, 창 등)가 몸 둘레에 떠 있다가 적에게 날아가 친다"),
        new Card("Shield", "서브 능력, E", "에너지 방패", "누르고 있는 동안 정면 공격을 막는다. 떼면 앞의 적을 밀쳐 낸다"),
        new Card("Grapple", "서브 능력, F", "그랩 훅", "와이어를 쏘아 처음 닿은 적을 내 앞까지 끌어온다"),
    };
    public string swordsmanNoteTitle = "서브 능력 키우기";
    [TextArea(2, 4)] public string swordsmanNoteText = "정비 창에서 사고 레벨을 올린다. Lv2, Lv3 에서 두 가지 변형 중 하나를 고른다";

    [Header("크게 보기 창")]
    public string zoomHint = "← → 다른 그림   ESC 돌아가기";
    public string zoomBadge = "+ 크게";

    [Header("4쪽 — 스킬과 상위 무기 (스킬 이름 · 설명 · 그림은 스킬 에셋에서 읽는다)")]
    [Tooltip("스킬 카드의 둘째 줄 — {0} 최대 레벨 · {1} 첫 값 (이 글꼴에는 곱셈표가 없다)")]
    public string skillLevelFormat = "최대 Lv{0}, 값 {1}부터";
    [Tooltip("상위 무기의 짝이 되는 스킬에 붙는 줄 — {0} 무기 · {1} 상위 무기")]
    public string skillPairFormat = "{0}의 짝 → {1}";
    public string statsName = "능력치 카드";
    [TextArea(1, 3)] public string statsText = "공격력, 이동 속도, 탄속, 최대 체력, 체력 회복. 값이 싸고 몇 번이든 살 수 있다";
    public string evolutionTitle = "상위 무기를 얻는 법";
    public string[] evolutionSteps =
    {
        "1. 정비 창에서 무기를 Lv3까지 강화한다",
        "2. 짝 장비를 갖춘다. 개조와 서브 능력은 정비 창에서 사고, 스킬은 레벨업 카드로 얻는다",
        "3. 정비 창 맨 앞이나 스테이지 보상에 뜨는 상위 무기 카드를 고른다 (무료)",
    };
    public string evolutionNote = "필요한 짝은 로비의 조합식과 위 스킬 카드에 나온다. 보조 칸의 무기도 진화한다";

    [Header("5쪽 — 아이템")]
    public Item[] items =
    {
        new Item("Exp", "경험치 구슬", "몬스터가 떨어뜨린다. 가까이 가면 빨려 온다. 모으면 레벨이 오르고 스킬 카드 창이 열린다"),
        new Item("Coin", "크레딧", "몬스터가 떨어뜨린다. 정비 창에서 무기와 장비를 사는 돈이다"),
        new Item("Magnet", "자석", "가끔 떨어진다. 주우면 주변의 구슬과 크레딧을 한꺼번에 끌어온다"),
        new Item("Heal", "회복 팩", "가끔 떨어진다. 체력을 25% 회복한다. 체력이 가득이면 주워지지 않는다"),
        new Item("Beacon", "정비 신호기", "스테이지 중간에 한 번 나타난다. 화면의 화살표를 따라가 닿으면 정비 창이 열린다"),
    };
    public string[] itemNotes =
    {
        "자석과 회복 팩은 25초 뒤 사라진다. 사라지기 5초 전부터 깜빡인다.",
        "스테이지를 깨면 바닥에 남은 크레딧이 모두 들어오고, 보상 카드 한 장과 정비 창이 나온다.",
    };

    static GuideContent current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => current = null;

    public static GuideContent Current
    {
        get
        {
            if (current != null) return current;
            current = Resources.Load<GuideContent>(ResourcePath);
            if (current == null) current = CreateInstance<GuideContent>();
            return current;
        }
    }

    /// <summary>id 의 키 설명 (없으면 null).</summary>
    public string KeyLabel(string id)
    {
        if (keys == null) return null;
        foreach (KeyHint k in keys)
            if (k != null && k.id == id) return k.label;
        return null;
    }

    /// <summary>id 의 캐릭터 카드 — 사수 · 검사 어느 쪽이든 (없으면 null).</summary>
    public Card FindCard(string id)
    {
        foreach (Card[] list in new[] { gunnerCards, swordsmanCards })
        {
            if (list == null) continue;
            foreach (Card c in list)
                if (c != null && c.id == id) return c;
        }
        return null;
    }

    /// <summary>id 의 아이템 (없으면 null).</summary>
    public Item FindItem(string id)
    {
        if (items == null) return null;
        foreach (Item i in items)
            if (i != null && i.id == id) return i;
        return null;
    }
}
