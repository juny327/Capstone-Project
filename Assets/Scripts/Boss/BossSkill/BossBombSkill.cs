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

    // 예고 중(아직 폭탄이 안 나간) 바닥 표시 — 발사한 뒤에는 폭발이 지운다
    GameObject pendingMarker;

    protected override IEnumerator Execute()
    {
        Transform player = brain.player;

        // 1. 마커 생성
        GameObject marker = Instantiate(markerPrefab, player.position, Quaternion.Euler(90,0,0));
        pendingMarker = marker;
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

        pendingMarker = null; // 여기부터는 폭발(BossProjectile.OnExplosion)이 표시를 지운다

        // 3. 발사
        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        // 🔥 Vector3로 넘긴다 (중요)
        proj.GetComponent<BossProjectile>().Init(finalTarget);
    }

    // 2026-11-14 — 예고 도중 멈추면(플레이어 사망 → StopAllSkills) 코루틴이 끊겨
    // Warning 과 바닥 표시가 꺼지지 않고 게임오버 화면까지 남았다. 멈출 때 같이 치운다.
    public override void StopSkill()
    {
        base.StopSkill();
        ClearWarning();
    }

    void OnDisable()
    {
        ClearWarning();
    }

    void ClearWarning()
    {
        if (warningUI != null)
            warningUI.SetActive(false);

        if (pendingMarker != null)
            Destroy(pendingMarker);

        pendingMarker = null;
    }
}