using UnityEngine;

/// <summary>
/// 스테이지 클리어 보상 한 장.
///
/// **이 프로젝트에서 보상을 늘릴 때 유일하게 건드릴 곳이다.**
/// 새 보상 종류(부착물 · 스탯 강화 · 서브유닛 …)를 넣으려면
/// 이 클래스를 상속한 뒤 에셋을 만들어 <see cref="StageRewardPool"/> 에 넣기만 하면 된다.
/// 카드 UI · 매니저 · 스테이지 흐름은 <b>한 줄도 고치지 않는다</b>.
///
/// 예) 총기 부착물을 추가할 때
/// <code>
/// [CreateAssetMenu(menuName = "Stage/Reward/Attachment")]
/// public class AttachmentReward : StageReward
/// {
///     public AttachmentData attachment;
///
///     public override string Title =&gt; attachment.displayName;
///     public override bool CanOffer(GameObject player) =&gt; ...;   // 이미 달았으면 false
///     public override void Apply(GameObject player) =&gt; ...;
/// }
/// </code>
/// </summary>
public abstract class StageReward : ScriptableObject
{
    [Header("카드 표시 (비우면 보상이 알아서 채운다)")]
    [Tooltip("비워 두면 각 보상이 기본 제목을 만든다")]
    [SerializeField] protected string titleOverride;

    [Tooltip("비워 두면 각 보상이 기본 설명을 만든다")]
    [TextArea(2, 4)]
    [SerializeField] protected string descriptionOverride;

    [Tooltip("비워 두면 각 보상이 기본 아이콘을 쓴다")]
    [SerializeField] protected Sprite iconOverride;

    /// <summary>카드에 굵게 나오는 이름.</summary>
    public abstract string Title { get; }

    /// <summary>카드 아래 설명.</summary>
    public abstract string Description { get; }

    /// <summary>카드 그림. 없으면 null 이어도 된다.</summary>
    public abstract Sprite Icon { get; }

    /// <summary>
    /// 지금 이 보상을 제시할 수 있는가.
    ///
    /// 슬롯이 꽉 찼거나 최대 레벨이라 **줘도 아무 일도 일어나지 않는 보상**을
    /// 후보에서 빼기 위한 것이다. 이걸 거르지 않으면 카드를 골랐는데
    /// 아무 변화가 없는 상황이 생긴다.
    /// </summary>
    public abstract bool CanOffer(GameObject player);

    /// <summary>실제로 보상을 적용한다.</summary>
    public abstract void Apply(GameObject player);
}
