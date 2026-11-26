# Real Spells Demo - manual

Version 1.0.0. Package root `Assets/RealSpellsDemo`, namespace `RealSpellsDemo`. The one-minute start is `README.md`; the
recipe format is `SpellRecipe.md`; AI coding agents read `AGENTS.md`.

Contents: 1 Requirements and install, 2 What is in the package, 3 Adding a spell to a game, 4 The spell types, 5 Events and pooling, 6 Sound, 7 Changing a spell, 8 Quality tiers and build size, 9 Demo content, 10 Known limits, 11 Support.

## 1. Requirements and install

- Unity 6000.3, Universal Render Pipeline 17.3. **URP only**: in a Built-in or HDRP project the materials are pink.
- A URP asset must be assigned in Project Settings > Graphics (projects made from Unity's "Universal 3D" template have
  one). The spells need the camera's depth texture: the demo cameras ask for it themselves; in your scene either tick
  Depth Texture on the URP asset or on the camera (Rendering > Depth Texture). HDR on the URP asset gives the glow its
  range; bloom in a post-processing volume is what makes the effects shine as in the videos.
- Import the package: everything lands in `Assets/RealSpellsDemo`. It depends on no other package.
- Remove the package: delete `Assets/RealSpellsDemo`.
- Several Real Spells packages (the three styles and the free Demo) can share one project. Each has its own root
  folder, namespace, assemblies, shader names and GUIDs, so nothing collides.
- Folders you can delete without breaking the rest: `Demo/` (demo scene and its content), `Documentation/`,
  any `Spells/<id>/` folder of a spell you do not use.

## 2. What is in the package

10 spells. Every one is a `SpellDefinition` asset and a prefab with a `SpellPlayer`:

`Assets/RealSpellsDemo/Spells/<id>/<id>.asset` and `<id>.prefab`.

The number is the permanent number of the effect in every Real Spells video (R-01 ...).

| No. | Id | Name | Element | Type | Asset |
|---|---|---|---|---|---|
| R-01 | `fireball` | Fireball | Fire | Bolt | `Spells/fireball/fireball.asset` |
| R-02 | `meteor` | Meteor | Fire | Strike | `Spells/meteor/meteor.asset` |
| R-09 | `frost_nova` | Frost Nova | Ice | Nova | `Spells/frost_nova/frost_nova.asset` |
| R-10 | `frost_ray` | Frost Ray | Ice | Beam | `Spells/frost_ray/frost_ray.asset` |
| R-12 | `frozen` | Frozen | Ice | Status | `Spells/frozen/frozen.asset` |
| R-14 | `thunder_strike` | Thunder Strike | Lightning | Strike | `Spells/thunder_strike/thunder_strike.asset` |
| R-29 | `divine_shield` | Divine Shield | Holy | Ward | `Spells/divine_shield/divine_shield.asset` |
| R-31 | `shadow_bolt` | Shadow Bolt | Shadow | Bolt | `Spells/shadow_bolt/shadow_bolt.asset` |
| R-39 | `poison_cloud` | Poison Cloud | Poison | Nova | `Spells/poison_cloud/poison_cloud.asset` |
| R-44 | `stone_spikes` | Stone Spikes | Earth | Strike | `Spells/stone_spikes/stone_spikes.asset` |

## 3. Adding a spell to a game

The whole setup for a character that casts at an enemy and applies damage on the hit:

```csharp
using UnityEngine;
using RealSpellsDemo;

public class Mage : MonoBehaviour
{
    public SpellDefinition spell;      // drag Assets/RealSpellsDemo/Spells/fireball/fireball.asset here
    public Transform hand;             // where the spell leaves the character
    public Transform enemy;            // the target (give it a collider and a SpellReactive)

    public void Attack()
    {
        // one pooled instance: cast, flight, impact, lights and sound
        SpellPlayer p = SpellPlayer.Cast(spell, hand, enemy, transform);
        p.Contacted += OnContact;      // the frame the effect lands
    }

    void OnContact(SpellPlayer p, Vector3 point)
    {
        p.Contacted -= OnContact;      // instances are pooled: unsubscribe
        enemy.GetComponent<Health>().Damage(40);   // your own game code
    }
}
```

1. The **caster**: any object. The last argument of `Cast` is the caster's root: the projectile flies through every
   collider under it (your own character never blocks its own spell).
2. The **target**: give it a collider (the projectile stops on it) and add `SpellReactive` to its root: the target
   flashes, rocks back and shows the tint of a status. `knockback` and `flash` switch the two reactions.
3. The **camera**: add `SpellCameraShake`; impacts shake it. `strength` scales all shake.
4. Apply damage in `Contacted`. `ImpactPoint` and `ImpactNormal` of the instance say where it landed.

Without code: put the `SpellCaster` component on the character, assign `spell` and `hand`, and call
`CastAt(target)` (a Transform or a point) from your input or animation event.

Lights: every spell carries real point lights. `SpellPlayer.LightScale` (static, default 1) multiplies all of them:
set it once when your scene is much darker or brighter than the demo.

## 4. The spell types

| Type | What the game does | What the effect does |
|---|---|---|
| Bolt | `Cast(def, hand, target, casterRoot)` | flies from the hand; stops on the first collider of `hitMask` that is not under `ignoreRoot` (the caster root you passed) and raises `Contacted` there |
| Strike | `Cast(def, hand, target)` or `Cast(def, hand, point)` | shows a telegraph mark on the ground under the target for the spell's telegraph time (0.35-0.5 s), then lands; `Contacted` on the strike frame |
| Nova | `Cast(def, hand, target, casterRoot)` | a front runs outward from the caster; `Contacted` when it reaches the target you passed |
| Beam | keep the instance, call `StopLoop()` to release | holds from the hand to the target while it runs, with an impact loop at the far end |
| Ward | keep the instance, `StopLoop()` to drop it | wraps the caster; `HitWard(point)` plays its hit flash, `ShellPoint(from)` gives the point of the shell facing an attacker |
| Status | keep the instance, `StopLoop()` to end | sits on the target while it runs |

- Only a **projectile** tests colliders (a sphere sweep along its path; `SweepHit` is the same test for your own
  code). Beams, novas, strikes, wards and statuses address the target you pass and do not test colliders. To stop a
  beam at a wall, do your own raycast first and cast the beam at the hit point.
- `hitMask` (on the instance and on `SpellCaster`) limits what a projectile can hit. Set it on the prefab or right
  after `SpellPool.Get`.
- For wards and statuses that should sit on the body rather than at the feet, set `casterBody` / `targetBody` on the
  instance to a chest-height transform before casting.

## 5. Events and pooling

Instance events: `Released` (the projectile leaves the hand, or the cast ends), `Contacted(player, point)`,
`Finished` (everything faded). A pooled instance goes back to the pool on `Finished`: unsubscribe your handlers
there, the next `Cast` may hand you the same object.

Static events of `SpellPlayer`, for game-wide systems:

| Event | Use |
|---|---|
| `PhaseStarted(player, phase, time)` | a phase (cast, travel, impact, loop, telegraph) began |
| `SoundEvent(SpellSoundEvent)` | every sound start and stop (clip, volume, pitch, position): feed your own audio system when `playAudio` is off |
| `ScreenFlash(player, color, strength, frames)` | a spell asks for a full-screen flash (lightning, meteor). Nothing draws it for you outside the demo: draw a UI image with this colour for these frames, or ignore it |
| `StatusChanged(player, on)` | a status effect started or ended on its target |

Pooling: `SpellPlayer.Cast(..., pooled: true)` (the default) takes an instance from `SpellPool`.
`SpellPool.Prewarm(definition, count)` creates instances ahead of the first cast (do it on a loading screen: the
first cast of a spell otherwise instantiates its prefab). `SpellPool.Release(player)` returns one by hand;
`StopImmediate()` cuts an effect without its fade.

## 6. Sound

Every spell has its sounds as `AudioSource`s inside the prefab, placed on the release frame and on the contact frame.

- `playAudio = false` on an instance (or on the prefab) silences it; `SoundEvent` still fires, so you can play the
  clips through your own system.
- Audio mixer: select the AudioSources of a spell prefab and set their Output to your mixer group. For all spells at
  once, search the Project for `t:Prefab` under `Assets/RealSpellsDemo/Spells` and set the Output in the multi-selection.
- Each sound has a spatial blend (`spatial` in the definition: 0 = 2D, 1 = 3D).

## 7. Changing a spell

A `SpellDefinition` is data: phases (`cast`, `travel`, `impact`, `loop`, `telegraph`), each with flipbook layers,
lights, sounds and a camera shake, plus `projectile` and `feel`. The prefab and the materials are built from it.

Safe to edit directly, no rebuild needed:

| What | Where |
|---|---|
| Colour of a layer | its material in `Spells/<id>/Materials/` : `_Tint` (HDR) |
| Brightness of a layer | the same material: `_Intensity` |
| Soft blending against geometry | `_Softness` |
| All spell lights | `SpellPlayer.LightScale` (static), or the Light components of the prefab |
| Sound volume / mixer | the AudioSources of the prefab |
| Target reaction, camera shake strength | `SpellReactive` (`knockback`, `flash`) and `SpellCameraShake.strength` |

Worked example, a green fireball: duplicate `Spells/fireball` (Ctrl+D on the folder), open the copy's
`Materials/`, set `_Tint` of each material toward green, and point the copied prefab's particle renderers at them.
Cast the copied prefab with `SpellPool.Get(prefab)` and `Cast(...)`.

Needs a rebuild (timings, sizes, speeds, which sheet a layer plays): edit the spell's recipe and build it again
with `Tools > Real Spells Demo > Build Spell From Folder...`. The package ships one complete example recipe in
`Documentation/ExampleRecipe/` (`spells/example_shadow_bolt/spell.json`, its sheets and sounds) and the format reference
`Documentation/SpellRecipe.md`. Run the menu command, choose `Documentation/ExampleRecipe/spells/example_shadow_bolt`:
the spell `Spells/example_shadow_bolt/` appears with its prefab, definition and materials. Copy the example folder, change
the numbers in `spell.json`, build again: that is how a new spell is made without code. The `feel` block of the
recipe holds the timing and the reaction: cast time, travel speed, hit-stop, knockback, target flash, light spike,
telegraph, reaction.

## 8. Quality tiers and build size

Every flipbook ships in three tiers cut from the same frames:

| Tier | Frame size | Sheet files | In the package (PNG) |
|---|---|---|---|
| High | 512 px | 90 | 86 MB |
| Standard | 256 px | 80 | 30 MB |
| Mobile | 128 px | 80 | 10 MB |

- `Tools > Real Spells Demo > Quality Tier > High / Standard / Mobile` sets the tier of the whole package: it rewrites the
  sheet data of every definition and the textures of every spell material. `Settings/SpellQualitySettings.asset`
  shows the tier in use.
- One spell can keep its own tier: select its definition, set "Quality tier" at the top of the Inspector
  (Default = follow the package).
- Check: select a material under `Spells/<id>/Materials` - its texture is `sheet.png` (High), `sheet_256.png`
  (Standard) or `sheet_128.png` (Mobile).
- **Player builds.** Unity puts into a build only what the build's scenes and prefabs reference. The spell
  materials reference the sheets of the tier in use, so the other two tiers stay out of the build, and so do the
  spells you never reference. Compressed sheets are nearly all of that size: a Windows player that references every effect of the pack at High carries the High sheets as compressed GPU textures (several times their PNG size); Standard is about a quarter of that and Mobile about a sixteenth.
  A game that uses ten spells carries about a seventh of that. High is for desktop games that show the effects
  large on screen; Standard is the sensible default for most games; use Mobile for mobile and web builds.

## 9. Demo content

`Assets/RealSpellsDemo/Demo/ArenaDemo.unity` is the spell browser on its stage, with its scripts in `Demo/Scripts`
(`SpellDemoBrowser`). It is demo content only: delete `Assets/RealSpellsDemo/Demo` when you do not need it.

## 10. Known limits

- URP only (Unity 6000.3, URP 17.3). Built-in and HDRP are not supported.
- The effects are flipbook sprites with real point lights. They are not volumetric simulations, they do not cast
  shadows and they do not light each other.
- Only projectiles test colliders (section 4).
- The hit flash of `SpellReactive` is smoothly shaded for its 2-3 frames (flat-shaded toon models turn smooth for
  that moment) and has no DepthNormals pass: with a renderer feature that needs the normals texture (SSAO set to
  Depth Normals) the flashed target is missing from that texture for those frames.
- Forward+ rendering was not evaluated; the pack is tested with the Forward renderer.
- Held `stream` layers (flame, acid and shard streams) are tuned to be released within about a second: held longer,
  their particles fade. Sprites that fly along their velocity are rotated, not mirrored.
- Tested on Windows (editor and standalone player, Direct3D 11). Other platforms are not validated.
- The demo browser's keys use IMGUI key events; the on-screen buttons always work.

## 11. Support

Questions and bug reports: cgteamorg@gmail.com. Please include your Unity and URP version, the package version
(1.0.0) and the spell id.
