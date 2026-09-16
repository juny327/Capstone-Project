using System;

// One geometry definition shared by the warning mesh and swept damage queries.
public readonly struct DashFootprint
{
    public readonly float HalfWidth;
    public readonly float BodyRadius;
    public readonly float Travel;
    public float Rear => -BodyRadius;
    public float Front => Travel + BodyRadius;
    public float Length => Travel + 2f * BodyRadius;

    public DashFootprint(float requestedWidth, float bodyRadius, float travel)
    {
        BodyRadius = Math.Max(0.01f, bodyRadius);
        HalfWidth = Math.Max(requestedWidth * 0.5f, BodyRadius);
        Travel = Math.Max(0f, travel);
    }

    public void GetSweep(float from, float to, out float center, out float halfLength)
    {
        from = Math.Max(0f, Math.Min(Travel, from));
        to = Math.Max(from, Math.Min(Travel, to));
        center = (from + to) * 0.5f;
        halfLength = (to - from) * 0.5f + BodyRadius;
    }
}
