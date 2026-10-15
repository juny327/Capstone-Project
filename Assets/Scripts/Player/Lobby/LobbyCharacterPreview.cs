using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 로비 가운데 3D 캐릭터 미리보기 (로비-선택화면-설계.md).
///
/// 화면 밖 먼 곳에 작은 무대(받침 · 카메라)를 만들고, 그 카메라가 RenderTexture 에 찍은 그림을 RawImage 로 보여 준다.
/// 모델은 게임과 같은 Player 프리팹을 꺼내 **그림에 필요 없는 컴포넌트(이동 · 체력 · 무기 로직 · 물리)를 모두 뗀 뒤** 켠다 —
/// 따로 만든 모형 프리팹이 없으니 플레이어 모델 · 애니메이터가 바뀌어도 미리보기가 저절로 따라간다.
///
/// 게임과 같은 방식으로 보여 준다
///   · 캐릭터 색: CharacterLoadout.SwapBodyMaterial (기본 몸 머티리얼 → CharacterData.bodyMaterial)
///   · 무기: WeaponController 소켓에 무기 프리팹을 로컬 0 으로 붙인다 (무기 로직은 떼고 발광 연출만 남긴다)
///   · 자세: ShootLayer / MeleeLayer 무게 · MeleeStyle (WeaponController.ApplyUpperBodyLayers · MeleeWeapon.ApplyStyle 과 같다)
///   · 양손 무기: MeleeTwoHandGrip.SetPreviewGrip 으로 왼손이 자루를 쥔다
///
/// 배치: 로비 RawImage 와 같은 오브젝트. 끌면 모델이 돈다. LobbySetup 이 만든다.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class LobbyCharacterPreview : MonoBehaviour, IDragHandler
{
    [Tooltip("미리보기에 쓸 플레이어 프리팹 (게임과 같은 것)")]
    [SerializeField] private GameObject characterPrefab;

    [Tooltip("받침 머티리얼 (_BaseColor 를 캐릭터 색으로 물들인다)")]
    [SerializeField] private Material pedestalMaterial;

    [Header("무대")]
    [Tooltip("무대 자리 — 로비 카메라에 안 보이는 먼 곳")]
    [SerializeField] private Vector3 stageOrigin = new Vector3(0f, -500f, -800f);
    [SerializeField] private Color background = new Color(0.055f, 0.06f, 0.13f, 1f);
    [Range(10f, 60f)] [SerializeField] private float fieldOfView = 26f;
    [SerializeField] private float cameraDistance = 6.4f;
    [SerializeField] private float cameraHeight = 1.5f;
    [SerializeField] private float lookHeight = 1.1f;
    [Tooltip("빛이 오는 쪽에서 카메라를 이만큼 옆으로 돌린다 (얼굴에 3/4 빛)")]
    [SerializeField] private float lightSideAngle = 35f;
    [Tooltip("처음에 카메라 쪽에서 이만큼 몸을 돌려 세운다 (무기가 보이게)")]
    [SerializeField] private float startYaw = -28f;
    [SerializeField] private float pedestalRadius = 0.9f;

    [Header("조작")]
    [Tooltip("끌 때 픽셀당 도는 각도")]
    [SerializeField] private float dragSpeed = 0.4f;
    [Tooltip("RenderTexture 해상도 = 화면 크기 × 이 값")]
    [Range(1f, 2f)] [SerializeField] private float supersample = 1.5f;

    // 그림에 남길 무기 연출 (나머지 무기 스크립트는 뗀다)
    static readonly Type[] KeepOnWeapon = { typeof(EvolvedGlow), typeof(EvolvedOrbit) };
    static readonly Type[] KeepOnCharacter = { typeof(MeleeTwoHandGrip) };

    static readonly int MeleeStyleHash = Animator.StringToHash("MeleeStyle");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private RawImage view;
    private RenderTexture texture;
    private GameObject stage;
    private Camera stageCamera;
    private Renderer pedestal;
    private Transform character;
    private Animator anim;
    private MeleeTwoHandGrip grip;
    private readonly Dictionary<WeaponSocket, Transform> sockets = new Dictionary<WeaponSocket, Transform>();
    private Material defaultBody;
    private Material currentBody;
    private GameObject weaponModel;
    private Quaternion facing = Quaternion.identity;
    private float yaw;
    private int shootLayer = -1;
    private int meleeLayer = -1;

    /// <summary>지금 손에 든 무기 (테스트용).</summary>
    public WeaponData ShownWeapon { get; private set; }

    /// <summary>지금 보여 주는 캐릭터 (테스트용).</summary>
    public CharacterData ShownCharacter { get; private set; }

    /// <summary>무대 카메라가 찍는 그림 (테스트 · 캡처용).</summary>
    public RenderTexture Texture => texture;

    /// <summary>지금 손에 붙은 무기 모형 (테스트용).</summary>
    public GameObject WeaponModel => weaponModel;

    void Awake()
    {
        view = GetComponent<RawImage>();
        BuildStage();

        // 무대보다 먼저 고른 것이 있으면 (다른 오브젝트의 OnEnable 이 먼저 돈 경우) 다시 입힌다
        if (ShownCharacter != null) ShowCharacter(ShownCharacter);

        WeaponData pending = ShownWeapon;
        ShownWeapon = null;
        if (pending != null) ShowWeapon(pending);
    }

    void OnDestroy()
    {
        if (stage != null) Destroy(stage);

        if (texture != null)
        {
            if (stageCamera != null) stageCamera.targetTexture = null;
            texture.Release();
            Destroy(texture);
        }
    }

    void Update()
    {
        if (character != null)
            character.localRotation = facing * Quaternion.Euler(0f, yaw, 0f);
    }

    public void OnDrag(PointerEventData eventData)
    {
        yaw -= eventData.delta.x * dragSpeed;
    }

    /// <summary>캐릭터 색 · 받침 색을 바꾼다.</summary>
    public void ShowCharacter(CharacterData data)
    {
        ShownCharacter = data;
        if (character == null || data == null) return;

        Material target = data.bodyMaterial != null ? data.bodyMaterial : defaultBody;
        CharacterLoadout.SwapBodyMaterial(character.gameObject, currentBody, target);
        if (target != null) currentBody = target;

        if (pedestal != null)
        {
            var block = new MaterialPropertyBlock();
            pedestal.GetPropertyBlock(block);
            Color c = Color.Lerp(new Color(0.08f, 0.09f, 0.16f, 1f), data.accentColor, 0.45f);
            c.a = 1f;
            block.SetColor(BaseColorId, c);
            pedestal.SetPropertyBlock(block);
        }
    }

    /// <summary>손에 든 무기를 바꾼다. 상위 무기를 주면 진화한 모습을 든다.</summary>
    public void ShowWeapon(WeaponData data)
    {
        if (data == ShownWeapon && weaponModel != null) return;
        ShownWeapon = data;

        if (weaponModel != null)
        {
            Destroy(weaponModel);
            weaponModel = null;
        }

        if (character == null || data == null) return;

        WeaponKind kind = data.Kind;
        SetLayer(shootLayer, kind == WeaponKind.Projectile ? 1f : 0f);
        SetLayer(meleeLayer, kind == WeaponKind.Melee ? 1f : 0f);

        if (grip != null) grip.SetPreviewGrip(null, Vector3.zero, Vector3.zero);
        if (data.weaponPrefab == null) return;

        Transform socket = sockets.TryGetValue(data.socket, out Transform s) && s != null ? s : character;

        weaponModel = SpawnStripped(data.weaponPrefab, KeepOnWeapon);
        weaponModel.name = "Preview_" + data.weaponPrefab.name;
        weaponModel.transform.SetParent(socket, false);
        weaponModel.transform.localPosition = Vector3.zero;
        weaponModel.transform.localRotation = Quaternion.identity;
        SetLayerRecursive(weaponModel, character.gameObject.layer);

        MeleeWeapon melee = data.weaponPrefab.GetComponent<MeleeWeapon>();
        if (melee != null)
        {
            if (anim != null) anim.SetInteger(MeleeStyleHash, (int)melee.Style);

            if (grip != null && melee.TryGetLeftGripLocal(out Vector3 near, out Vector3 far))
                grip.SetPreviewGrip(socket, near, far);
        }
    }

    /// <summary>처음 각도로 되돌린다.</summary>
    public void ResetRotation() => yaw = 0f;

    // ───────── 무대 ─────────

    void BuildStage()
    {
        stage = new GameObject("LobbyPreviewStage");
        stage.transform.position = stageOrigin;

        // 빛이 오는 쪽(빛 방향의 반대)에서 조금 옆으로 비켜 찍는다 — 장면 빛을 그대로 쓰고 따로 빛을 두지 않는다
        Light sun = FindSun();
        Vector3 toCamera = Vector3.back;
        if (sun != null)
        {
            Vector3 flat = Vector3.ProjectOnPlane(-sun.transform.forward, Vector3.up);
            if (flat.sqrMagnitude > 0.0001f) toCamera = flat.normalized;
        }
        toCamera = Quaternion.Euler(0f, lightSideAngle, 0f) * toCamera;

        var camGo = new GameObject("PreviewCamera");
        camGo.transform.SetParent(stage.transform, false);
        camGo.transform.position = stageOrigin + toCamera * cameraDistance + Vector3.up * cameraHeight;
        camGo.transform.LookAt(stageOrigin + Vector3.up * lookHeight);

        Rect rect = view.rectTransform.rect;
        int w = Mathf.Clamp(Mathf.RoundToInt(rect.width * supersample), 256, 2048);
        int h = Mathf.Clamp(Mathf.RoundToInt(rect.height * supersample), 256, 2048);
        texture = new RenderTexture(w, h, 24) { name = "LobbyPreview", antiAliasing = 4 };
        texture.Create();

        stageCamera = camGo.AddComponent<Camera>();
        stageCamera.clearFlags = CameraClearFlags.SolidColor;
        stageCamera.backgroundColor = background;
        stageCamera.fieldOfView = fieldOfView;
        stageCamera.nearClipPlane = 0.1f;
        stageCamera.farClipPlane = 30f;
        stageCamera.targetTexture = texture;
        stageCamera.depth = -10f;
        view.texture = texture;
        view.color = Color.white;

        // 받침
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Pedestal";
        Destroy(disc.GetComponent<Collider>());
        disc.transform.SetParent(stage.transform, false);
        disc.transform.localPosition = new Vector3(0f, -0.03f, 0f);
        disc.transform.localScale = new Vector3(pedestalRadius * 2f, 0.03f, pedestalRadius * 2f);
        pedestal = disc.GetComponent<Renderer>();
        if (pedestalMaterial != null) pedestal.sharedMaterial = pedestalMaterial;

        // 캐릭터 — 카메라를 보고 startYaw 만큼 돌려 세운다
        Vector3 faceDir = Vector3.ProjectOnPlane(camGo.transform.position - stageOrigin, Vector3.up).normalized;
        facing = Quaternion.LookRotation(faceDir, Vector3.up) * Quaternion.Euler(0f, startYaw, 0f);

        if (characterPrefab == null)
        {
            Debug.LogWarning("[LobbyCharacterPreview] characterPrefab 이 비어 있습니다 — Tools / 로비 / 선택 화면 만들기 를 실행하세요.", this);
            return;
        }

        SpawnCharacter();
    }

    void SpawnCharacter()
    {
        // 꺼진 그릇 안에 꺼내면 Awake 가 돌지 않는다 — 그 사이에 게임 로직을 뗀다
        var holder = new GameObject("PreviewHolder");
        holder.SetActive(false);

        GameObject go = Instantiate(characterPrefab, holder.transform);
        go.name = "PreviewCharacter";

        var loadout = go.GetComponent<CharacterLoadout>();
        defaultBody = loadout != null ? loadout.DefaultBodyMaterial : null;
        currentBody = defaultBody;

        var weapons = go.GetComponent<WeaponController>();
        foreach (WeaponSocket s in Enum.GetValues(typeof(WeaponSocket)))
            sockets[s] = weapons != null ? weapons.FindSocket(s) : null;

        Strip(go, KeepOnCharacter);

        // "Player" 태그로 플레이어를 찾는 HUD 들이 모형을 붙잡지 않게
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.tag = "Untagged";

        go.transform.SetParent(stage.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = facing;
        Destroy(holder);

        character = go.transform;
        anim = go.GetComponent<Animator>();
        grip = go.GetComponent<MeleeTwoHandGrip>();

        if (anim != null)
        {
            anim.applyRootMotion = false;
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;   // 일시정지한 채 로비로 와도 움직인다
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            shootLayer = anim.GetLayerIndex("ShootLayer");
            meleeLayer = anim.GetLayerIndex("MeleeLayer");
        }
    }

    GameObject SpawnStripped(GameObject prefab, Type[] keep)
    {
        var holder = new GameObject("PreviewHolder");
        holder.SetActive(false);

        GameObject go = Instantiate(prefab, holder.transform);
        Strip(go, keep);

        go.transform.SetParent(null, false);
        Destroy(holder);
        return go;
    }

    /// <summary>그림에 필요 없는 컴포넌트를 뗀다 — 스크립트(keep 제외) · 물리 · 소리. 렌더러 · 애니메이터 · 파티클은 남긴다.</summary>
    static void Strip(GameObject root, Type[] keep)
    {
        // 서로 RequireComponent 로 묶여 있으면 한 번에 안 떨어진다 — 몇 번 돈다
        for (int pass = 0; pass < 4; pass++)
        {
            bool left = false;

            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || !ShouldStrip(c, keep)) continue;

                if (CanRemove(c)) DestroyImmediate(c);
                else left = true;
            }

            if (!left) break;
        }
    }

    static bool ShouldStrip(Component c, Type[] keep)
    {
        if (c is MonoBehaviour)
            return Array.IndexOf(keep, c.GetType()) < 0;

        return c is Collider || c is Rigidbody || c is AudioSource || c is Joint;
    }

    // 같은 오브젝트의 다른 (뗄) 컴포넌트가 RequireComponent 로 이것을 붙들고 있으면 나중에 뗀다
    static bool CanRemove(Component c)
    {
        Type type = c.GetType();

        foreach (Component other in c.GetComponents<Component>())
        {
            if (other == null || other == c) continue;

            foreach (RequireComponent req in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
            {
                if (Requires(req.m_Type0, type) || Requires(req.m_Type1, type) || Requires(req.m_Type2, type))
                    return false;
            }
        }

        return true;
    }

    static bool Requires(Type required, Type type) => required != null && required.IsAssignableFrom(type);

    static Light FindSun()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;

        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.isActiveAndEnabled) return l;

        return null;
    }

    void SetLayer(int index, float weight)
    {
        if (anim != null && index >= 0 && index < anim.layerCount)
            anim.SetLayerWeight(index, weight);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}
