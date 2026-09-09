using UnityEngine;
using System.Collections;

public class BossBombSkill : BossSkill
{
    public float warningTime = 1.5f;

    public GameObject markerPrefab;
    public GameObject warningUI;
    public GameObject projectilePrefab;

    public Transform firePoint; // 머리 위치

    public float trackingTime = 1.2f; // 유도 시간

    protected override IEnumerator Execute()
    {
        Transform player = brain.player;

        // 1. 마커 생성
        GameObject marker = Instantiate(markerPrefab, player.position, Quaternion.Euler(90,0,0));
        var follow = marker.GetComponent<MarkerFollow>();
        follow.target = player;
        follow.trackingTime = warningTime; // 🔥 발사 전까지 추적

        if (warningUI != null)
            warningUI.SetActive(true);

        // 2. 발사 전까지 대기
        yield return new WaitForSeconds(warningTime);

        // 🔥 여기서 최종 타겟 확정
        Vector3 finalTarget = marker.transform.position;


        if (warningUI != null)
            warningUI.SetActive(false);

        // 3. 발사
        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        // 🔥 Vector3로 넘긴다 (중요)
        proj.GetComponent<BossProjectile>().Init(finalTarget);
    }
}