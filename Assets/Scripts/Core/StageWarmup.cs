using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 입장 가림막 아래에서 미리 채우기 (2026-11-18 성능 2단계).
/// 스테이지에 들어갈 때 화면이 덮인 동안(SceneCurtain.WhileCovered) 처음 쓸 때 끊기던 것을 먼저 만들어 둔다.
///
///  · 스킬 투사체 · 효과 — 파이어볼 · 전격샷 · 토네이도 · 체인라이트닝 · 블랙홀 (PoolManager 풀, 스테이지 씬마다)
///  · 처형자 연쇄 폭발 효과 (FxPool) — 한 번 연쇄에 여럿이 한꺼번에 터진다
///  · 정비 창 — 처음 열 때만 63ms 끊겼다 (판에 한 번)
///
/// 스킬 · 상위 무기 설정은 이미 불러온 것(레벨업 카드 목록 · 무기 데이터가 들고 있는 것)만 본다 — 새로 불러오지 않는다.
/// </summary>
public static class StageWarmup
{
    /// <summary>마지막으로 한 일 (시험 · 로그).</summary>
    public static string Last { get; private set; } = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneCurtain.WhileCovered -= OnCovered;
        SceneCurtain.WhileCovered += OnCovered;
    }

    static void OnCovered(Scene scene)
    {
        if (!scene.name.StartsWith("Stage")) return;

        var sw = Stopwatch.StartNew();
        int pools = 0;

        // 깨울 자리 — 카메라 화면 안, 플레이어에서 5m 옆 (한 번 그려져야 재질 · 메시가 올라간다. 가림막 아래라 보이지 않는다).
        // 플레이어 자리는 피한다 — 블랙홀 프리팹에 통과되지 않는 콜라이더가 있어 한 프레임 밀릴 수 있다
        GameObject player = GameAppManager.Instance != null ? GameAppManager.Instance.Player : null;
        wakeAt = (player != null ? player.transform.position : Vector3.zero) + Vector3.right * 5f + Vector3.up * 1.2f;

        PoolManager pm = PoolManager.Instance;
        if (pm != null)
        {
            // 동시에 날아다니는 수만큼 — 쏘는 간격 · 나는 시간 기준
            foreach (FireBallEffect e in Resources.FindObjectsOfTypeAll<FireBallEffect>()) pools += Fill(pm, e.projectilePrefab, 4);
            foreach (ElectricShotEffect e in Resources.FindObjectsOfTypeAll<ElectricShotEffect>()) pools += Fill(pm, e.projectilePrefab, 6);
            foreach (TornadoEffect e in Resources.FindObjectsOfTypeAll<TornadoEffect>()) pools += Fill(pm, e.projectilePrefab, 3);
            foreach (ChainLightningEffect e in Resources.FindObjectsOfTypeAll<ChainLightningEffect>()) pools += Fill(pm, e.lightningPrefab, 8);
            foreach (BlackHoleEffect e in Resources.FindObjectsOfTypeAll<BlackHoleEffect>()) pools += Fill(pm, e.blackHolePrefab, 1);
        }

        int chains = 0;
        foreach (KillChainExplosionEffect k in Resources.FindObjectsOfTypeAll<KillChainExplosionEffect>())
        {
            k.Prewarm(8, wakeAt);
            chains++;
        }

        bool shop = false;
        if (MaintenanceUI.Instance != null)
        {
            MaintenanceUI.Instance.PrewarmHidden();
            shop = true;
        }

        // 보스 — 드래곤 불덩이 풀 (보스 씬에만 있다. 등장 연출 전이라 꺼져 있는 드래곤도 찾는다)
        int bosses = 0;
        foreach (DragonBoss dragon in Object.FindObjectsByType<DragonBoss>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            dragon.PrewarmPools();
            bosses++;
        }

        // 연출 공통 재료 (2026-11-19) — 입자 채널 9개를 만들고 한 번 그려 둔다 (첫 폭발 · 낙뢰 때 끊기지 않게)
        FxKit.Prewarm(wakeAt);

        // 피격 연출 — 처음 맞을 때 만들던 화면 요소를 미리 (첫 피격 프레임 12ms)
        PlayerHurtFx hurt = player != null ? player.GetComponent<PlayerHurtFx>() : null;
        if (hurt != null) hurt.Prewarm();

        Last = $"{scene.name}: 스킬 풀 {pools}개 · 연쇄 폭발 {chains}개 · 정비 창 {(shop ? "깨움" : "없음")} · 피격 연출 {(hurt != null ? "준비" : "없음")}{(bosses > 0 ? " · 보스 불덩이 풀" : "")} · {sw.Elapsed.TotalMilliseconds:0.0}ms";
    }

    static Vector3 wakeAt;

    /// <summary>
    /// 풀에 미리 만들고, 한 프레임 켰다 끈다 (2026-11-18) — 만들기만 해서는 처음 켜질 때의 초기화가 첫 시전 프레임에 남았다
    /// (스킬 카드를 고른 순간 51ms). 투사체는 노릴 적이 없어 다음 프레임에 스스로 풀로 돌아가고, 나머지는 PoolManager 가 돌려놓는다.
    /// </summary>
    static int Fill(PoolManager pm, GameObject prefab, int count)
    {
        if (prefab == null) return 0;
        pm.Prewarm(prefab, count);
        pm.Wake(prefab, wakeAt);
        return 1;
    }
}
