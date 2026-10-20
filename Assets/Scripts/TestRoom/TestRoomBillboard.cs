using UnityEngine;

public class TestRoomBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}
