using UnityEngine;
using UnityEngine.UI;

public class EnemyUI : MonoBehaviour
{
    public Enemy enemy;

    public Canvas canvas;
    public Image healthFill;

    public float showDuration = 2f;

    float timer;

    private Camera cam;

    void Awake()
    {
        enemy = GetComponent<Enemy>();

        cam = Camera.main; // 카메라 캐싱

        if (canvas != null)
            canvas.enabled = false;
    }
    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            canvas.enabled = false;
            enabled = false;
        }
    }

    void LateUpdate()
    {
        if (!canvas.enabled) return;
        if (Camera.main == null) return;

        canvas.transform.forward = cam.transform.forward;
    }

    public void Show()
    {
        canvas.enabled = true;
        enabled = true;
        timer = showDuration;
    }

    public void UpdateHealth(float currentHp, float maxHp)
    {
        healthFill.fillAmount = currentHp / maxHp;
    }
}