// using TMPro;
// using UnityEngine;

// public class DamageText : MonoBehaviour
// {
//     public TextMeshProUGUI text;
//     public Canvas canvas;

//     float timer;
//     Vector3 moveDir;

//     public void Show(int damage, Vector3 worldPos)
//     {
//         transform.position = worldPos;

//         text.text = damage.ToString();

//         timer = 1f;

//         moveDir = new Vector3( Random.Range(-0.5f,0.5f), 1, Random.Range(-0.5f,0.5f));
//     }

//     void Update()
//     {
//         transform.position += moveDir * Time.deltaTime * 2f;

//         timer -= Time.deltaTime;

//         if(timer <= 0)
//         {
//             PoolManager.Instance.Return(gameObject);
//         }
//     }

//     void LateUpdate()
//     {
//         if (Camera.main == null) return;
        
//         canvas.transform.forward = Camera.main.transform.forward;
//     }
// }

using TMPro;
using UnityEngine;

public class DamageText : MonoBehaviour
{
    public TextMeshProUGUI text;
    public Canvas canvas;
    public Animator animator;

    private Camera cam;

    void Awake()
    {
        cam = Camera.main; // 카메라 캐싱
    }

    public void Show(int damage, Vector3 worldPos, bool isCritical) => Show((float)damage, worldPos, isCritical);

    public void Show(float damage, Vector3 worldPos, bool isCritical)
    {
        transform.position = worldPos;
        text.SetText(DamageTextManager.Format(damage));   // 10 미만은 소수 한 자리 (2026-11-17)

        if (isCritical)
        {
            animator.Play("CriticalPop");
        }
        else
        {
            animator.Play("DamagePop");
        }
    }

    void LateUpdate()
    {
        if (Camera.main == null) return;

        canvas.transform.forward = cam.transform.forward;
    }

    // 🔥 애니메이션 끝날 때 호출됨
    public void OnAnimationEnd()
    {
        GetComponent<Poolable>().ReturnToPool();
    }
}