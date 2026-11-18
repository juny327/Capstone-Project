using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicitly armed editor-only integration checks. Never included in a player build.</summary>
[InitializeOnLoad]
public static class DeepSpaceVerification
{
    static bool checking;
    public static string Report = "Not run";
    static DeepSpaceVerification() { EditorApplication.update += Guard; }
    public static void Arm() { SessionState.SetBool("DeepSpaceVerificationGuard",true); }
    public static void Disarm()
    {
        SessionState.SetBool("DeepSpaceVerificationGuard",false);
        if (GameEvents.Player) GameEvents.Player.GetComponent<PlayerStats>()?.ClearInvulnerable();
    }
    static void Guard()
    {
        if (!EditorApplication.isPlaying || checking || !SessionState.GetBool("DeepSpaceVerificationGuard",false)) return;
        if (GameEvents.Player) GameEvents.Player.GetComponent<PlayerStats>()?.SetInvulnerable(5);
    }

    public static string Check()
    {
        if (!EditorApplication.isPlaying || !GameEvents.Player) throw new Exception("A live player is required.");
        if (checking) throw new Exception("Check already running.");
        var zone=Object.FindObjectsByType<DeepSpaceHazard>(FindObjectsSortMode.None).First();
        checking=true;Report="RUNNING";
        zone.StartCoroutine(Run(zone));
        return "Started live hazard integration check";
    }
    static void Require(bool condition,string message) { if(!condition) throw new Exception("DEEP_SPACE_TEST: "+message); }

    static IEnumerator Run(DeepSpaceHazard zone)
    {
        var player=GameEvents.Player;var stats=player.GetComponent<PlayerStats>();var move=player.GetComponent<PlayerMove>();var body=player.GetComponent<Rigidbody>();
        var managers=Object.FindObjectsByType<EnemyManager>(FindObjectsSortMode.None).Where(m=>m.enabled).ToArray();
        var enemies=Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).Where(e=>e.gameObject.activeSelf).ToArray();
        var pos=player.position;bool moveEnabled=move.enabled;bool kinematic=body&&body.isKinematic;
        int hp=stats.currentHp;float baseline=move.CurrentSpeed,scale=Time.timeScale;
        float delay=zone.initialDelay,offset=zone.phaseOffset,rest=zone.restSeconds,active=zone.activeSeconds;
        bool passed=false;
        try
        {
            Require(!stats.IsDead,"player already dead");
            foreach(var m in managers)m.enabled=false;foreach(var e in enemies)e.gameObject.SetActive(false);
            foreach(var other in Object.FindObjectsByType<DeepSpaceHazard>(FindObjectsSortMode.None))if(other!=zone)other.Stop();
            move.enabled=false;if(body)body.isKinematic=true;Time.timeScale=1;
            zone.enabled=false;zone.initialDelay=0;zone.phaseOffset=0;zone.restSeconds=1;zone.activeSeconds=30;zone.enabled=true;
            player.position=zone.transform.position+Vector3.up*.8f;Physics.SyncTransforms();stats.ClearInvulnerable();
            int frame=Time.frameCount;
            yield return new WaitForSeconds(1.4f);
            Require(Time.frameCount>frame,"player loop did not advance");
            Require(zone.CurrentPhase==DeepSpaceHazard.Phase.Warning,"warning phase missing");
            Require(stats.currentHp==hp,"damage before active phase");
            Require(Mathf.Approximately(move.CurrentSpeed,baseline),"slow before active phase");
            float deadline=Time.time+5;
            while(zone.CurrentPhase!=DeepSpaceHazard.Phase.Active&&Time.time<deadline)yield return null;
            yield return null;
            Require(zone.CurrentPhase==DeepSpaceHazard.Phase.Active,"active phase missing");
            if(zone.effect==DeepSpaceHazard.Effect.Cryogenic)
            {
                Require(Mathf.Abs(move.CurrentSpeed-baseline*zone.slowMultiplier)<.001f,"slow not applied exactly once");
                player.position=Vector3.up*.8f;Physics.SyncTransforms();yield return null;yield return null;
                Require(Mathf.Abs(move.CurrentSpeed-baseline)<.001f,"exit did not restore speed");
                player.position=zone.transform.position+Vector3.up*.8f;Physics.SyncTransforms();yield return null;yield return null;
                Require(zone.HasAppliedSlow,"reentry did not apply slow");
                zone.enabled=false;yield return null;
                Require(Mathf.Abs(move.CurrentSpeed-baseline)<.001f,"disable leaked speed modifier");
                Report="PASS: warning safe; active slow x"+zone.slowMultiplier+"; exit/reentry correct; disable restores speed; frames advance";
            }
            else
            {
                Require(stats.currentHp<hp,"active zone did not damage player");
                Require(zone.DamageTicks==1,"initial damage applied more than once");
                int after=stats.currentHp;stats.SetInvulnerable(2);
                yield return new WaitForSeconds(1.2f);
                Require(stats.currentHp==after,"hazard ignored player invulnerability");
                stats.ClearInvulnerable();player.position=Vector3.up*.8f;Physics.SyncTransforms();
                yield return new WaitForSeconds(1.1f);
                Require(stats.currentHp==after,"damage applied outside zone");
                zone.Stop();yield return null;
                Require(zone.CurrentPhase==DeepSpaceHazard.Phase.Stopped,"stop failed");
                Report="PASS: warning safe; active damage "+(hp-after)+"; one hit per tick; invulnerability honored; outside safe; stop clean; frames advance";
            }
            passed=true;
        }
        finally
        {
            zone.enabled=false;zone.initialDelay=delay;zone.phaseOffset=offset;zone.restSeconds=rest;zone.activeSeconds=active;
            player.position=pos;move.enabled=moveEnabled;if(body)body.isKinematic=kinematic;
            stats.currentHp=hp;stats.ClearInvulnerable();Time.timeScale=scale;
            foreach(var m in managers)if(m)m.enabled=true;foreach(var e in enemies)if(e)e.gameObject.SetActive(true);
            checking=false;
            if(!passed)Report="FAILED - see Unity console";
        }
    }
}
