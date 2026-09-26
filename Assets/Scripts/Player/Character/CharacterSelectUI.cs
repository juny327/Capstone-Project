using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비의 캐릭터 구분 (설계 5장).
///
/// 두 캐릭터의 무기 버튼을 **각자의 영역에 떨어뜨려** 한 화면에 보여 준다.
/// 무기를 고르면 그 무기가 속한 캐릭터가 함께 정해진다 — 따로 캐릭터 버튼을 누를 필요가 없다.
/// 영역 제목을 눌러도 그 캐릭터의 첫 무기가 골라진다.
///
/// LobyManager(Core)는 고치지 않는다. 버튼은 지금처럼 LobyManager.SelectWeapon 을 부르고,
/// 이 스크립트는 OnSelectionChanged 를 듣고 캐릭터를 CharacterSelection 에 맡긴다.
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    [SerializeField] private LobyManager manager;

    [Tooltip("캐릭터 순서. 영역 · 제목 배열과 같은 순서여야 한다")]
    [SerializeField] private CharacterData[] characters;

    [Tooltip("LobyManager.selectableWeapons 와 같은 순서의 무기 목록 (로비로 돌아왔을 때 이전 선택을 되살리는 데 쓴다)")]
    [SerializeField] private WeaponData[] weaponOrder;

    [Tooltip("캐릭터마다 영역 제목을 눌렀을 때 고를 무기의 인덱스 (selectableWeapons 기준)")]
    [SerializeField] private int[] firstWeaponIndex;

    [Header("View")]
    [Tooltip("캐릭터 영역의 테두리 (배경보다 조금 큰 뒤쪽 이미지)")]
    [SerializeField] private Image[] panelFrames;

    [Tooltip("캐릭터 영역의 배경")]
    [SerializeField] private Image[] panelBackgrounds;

    [SerializeField] private TextMeshProUGUI[] titles;

    [Tooltip("배경의 바탕색. 캐릭터 색을 조금 섞어 영역마다 색이 갈리게 한다")]
    [SerializeField] private Color panelBase = new Color(0.03f, 0.045f, 0.06f, 1f);

    [Tooltip("캐릭터 색을 섞는 비율 (선택 / 선택 안 됨)")]
    [Range(0f, 1f)] [SerializeField] private float tintSelected = 0.24f;
    [Range(0f, 1f)] [SerializeField] private float tintNormal = 0.10f;

    [Range(0f, 1f)] [SerializeField] private float alphaSelected = 0.95f;
    [Range(0f, 1f)] [SerializeField] private float alphaNormal = 0.82f;

    [Tooltip("선택되지 않은 영역의 테두리를 이만큼 흐리게")]
    [Range(0f, 1f)] [SerializeField] private float dimAlpha = 0.3f;

    void Awake()
    {
        if (manager == null)
            manager = FindFirstObjectByType<LobyManager>();
    }

    void OnEnable()
    {
        if (manager == null) return;

        // LobyManager.Start 가 기본값(소총)을 고르며 이벤트를 쏜다. OnEnable 이 먼저 돌아 놓치지 않는다
        manager.OnSelectionChanged += Refresh;
        Refresh(manager.SelectedIndex);
    }

    void OnDisable()
    {
        if (manager == null) return;

        manager.OnSelectionChanged -= Refresh;
    }

    IEnumerator Start()
    {
        // LobyManager.Start 가 0번(소총)을 고른 뒤에 되살려야 덮어쓰이지 않는다
        yield return null;

        RestorePreviousSelection();
    }

    /// <summary>영역 제목 버튼의 OnClick 에 연결한다.</summary>
    public void SelectCharacter(int characterIndex)
    {
        if (manager == null || firstWeaponIndex == null) return;
        if (characterIndex < 0 || characterIndex >= firstWeaponIndex.Length) return;

        manager.SelectWeapon(firstWeaponIndex[characterIndex]);
    }

    // 스테이지에서 로비로 돌아왔을 때, 직전에 고른 무기(= 캐릭터)를 다시 골라 둔다
    void RestorePreviousSelection()
    {
        if (manager == null || weaponOrder == null) return;
        if (GameAppManager.Instance == null || GameAppManager.Instance.SelectedWeapon == null) return;

        int index = System.Array.IndexOf(weaponOrder, GameAppManager.Instance.SelectedWeapon);

        if (index >= 0 && index != manager.SelectedIndex)
            manager.SelectWeapon(index);
    }

    void Refresh(int weaponIndex)
    {
        int selected = FindCharacter(manager != null ? manager.Selected : null);

        if (selected >= 0)
            CharacterSelection.Select(characters[selected]);

        if (characters == null) return;

        for (int i = 0; i < characters.Length; i++)
        {
            bool on = i == selected;
            Color accent = characters[i] != null ? characters[i].accentColor : Color.white;
            Color dim = new Color(accent.r, accent.g, accent.b, accent.a * dimAlpha);

            if (panelFrames != null && i < panelFrames.Length && panelFrames[i] != null)
                panelFrames[i].color = on ? accent : dim;

            if (panelBackgrounds != null && i < panelBackgrounds.Length && panelBackgrounds[i] != null)
            {
                Color bg = Color.Lerp(panelBase, accent, on ? tintSelected : tintNormal);
                bg.a = on ? alphaSelected : alphaNormal;
                panelBackgrounds[i].color = bg;
            }

            if (titles != null && i < titles.Length && titles[i] != null)
                titles[i].color = on ? accent : new Color(1f, 1f, 1f, 0.55f);
        }
    }

    int FindCharacter(WeaponData weapon)
    {
        if (weapon == null || characters == null) return -1;

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null && characters[i].HasStartWeapon(weapon))
                return i;
        }

        return -1;
    }
}
