# Real Spells Demo - quick start

Version 1.0.0. Package root: `Assets/RealSpellsDemo`. The full manual is `Documentation/Manual.md`.

**Requirements:** Unity 6000.3 with the Universal Render Pipeline (URP 17.3). URP only: in a Built-in or HDRP project
the materials are pink. The project needs a URP asset assigned in Project Settings > Graphics (a project made from
Unity's "Universal 3D" template has one). The package depends on nothing else.

Every spell is a complete effect: a cast on the caster's hand, a projectile, beam or strike, an impact with a hit
flash, hit-stop and camera shake, real point lights and sounds. All spells are ready-made prefabs; nothing is
generated at run time.

This free pack holds 10 Realistic spells of the Real Spells series. The full packs carry 48 spells
and 24 magic circles each: Real Spells: Realistic, Real Spells: Hand-Painted and Real Spells: Pixel Art. This pack has
its own namespace and folder, so it can stay in a project next to any of them.

## See it in one minute

1. Open `Assets/RealSpellsDemo/Demo/ArenaDemo.unity` and press Play.
2. Use the on-screen buttons: `< Prev`, `Cast`, `Next >`, `Auto play`. Keys do the same: Left / Right (or A / D)
   select a spell, Space casts it, P toggles auto play.

## Cast a spell from your code

```csharp
using RealSpellsDemo;

public class Wizard : MonoBehaviour
{
    public SpellDefinition fireball;   // Assets/RealSpellsDemo/Spells/fireball/fireball.asset
    public Transform hand;
    public Transform enemy;

    void Attack()
    {
        SpellPlayer spell = SpellPlayer.Cast(fireball, hand, enemy);   // pooled, one line
        spell.Contacted += (player, point) => Debug.Log("hit at " + point);
    }
}
```

- `SpellPlayer.Cast(definition, origin, target)` casts at a transform; `Cast(definition, origin, point)` at a point.
- Events on the returned instance: `Released` (the projectile leaves the hand), `Contacted` (it reaches the target),
  `Finished` (everything faded; pooled instances return to the pool).
- Beams, wards and statuses run until you call `StopLoop()` on the instance.
- Add `SpellReactive` to an enemy to make it flash and rock back when hit; add `SpellCameraShake` to the camera.
- Spell lights too dim or too strong for your scene: set the static `SpellPlayer.LightScale` once (default 1); it multiplies every spell light and leaves the spell assets alone.
- No code: put `SpellCaster` on a character, assign a spell and a hand, call `CastAt(target)` from your input.

## Quality tiers and build size

`Tools > Real Spells Demo > Quality Tier` switches every spell between High, Standard and Mobile sheets (512, 256 and 128 px
frames). A spell's own tier (the "Quality tier" field at the top of its definition's Inspector) overrides the package
setting. A player build contains only the sheets of the tier in use, and only of the spells your scenes and prefabs
reference: the other tiers and unused spells stay out of the build. The package ships set to High. For scale:
a Windows player that references every effect of the pack at High carries the High sheets as compressed GPU textures (several times their PNG size); Standard is about a quarter of that and Mobile about a sixteenth. Pick Standard or Mobile before you build unless you need High.

## Folders

| Folder | What | Safe to delete |
|---|---|---|
| `Spells/<id>/` | one spell: definition asset, prefab, materials | a spell you do not use |
| `Sheets/`, `Audio/` | flipbook sheets (all tiers) and sounds shared by the spells | no |
| `Runtime/`, `Editor/`, `Shaders/` | the engine (namespace `RealSpellsDemo`) | no |
| `Settings/` | shader set and quality settings | no |
| `Demo/` | the demo scene, its stage and its scripts | yes, the whole folder |
| `Documentation/` | this file, the manual, the recipe reference with an example recipe, the guide for AI agents | yes |

Several Real Spells packages (styles and the free Demo) can share one project: each has its own root folder,
namespace, assemblies, shader names and GUIDs.

More: `Documentation/Manual.md`, `Documentation/SpellRecipe.md`, `Documentation/AGENTS.md` (for AI coding agents),
`Documentation/CHANGELOG.md`.

Support: cgteamorg@gmail.com - please include your Unity and URP version and the spell id.
