using UnityEngine;

public sealed class DeepSpaceMotion : MonoBehaviour
{
    public Vector3 angularVelocity = new Vector3(0, 3, 0);
    public float bobAmplitude = .2f;
    public float bobFrequency = .35f;
    Vector3 origin;
    void OnEnable() { origin = transform.localPosition; }
    void Update()
    {
        transform.Rotate(angularVelocity * Time.deltaTime, Space.Self);
        transform.localPosition = origin + Vector3.up * (Mathf.Sin(Time.time * bobFrequency) * bobAmplitude);
    }
}
