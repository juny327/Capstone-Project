using UnityEngine;

public class HitFlashController : MonoBehaviour
{
    Renderer[] renderers;
    MaterialPropertyBlock mpb;

    float flashTimer;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock(); // MPB로 컨트롤 인스턴싱 안깨질려고
    }

    public void HitFlash()
    {
        flashTimer = 0.1f;
    }

    void Update()
    {
        if (flashTimer <= 0) return;

        flashTimer -= Time.deltaTime;

        float value = flashTimer * 10f;

        foreach (var r in renderers)
        {
            r.GetPropertyBlock(mpb); // Render의 material의 MPB 가져오기
            mpb.SetFloat("_FlashAmount", value);
            r.SetPropertyBlock(mpb); // MPB에 값 적용
        }
    }
}