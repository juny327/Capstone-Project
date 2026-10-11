using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Effects/SpeedUp")]
public class SpeedUpgrade : UpgradeEffect
{
    public float amount;

    public override void Apply(GameObject player)
    {
        Debug.Log("SpeedUpgrade Apply : " + player.name);
        PlayerMove move = player.GetComponent<PlayerMove>();

        if (move != null)
        {
            Debug.Log("a");
            move.AddSpeed(amount);
        }
        else
        {
            Debug.Log("PlayerMove not found");
        }
    }
}
