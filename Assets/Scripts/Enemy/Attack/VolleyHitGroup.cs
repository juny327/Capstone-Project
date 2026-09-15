using System.Collections.Generic;

// A fresh instance belongs to exactly one three-projectile volley.
// Never reset or share it with another volley, including after pooling.
public sealed class VolleyHitGroup
{
    readonly HashSet<int> damagedPlayers = new HashSet<int>();
    public bool TryHit(int playerId) => damagedPlayers.Add(playerId);
}
