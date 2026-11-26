# Real Spells Demo - spell recipe reference

A spell of this pack is built from a recipe: a folder with `spell.json`, flipbook sheets and WAV files. `Tools > Real Spells Demo > Build Spell From Folder...` builds the spell (prefab, definition, materials, imported sheets of every tier, sounds) into `Assets/RealSpellsDemo/Spells/<folder name>/`. The package ships one complete recipe to start from: `Assets/RealSpellsDemo/Documentation/ExampleRecipe/` (`spells/example_shadow_bolt/spell.json`, `sheets/`, `audio/`).

Part 1 is the `spell.json` format, part 2 the sheet format.

---

# Part 1. `spell.json` schema (Real Spells, schema 1)

One spell = one recipe folder with `spell.json` (the shipped example: `Documentation/ExampleRecipe/spells/<id>/`).
The editor builder (`Tools > Real Spells Demo > Build Spell From Folder...`) turns it into a prefab and a `SpellDefinition` asset.
A new spell is a new JSON file, never new code.

## Conventions

- Units: metres, seconds, degrees. Axes (engine-neutral): **+Y up, +Z forward** (from caster to target), +X right.
- Colours: sRGB 0..1 triples `[r, g, b]`. HDR brightness is a separate `intensity` multiplier, never a colour > 1.
- **Range**: a number `0.8` or `[min, max]` (uniform random per particle / per cast).
- **Curve**: a number (constant) or a list of keys `[[x, y], [x, y], ...]`, x ascending, linear between keys, held
  flat outside. On `once` layers x is the **normalized particle life 0..1**; on `loop` layers and on lights x is
  **seconds since the layer / light started** (`sizeCurve` is ignored on `loop` layers).
- Paths are relative to the spell folder (`../` allowed). A sheet reference without `/` is a bare name, looked up in
  `<spell>/sheets/<name>/` first, then `<style>/sheets/<name>/`.
- Unknown keys are ignored with a log warning (`RS|LOAD|WARN|unknown key ...`); a missing required key fails the load.

## Top level

| Key | Type | Req | Meaning |
|---|---|---|---|
| `schema` | string | yes | `"realspells.spell/1"` |
| `id` | string | yes | folder name, `snake_case` |
| `name` | string | yes | display name (English) |
| `style` | string | yes | `realistic`, `toon` or `pixel` |
| `seed` | int | no | random seed of the preview (default 1) |
| `projectile` | object | no | how the spell travels; omit for spells that appear at the target (no `travel` phase) |
| `phases` | object | yes | `cast` (required), `travel`, `impact`, `loop`, `telegraph` (all optional) |
| `feel` | object | no | timing and reaction (section "Feel"); omitted = the defaults of the spell's class |
| `archetype` | string | no | `bolt`, `strike`, `nova`, `beam`, `ward`, `status` (`docs/archetypes.md`): preview shots, holds and the archetype timing tests. Omitted = inferred for shots only (no archetype tests) |

### `projectile`

| Key | Default | Meaning |
|---|---|---|
| `speed` | 14 | m/s |
| `arc` | 0 | height of a parabola at mid-flight, metres (0 = straight) |
| `radius` | 0.25 | hit sphere radius, m (sphere-cast against colliders; reaching the target point also hits) |
| `maxRange` | 40 | m; at max range the spell impacts in the air |
| `spin` | 0 | deg/s roll of the projectile anchor (rotates `local` travel layers) |
| `curve` | 0 | sideways bend of the flight at mid-flight, metres, signed (+ = to the caster's right along the flight); the same parabola as `arc`, in the horizontal plane. 2D side view: drawn as height (no depth to bend into). Test `arch_bolt_curve`: the measured bend is within 15 % + 0.05 m of `curve` |
| `curveRandomSign` | false | every cast picks the side of the bend at random (seeded per cast): arcane missiles that swerve left and right |

## Phases

Order: `cast` starts at t = 0 at the caster's hand. After `cast.duration` the projectile launches and `travel` runs
until the hit; then `impact` plays at the hit point. Without `travel`, `impact` starts at the target right after
`cast.duration`. `loop` (auras, shields, channelled beams) starts together with `cast` at the caster and runs until
the game stops it, or for `loop.duration` seconds when given. Layers that are still alive when their phase ends finish
their own life (one-shot) or fade out (loop layers, `fadeOut`).

Archetype additions (2026-11-13, `docs/archetypes.md`), all optional and without effect on spells that do not use them:
- `telegraph` (Strike): starts on the release on the ground under the target and lasts `feel.telegraph` s (or its own
  `duration`); then the impact lands on the target. Its `impact`-anchored layers sit on a fixed ground mark. With a
  `travel` phase and `feel.fall` > 0 the travel layers fly from the sky to the target during the last `fall` s.
- `loop.start`: `"cast"` (default) or `"release"` (default for the `beam` and `status` classes): the loop starts on
  the release frame, after the impact (beams hold from the hand to the target, statuses ride on the target).
- `feel.class: "area"` (Nova): the impact phase starts at the caster's feet on the release; the contact (sounds
  `at: "contact"`, hit-stop, reaction) comes when the first `ring` layer's radius (or a forward wave) reaches the target.

| Key | Type | Meaning |
|---|---|---|
| `duration` | number | `cast`: seconds until launch (**required**). `impact`: seconds until the spell returns to the pool (default: the end of its last layer / light / sound). `loop`: optional run time. Ignored on `travel` (flight time decides). |
| `layers` | list | particle layers (below) |
| `lights` | list | real lights driven by curves (below) |
| `sounds` | list | sound events (below) |
| `shake` | object | camera shake impulse (below), optional |

Anchors (`anchor` key on layers, lights, sounds): `caster` (caster root on the ground), `hand` (cast point),
`projectile`, `impact` (hit point, up = surface normal), `target` (follows the target through its knockback: status
layers), `floor` (the hit point projected down to the floor: shockwave rings with `orient: ground`, dust billboards).
`casterBody` / `targetBody`: the body centres (the stage's body transforms; else the caster + 1 m and the target
point), following recoil and knockback (wards, statuses).
Defaults: `cast` -> `hand`, `travel` -> `projectile`, `impact` -> `impact`, `loop` -> `caster`, `telegraph` -> `impact` (the ground mark).

## Layer

| Key | Default | Meaning |
|---|---|---|
| `name` | required | unique within the phase |
| `sheet` | required | sheet folder (holds `sheet.json`, `sheet.png`, optional `sheet_mv.png`) |
| `blend` | sheet's `blend.mode` | `additive` (sparks, light), `alpha` (smoke, debris, scorch), `glow` (fire: one material that adds light and occludes; `premultiplied` is accepted as an alias). Equations: `docs/sheet-schema.md` section 2 |
| `occlusion` | sheet's `blend.occlusion` or 0.5 | `glow` only: 0 = additive, 1 = alpha |
| `playback` | `once` | `once`: the sheet's schedule plays once over the particle life. `loop`: the sheet loops at its own frame times x `speed`. `segments` (sheets with `segments`, e.g. magic circles): `appear` once, then the `loop` segment repeats while the phase runs (a channelled cast holds it), then `activate` + `vanish` play when the phase ends (launch, or `StopLoop` for the loop phase) |
| `speed` | 1 | playback rate of `loop` sheets; for `once` sheets with no `life` it scales the sheet's own duration |
| `life` | sheet duration | Range, seconds. `once`: required life of one particle (default = sheet duration / speed). `loop`: lifetime of the layer; omitted = until the phase ends |
| `fadeIn` / `fadeOut` | 0 / 0.2 | `loop` layers only: seconds of alpha ramp at start and at phase end |
| `randomStart` | true | `loop` layers: start each particle on a random frame |
| `anchor` | phase default | see anchors |
| `offset` | `[0,0,0]` | metres in anchor space |
| `space` | `world` (`local` for loop layers) | `local` particles move with the anchor; `world` particles stay where they were born |
| `orient` | `billboard` | `billboard` (faces camera), `ground` (flat, world up), `velocity` (stretched along motion), `beam` (a strip from the anchor to the impact point, turned around its axis to face the camera; `size` = width; playback `loop`, or `once` for an end strip), `ring` (an upright band on the floor growing outward: `size` = diameter, grows by `sizeCurve`; seen from a low game camera) |
| `when` | `start` | `start` with the phase, `hit` on every `SpellPlayer.HitWard` (ward hit flash at the hit point), `end` when the phase ends (ward break, status end, beam end strip) |
| `tile` | beam: 2 x width; ring: 3 | beam: metres of strip per repeat of the sheet; ring: repeats around. A sheet with `"tile": {"axis": "x", "period": px}` in its sheet.json sets beam `tile` = width x period / content px |
| `scroll` | beam 2 (0 with a self-tiling sheet) | beam: repeats per second the strip moves toward the target |
| `reach` | 0 | beam: seconds the strip takes to grow from its anchor to the impact point |
| `widthCurve` | 1 | beam strip and `beam` spawns: width (size multiplier) along the beam, 0 = anchor, 1 = impact (e.g. `[[0, 0.3], [1, 1.2]]` widens toward the target) |
| `flicker` | beam 0.25, else 0 | loop layers: share of random intensity flicker |
| `ringHeight` | 0.6 | ring: band height in metres |
| `mirror` | false | every particle mirrored left-right (pixel art moving toward -x) |
| `behind` | false | drawn after the 2D stage's backdrop and before its characters (marks, craters, stains, back waves); `order` -15..15 sorts among them (queue 2942 + order) |
| `stretch` | 0.1 | `velocity` only: extra length per m/s |
| `spawn` | `{ "mode": "single" }` | see spawn |
| `size` | 1 | Range, metres: the **visible** size = the larger side of the sheet's `extent` box (largest frame). Without `extent` it is the full quad width. The quad is placed so the sheet's `pivot` sits on the spawn point |
| `sizeCurve` | 1 | Curve over life, multiplier |
| `rotation` | `[0, 360]` | Range, degrees at birth (`0` for sheets that must stay upright, e.g. a column of fire) |
| `spin` | 0 | Range, deg/s |
| `velocity` | none | see velocity |
| `tint` | `[1,1,1]` | sRGB multiplier of the sheet colour |
| `intensity` | 1 | Curve over life, HDR multiplier on top of the sheet's `blend.intensity` (final = sheet x layer; values above 1 drive bloom) |
| `alpha` | 1 | Curve over life, opacity multiplier |
| `lingering` | impact layers that are lit or named `smoke` / `dust` | smoke that hangs after the hit. The preview starts the next close-up cast only after every lingering layer's alpha curve is below 0.2 (wide shots may overlap); keep it for slow smoke, set `false` for fast puffs |
| `depthOffset` | impact/target billboards >= 1.5 m: `min(0.6, 0.2 x size)`, else 0 | metres the quad is pulled toward the camera along the view ray (it covers the same pixels). An explosion on a dummy then sits in front of the body and the soft fade melts the body into it, instead of the body poking through with a hard edge |
| `depthOffset` < 0 | | allowed since 2026-11-13 (-2 .. 2): the quad is pushed AWAY from the camera by that many metres, so a billboard sits behind the body it bursts on (a geyser column behind the dummy). `0` switches the automatic pull off. Before that date a negative value was clamped to 0, so old recipes with a negative value now really go behind: check them once |
| `softness` | 0.3, billboards `max(0.3, 0.12 x size)` | soft depth fade in metres: the sprite fades where it comes within this distance of the geometry behind it (a 3.4 m explosion through a dummy fades over 0.41 m instead of cutting it with a hard line). 0 = hard edge; keep 0 only for ground decals and pixel art |
| `lit` | 1 if the sheet's `color.content` is `lit`, else 0; always 0 for style `pixel` (palette kept) | 0 = emissive/unlit; 1 = colour multiplied by scene ambient + main light (smoke, dust, debris read on a bright day) |
| `align` | `true` when the sheet has `forward` | follow the anchor's travel direction on screen. `"free"`: rotate the quad by any angle so the sheet's `forward` points along the travel. `"mirror"`: never rotate, only mirror the sprite so `forward` points the travel's way (horizontal `forward` mirrors left/right, vertical up/down). `"snap90"`: rotate in 90-degree steps only. `false`/`"none"`: off. `true` means `"free"` on smooth layers and `"mirror"` on point-filtered (pixel) layers; `"free"` on a pixel layer logs a warning. Pixel layers attached to a moving core (tail, core) use `"mirror"` or `"snap90"` so no pixel is ever resampled |
| `aspect` | 1 | `orient: billboard` only: width / height of the quad. 2 = twice as wide as tall (flat low fog, ground mist), 0.5 = twice as tall as wide. The longer side keeps `size`. The squash turns with the quad, so use `rotation: 0` (and `spin: 0`) for flat puffs; `spawn.flip` still mirrors them. Trail, ring and front cards stay square |
| `orient: velocity` and the sheet's `forward` | | a stretched billboard lies along its velocity with the texture's **-u** end leading (Unity). Since 2026-11-13 the engine reads the sheet's `forward`: `[1, 0]` (drawn flying to +u) is mirrored along the stretch axis so the head leads, `[-1, 0]` is left as it is; `spawn.flip` then only mirrors across the axis (v). Recipes that mirrored by hand with `spawn.flip: 1` look the same as before and may drop the flip. A sheet without `forward`, or with a vertical one, is laid as before (`spawn.flip` on both axes) |
| `filter` | `point` for style `pixel`, else `linear` | `point` = nearest sampling, no frame blending (pixel art) |
| `order` | 0 | draw order inside the spell (higher draws later / on top) |

### `spawn`

| `mode` | Keys | Meaning |
|---|---|---|
| `single` | `delay` | one particle |
| `burst` | `count` (Range), `delay`, `radius` | `count` particles at once inside a sphere of `radius` m |
| `trail` | `spacing`, `delay`, `jitter`, `lateral`, `sizeJitter`, `drift`, `inherit` | one particle about every `spacing` metres the anchor moves (use on `projectile`), see trail keys |
| `rate` | `rate`, `duration`, `delay`, `radius` | `rate` particles/s for `duration` s (default: until the phase ends; required outside `travel`/`loop`) |
| `beam` | `rate`, `duration`, `delay`, `lateral` | `rate` particles/s at random spots along the beam from the layer's anchor to the impact point (puffs, motes along a Beam); size x the layer's `widthCurve` at the spot |
| `ring` | `rate`, `duration` (required), `radius` (required), `falloff`, `delay` | the Nova wall: `rate` upright cards/s on a circle around the anchor whose radius grows `radius x (1 - (1 - t/duration)^2)`; a card's size x lerp(1, `falloff` (0.5), radius share). 2D side view: the two fronts (left and right) only. Also sets the Nova's contact time |
| `front` | `density` (cards per metre of the front, 4), `band` (0.15 m), `ride` (1), `duration` and `radius` (required), `falloff`, `delay` | the Nova wall as cards on the **current wave front only**: the front radius is the phase's `ring` layer's drawn radius when it has one, else `radius x (1 - (1 - t/duration)^2)`; cards are born in a band of `band` m around it, `density` x circumference cards are alive on the front (2D: the two fronts), a card's size x lerp(1, `falloff`, radius share). Give the layer a short `life` (0.1-0.4 s): each card dies just behind the front, so the wall travels and the disc behind it stays clear (a parse warning above 0.5 s). Every frame a live card moves out with the front by `ride` x the front's advance (1 = it rides the front and stays in its band, so a fast front leaves no trail; 0 = it stays where it was born and trails behind). Also sets the Nova's contact time. Test `arch_nova_front`: every card within the band around the current front (ahead <= band/2 + 0.05 m, behind the same plus the advance it does not ride), cards alive / (density x circumference) 0.6-1.4 once the front is past 1 m. Engine fix 2026-11-13: before it, front layers emitted nothing |
| `stream` | `rate`, `speed` (m/s, 20), `lateral`, `sizeJitter`, `duration`, `delay` | a stream of separate particles that fits any cast distance (since 2026-11-13): each is born at the layer's anchor (the hand), flies straight to the far end (the impact point, before the impact the target) at `speed` and dies there - its life is distance / speed, the layer's `life` is not used. On the first frame the whole length is already filled with `rate x distance / speed` particles. `rate` = particles per second (per metre: rate / speed). With `orient: velocity` the particles point along the stream (stones, drops, shards); `velocity` on the layer is ignored; `widthCurve` scales the size at birth. Use it in the loop phase of a Beam (`loop.start: release`). `mode: beam` is different: puffs appear at random spots along the beam and stay where they were born |

`delay` is seconds after the phase start (default 0). Default `radius` 0.

Keys that break up "a chain of identical stamped beads" (schema 1, added 2026-11-12 21:40; defaults in brackets are
for `trail`, every other mode defaults them to 0):

| Key | Default | Meaning |
|---|---|---|
| `jitter` | 0.3 | trail: +- share of random spacing (each step is `spacing x (1 +- jitter)`) |
| `lateral` | 0.08 | trail: random offset in metres across the path |
| `sizeJitter` | 0.25 | trail: +- share of random size on top of `size` |
| `drift` | 0.25 | trail: upward drift in m/s added to every particle (smoke and embers rise) |
| `inherit` | `velocity.inherit` | trail: share of the projectile's velocity a particle keeps at birth (0.05-0.2, with `velocity.drag` so it slows down) |
| `flip` | 0.5 (trail), 0 | any mode: share of particles mirrored on each screen axis (with a random `rotation` this hides the repeat) |
| `startOffset` | 0 | any `once` layer: seconds of random start into the sheet's schedule (a puff can be born a little older); capped at 25 % of the sheet; the end of the life stays the end of the schedule |

Recipes:
- **Trail that does not read as beads**: `spawn: {mode: trail, spacing: 0.25, jitter: 0.35, lateral: 0.1, sizeJitter: 0.25,
  flip: 0.5, startOffset: 0.08, drift: 0.3}`, `rotation: [0, 360]`, `size: [0.5, 0.8]`, `sizeCurve: [[0, 0.7], [1, 1.8]]`,
  `alpha: [[0, 0], [0.1, 1], [1, 0]]`, `velocity: {speed: [0.1, 0.4], direction: outward, inherit: 0.1, drag: 1.5}`.
- **Impact smoke that billows outward and thins** (not a tower): several puffs, not one tall sprite -
  `spawn: {mode: burst, count: [5, 8], delay: 0.1, radius: 0.5, flip: 0.5}`, `rotation: [0, 360]`,
  `velocity: {speed: [0.8, 1.8], direction: up, spread: 140, drag: 1.6}`, `sizeCurve: [[0, 0.5], [0.4, 1.4], [1, 2.2]]`,
  `alpha: [[0, 0], [0.08, 0.85], [0.6, 0.5], [1, 0]]`, `lit: 1`. A single `size: 4` particle with no velocity, no drag and
  no growth only plays the sheet as filmed: if the clip is a rising column, the result is a column that leaves the frame.
  Sheets meant for this should be one puff that swells and thins in place (pivot near its base).

### `velocity`

| Key | Default | Meaning |
|---|---|---|
| `speed` | 0 | Range, m/s at birth |
| `direction` | `outward` | `outward` (random over a sphere), `up`, `forward`, `back`, or a vector `[x,y,z]` in anchor space |
| `spread` | 30 | cone angle in degrees around `direction` (ignored for `outward`) |
| `inherit` | 0 | 0..1 share of the anchor's velocity (trails behind a projectile: 0.1-0.2) |
| `gravity` | 0 | multiplier of Earth gravity (1 = falls like a stone, negative rises) |
| `drag` | 0 | per-second damping 0..10 |

## Light

| Key | Default | Meaning |
|---|---|--- |
| `anchor` / `offset` | phase default / `[0,0,0]` | where the point light sits (it follows a moving anchor) |
| `color` | `[1,1,1]` | sRGB |
| `range` | 6 | metres |
| `intensity` | required | Curve over seconds since the light started; value = the engine's point-light intensity (Unity `Light.intensity`, URP, linear). Every spell light is multiplied by the scene-wide `SpellPlayer.LightScale` (static, default 1): a stage or a game darker or brighter than the day arena the lights are tuned on sets it once instead of editing recipes. |
| `delay` | 0 | seconds after phase start |
| `duration` | curve end | seconds; on `travel` / `loop` lights omitted = until the phase ends, holding the last curve value, then `fadeOut` |
| `fadeOut` | 0.15 | seconds |
| `flicker` | 0 | 0..1 random intensity flicker (fire 0.15-0.3, magic 0-0.1) |
| `shadows` | false | real-time shadows from this light (expensive, hero shots only) |

## Sound

| Key | Default | Meaning |
|---|---|---|
| `clip` | required | WAV path relative to the spell folder, or a list of paths (one picked at random per event) |
| `delay` | 0 | seconds after phase start |
| `volume` | 1 | 0..1 |
| `volumeJitter` | 0 | +- random share of the volume (0.1 = +-10 %) |
| `pitch` | 1 | playback rate |
| `pitchJitter` | 0 | +- random share of the pitch (0.05 = +-5 %, about +-1 semitone) |
| `loop` | false | loop the clip until the phase ends, then fade out over `fadeOut` |
| `fadeOut` | 0.15 | seconds |
| `anchor` | phase default | where the sound plays |
| `spatial` | 0.7 | 0 = 2D, 1 = fully 3D |
| `clickAt` | from the audio provenance | cast sounds: seconds into the clip of its release click (a number, or one per clip when `clip` is a list). The clip then starts `release - clickAt / pitch` so the click lands on the release frame (clip, volume and pitch are drawn when the cast starts). Absent: read from `provenance_v3.json` / `_v2` / `provenance.json` next to the clips (`release_transient_from_file_start_ms` or `release_s` of the clip's entry); not found = the clip starts with the phase. Test `timing_cast_click` |
| `at` | `phase` | `phase`: phase start + `delay`. `release`: the release frame + `delay` (cast whoosh transients). `contact`: the contact frame + `delay`. `hit`: every ward hit (`SpellPlayer.HitWard`) + `delay`. `end`: the end of its phase + `delay` (ward break, status end). Travel sounds start on the release frame and impact sounds on the contact frame anyway; the hit-stop never delays a sound that has started |

WAV: PCM 16/24-bit or float 32, mono or stereo, 44.1 or 48 kHz. A spell without at least one sound in `cast` and one in
`impact` is reported as unfinished by the engine tests.

## Feel (timing and reaction)

Every spell now has a short cast that snaps, a fast
projectile, a hit-stop on contact, a target that flashes and rocks back, camera shake and light spikes. The `feel`
block sets them; omitted keys take the defaults of the spell's `class`. The runtime applies them to every spell in the
library, with or without a `feel` block.

| Key | Default (projectile) | Meaning |
|---|---|---|
| `class` | from the structure | `projectile` (travel phase), `instant` (impact at the target, no travel), `area`, `beam`, `channel` (a cast phase with a `segments` circle, wards, or cast-only spells: the authored cast time is kept), `status` (castTime 0.32, hitStop 0.04, knockback 8, flash 2 frames, the `reaction` on) |
| `castTime` | 0.35 | seconds from the cast start to the release; an authored `cast.duration` within 0.1 s of it is kept (sprite release frames are timed to it). Otherwise the authored `cast` phase is time-scaled to it (layer delays and lives, light curves, sound delays); `phases.cast.duration` stays the authoring length |
| `castEase` | 1.6 | exponent on the cast layers' schedule time: the gather starts slow and accelerates into the release |
| `snap` | `{scale: 1.3, time: 0.05, light: 2}` | release pop: cast layers scale x`scale` for `time` s, cast lights flare to x`light` of their peak. A cast layer that ends at the release lives `2 x time` longer so the pop shows |
| `recoil` | `{distance: 0.065, time: 0.25}` | the caster kicks back `distance` m from the target on the release and settles on a damped spring in `time` s |
| `travelSpeed` | 27 | projectile cruise speed (m/s) along the straight line from the hand to the target, replaces `projectile.speed`; 12 m in about 0.45 s. `projectile.arc` and `curve` bend the path without changing the flight time, so the speed along the path is higher (arc 2.4 m over 12 m: x1.2); the test `arch_bolt_speed` (22-30 m/s) measures along the straight line since 2026-11-13 |
| `travelEase` | 0.06 | seconds: the projectile leaves the hand at 35 % speed and reaches cruise speed with this time constant. It spawns on the release frame |
| `hitStop` | 0.075 | seconds the spell (layers, its clock, the target's motion) freezes on the contact frame; sounds keep playing |
| `impactPeak` | 0.09 | seconds after the hit-stop at which the impact should peak (sheets are cut to peak by then; the preview paces casts on it) |
| `shake` | `{impact: 0.6, time: 0.25, release: 0.15}` | camera shake in degrees on contact (decaying over `time`) and a small kick on the release. A `shake` block on the impact phase replaces `impact` (its `amplitude` = spell strength) and starts on the contact frame |
| `targetFlash` | `{frames: 3}`, colour = halfway from white to the first impact light's colour | the hit object's renderers lean toward `color` for `frames` rendered frames and then ease out over 0.10 s. It is a tint, not a silhouette: the model keeps its base map, colour and shading (main light, ambient, per-object lights) and at the peak 60 % of it is replaced by the flash colour on the same lit form (`SpellReactive.FlashPeakAmount`, `FlashEase`). `color: [r, g, b]` fixes the colour; number = frames, `false` = off. Before 2026-11-13 the renderers turned flat white. A flash never renders darker than the model was: under daylight the result is lifted to the model's own brightness plus a share of the amount (about +20 % at the peak), and the target keeps casting its shadow during the flash (`RealSpellsDemo/Hit Flash` has a ShadowCaster pass). Checked on flat toon materials by day and on a skinned character with several materials and a textured face. The pixel 2D stage keeps its white flinch drawing (a pixel-art convention) |
| `knockback` | `{angle: 20, time: 0.7}` | the hit object rocks back `angle` degrees around its pivot on a damped spring and wobbles out in `time` s (starts after the hit-stop) |
| `lightSpike` | `{factor: 1.4, time: 0.08}` | impact lights jump to `factor` x their curve peak on the contact frame for `time` s, then follow their curve. 2-3x (the first proposal) blew the frame to white under bloom: 1.4 measured best |
| `status` | none | name of an impact layer that sticks to the target (burn, frost): its anchor becomes `target`, space local, life `{layer, life: 1}` |
| `screenFlash` | instant: `{strength: 0.35, color: [0.85, 0.92, 1], frames: 2}`, others off | full-screen additive flash on the contact frame for 1-3 rendered frames (lightning). Number = strength. Fired as `SpellPlayer.ScreenFlash`; the stage draws it |
| `props` | `{shake: 0.03, radius: 4}` | reactive props (component `SpellReactive` without knockback) within `radius` m shake up to `shake` m |
| `telegraph` | 0 (0.42 when a `telegraph` phase exists) | Strike: seconds from the release to the strike (0.35-0.50) |
| `fall` | 0 | Strike with a falling body: seconds the travel layers fall from the sky before the contact (0.17-0.25); 0 = an eruption or channel that lands at once (main impact layer peaks <= 0.12 s) |
| `reaction` | status class: `{tint: [0.55,0.8,1], tintAmount: 0.45, shiver: 0.012, stiffness: 0.6}`, others off | Status: from the contact to the loop end the target leans toward `tint` by `tintAmount`, shivers by `shiver` m, and `stiffness` (0..1) damps knockbacks (3D) / slows the 2D dummy's animation |
| `reaction.kind` (or `reaction: "<kind>"`) | `harm` | `harm`: the contact flinches the target (flash, knockback, hit-stop, shake). `none`: no reaction at all. `benefit`: no hit reaction; a status brightens the target (its tint is applied as a screen blend). Status tests check that a `none` / `benefit` spell causes 0 hit reactions and no hit-stop; the preview's `timing_hitstop` and `reaction_target` follow the kind (wanted: no hit-stop, no tilt, no flash), so a beneficial spell does not need hitStop 0 / targetFlash false / knockback 0 written out |
| `reaction.tintMode` | screen for `benefit`, multiply for a dark tint (luminance < 0.5), lerp otherwise | how the status tint is applied to the target: `lerp` toward the tint, `multiply` (a dark tint darkens: curses, poison), `screen` (brightens: blessings). Same on the 2D dummy and on 3D targets |
| `wardFlash` | `{boost: 1.5, time: 0.15}` | Ward: the loop layers brighten by 1 + boost on a hit and fall back over `time` s |

Class defaults that differ from `projectile`: `instant` castTime 0.3, hitStop 0.06, shake 0.5, knockback 18;
`area` castTime 0.28 (Nova gather <= 0.30; was 0.45 before 2026-11-13), hitStop 0.05, shake 0.9 over 0.35 s, knockback 25, props 0.05 m within 6 m; `beam` castTime 0.4,
no hit-stop, shake 0.25, knockback 10, flash 2 frames; `channel` keeps the authored cast time.

Targets react through the `SpellReactive` component (flash, knockback, shake; the rest pose is restored exactly). The
player uses the one on the hit collider's parents, else `SpellPlayer.reactionTarget`, else the target transform's parents.

### Hit filter (`SpellPlayer.ignoreRoot`)

What a cast can touch is decided in one place, `SpellPlayer.Ignores(Collider)`: never the spell's own colliders, never
the children of the `caster` transform given to `Cast`, and never anything under `ignoreRoot` (a `Transform`, empty by
default). `ignoreRoot` is for a caster whose bodies are not children of the caster transform: a ragdoll that lives
beside the character, a shield object, a mount.

- Where it applies: the projectile sweep (Bolt and every flying projectile; `SweepHit(from, dir, dist, out hit)` is the
  same test, callable from game code) and the floor lookups of `anchor: "floor"` layers and world-space ground decals.
- Where there is nothing to filter: Beam, Nova (area), Strike, Ward and Status do not look for colliders at all; they
  address the target transform given to `Cast` (the beam's far end, the ring's contact time, the strike point). A
  game that wants a beam stopped by a wall does its own ray and casts at that point.
- Lifetime: the field stays on the instance. `SpellCaster.ignoreRoot` sets it on every cast. The static
  `SpellPlayer.Cast(definition, origin, target, ...)` clears it on the (pooled) instance it returns, so set it right
  after the call; the sweep starts at the release, never in the frame of the call:

```csharp
SpellPlayer p = SpellPlayer.Cast(fireball, hand, enemy, casterRoot);
p.ignoreRoot = ragdollRoot;   // the caster's own ragdoll bodies are flown through
```

Measured in the preview (fireball, all three styles, 60 fps capture): release and first projectile particle on the
same frame, contact and impact phase on the same frame, the impact sound event on the contact frame, cast -> release
0.350 s, flight 12 m 0.450 s, hit-stop 0.083 s (5 frames: 75 ms rounded up to whole frames), dummy tilt 19.3-19.8 deg,
flash 3 frames, caster recoil 0.064 m. Tests: `timing_release_spawn`, `timing_release_sound`, `timing_contact_impact`,
`timing_contact_sound`, `timing_hitstop`, `reaction_target`, `reaction_recoil`, `reaction_rest`.

```json
"feel": {"class": "projectile", "castTime": 0.35, "hitStop": 0.075, "shake": {"impact": 0.7, "time": 0.25},
         "knockback": 22, "status": {"layer": "burn", "life": 1.0}}
```

## Camera shake

| Key | Default | Meaning |
|---|---|---|
| `delay` | 0 | seconds after phase start |
| `amplitude` | 0.5 | peak rotation, degrees |
| `frequency` | 20 | Hz |
| `duration` | 0.4 | seconds (decays to zero) |
| `radius` | 25 | metres from the anchor at which the shake fades to nothing |

## How a layer reads a sheet

The format is described in part 2 of this file (`realspells.sheet/1`). The engine reads: `grid`,
`frameCount`, `tiers.<tier>.file/cell/gutter/mipMax`, `color.content`, `blend.mode/intensity/occlusion`,
`schedule.times/duration/loop/end`, `motionVectors` (file per tier, `scale`, `strength`), `pivot`, `forward`,
`extent`, `projection`. The texture is used as stored (straight alpha, sRGB, dilated colour; no premultiply on import);
the shader premultiplies. The schedule becomes a 512-entry time -> fractional-frame lookup texture, so the shader
blends neighbouring frames (warped by the motion vectors when present) at non-uniform times, loops close N-1 -> 0,
and one-shots hold the last frame. A one-shot layer's `life` stretches the whole schedule (`duration`) over the life.
The preview uses tier `high` unless told otherwise (`-Tier standard|mobile`).

## Sample: `realistic/spells/fireball/spell.json`

```json
{
  "schema": "realspells.spell/1",
  "id": "fireball",
  "name": "Fireball",
  "style": "realistic",
  "seed": 7,
  "projectile": { "speed": 15, "arc": 0.4, "radius": 0.3, "maxRange": 40 },
  "phases": {
    "cast": {
      "duration": 0.55,
      "layers": [
        { "name": "charge", "sheet": "fire_charge", "blend": "additive", "playback": "once", "life": 0.6,
          "size": 0.9, "sizeCurve": [[0, 0.3], [0.8, 1.0], [1, 0.6]], "rotation": [0, 360],
          "tint": [1.0, 0.75, 0.45], "intensity": [[0, 0.5], [0.7, 3.0], [1, 0.0]] },
        { "name": "embers_in", "sheet": "spark", "blend": "additive", "orient": "velocity", "stretch": 0.05,
          "spawn": { "mode": "rate", "rate": 40, "duration": 0.45, "radius": 0.5 },
          "life": [0.25, 0.4], "size": [0.04, 0.08],
          "velocity": { "speed": [0.5, 1.2], "direction": "outward" },
          "tint": [1.0, 0.6, 0.3], "intensity": 4, "alpha": [[0, 0], [0.2, 1], [1, 0]] }
      ],
      "lights": [
        { "color": [1.0, 0.55, 0.25], "range": 5, "intensity": [[0, 0], [0.45, 3], [0.6, 1.5]], "flicker": 0.2 }
      ],
      "sounds": [
        { "clip": "sfx/fireball_cast.wav", "volume": 0.8, "volumeJitter": 0.1, "pitchJitter": 0.05 }
      ]
    },
    "travel": {
      "layers": [
        { "name": "core", "sheet": "fire_core_loop", "blend": "glow", "occlusion": 0.2,
          "playback": "loop", "fadeIn": 0.05, "fadeOut": 0.1, "size": 1.0, "rotation": [0, 360], "spin": [-120, 120],
          "tint": [1.0, 0.8, 0.55], "intensity": 3.0 },
        { "name": "trail_fire", "sheet": "fire_puff", "blend": "additive", "space": "world",
          "spawn": { "mode": "trail", "spacing": 0.15 }, "life": [0.3, 0.45], "size": [0.5, 0.7],
          "sizeCurve": [[0, 1.0], [1, 1.6]], "velocity": { "speed": [0.2, 0.6], "direction": "outward", "inherit": 0.1 },
          "tint": [1.0, 0.65, 0.35], "intensity": [[0, 2.5], [1, 0.5]], "alpha": [[0, 1], [1, 0]] },
        { "name": "trail_smoke", "sheet": "smoke_puff", "blend": "alpha", "space": "world", "lit": 0.8,
          "spawn": { "mode": "trail", "spacing": 0.3 }, "life": [0.9, 1.3], "size": [0.6, 0.9],
          "sizeCurve": [[0, 0.8], [1, 2.2]], "velocity": { "speed": [0.3, 0.6], "direction": "up", "spread": 40, "drag": 1.5 },
          "tint": [0.25, 0.23, 0.22], "alpha": [[0, 0], [0.15, 0.7], [1, 0]] }
      ],
      "lights": [
        { "color": [1.0, 0.6, 0.3], "range": 7, "intensity": [[0, 0], [0.08, 4]], "flicker": 0.25, "fadeOut": 0.1 }
      ],
      "sounds": [
        { "clip": "sfx/fireball_travel_loop.wav", "loop": true, "volume": 0.6, "fadeOut": 0.15 }
      ]
    },
    "impact": {
      "layers": [
        { "name": "explosion", "sheet": "fire_explosion", "blend": "glow", "occlusion": 0.4,
          "playback": "once", "life": 0.9, "size": 3.4, "rotation": 0, "offset": [0, 0.6, 0],
          "tint": [1.0, 0.85, 0.7], "intensity": [[0, 4.0], [0.3, 2.5], [1, 0.8]] },
        { "name": "smoke", "sheet": "smoke_puff", "blend": "alpha", "lit": 0.8,
          "spawn": { "mode": "burst", "count": [5, 7], "delay": 0.12, "radius": 0.4 },
          "life": [1.4, 2.0], "size": [1.4, 1.9], "sizeCurve": [[0, 0.7], [1, 1.8]],
          "velocity": { "speed": [0.6, 1.4], "direction": "up", "spread": 70, "drag": 1.2 },
          "tint": [0.22, 0.2, 0.19], "alpha": [[0, 0], [0.1, 0.85], [1, 0]] },
        { "name": "sparks", "sheet": "spark", "blend": "additive", "orient": "velocity", "stretch": 0.04,
          "spawn": { "mode": "burst", "count": [18, 26], "radius": 0.2 },
          "life": [0.4, 0.8], "size": [0.05, 0.09],
          "velocity": { "speed": [4, 9], "direction": "up", "spread": 150, "gravity": 1.0 },
          "tint": [1.0, 0.65, 0.3], "intensity": [[0, 6], [1, 1]], "alpha": [[0, 1], [0.8, 1], [1, 0]] },
        { "name": "scorch", "sheet": "scorch_decal", "blend": "alpha", "orient": "ground", "offset": [0, 0.02, 0],
          "life": 4.0, "size": 2.4, "softness": 0, "tint": [1, 1, 1], "alpha": [[0, 0], [0.04, 1], [0.7, 1], [1, 0]] }
      ],
      "lights": [
        { "color": [1.0, 0.55, 0.25], "range": 10, "intensity": [[0, 0], [0.04, 14], [0.3, 4], [1.0, 0]], "flicker": 0.15 }
      ],
      "sounds": [
        { "clip": "sfx/fireball_impact.wav", "volume": 1.0, "volumeJitter": 0.05, "pitchJitter": 0.06, "spatial": 0.5 }
      ],
      "shake": { "amplitude": 0.8, "frequency": 20, "duration": 0.5, "radius": 30 }
    }
  }
}
```

A channelled cast (circle under the caster that holds while the cast charges, activates on launch, vanishes):
`"cast": {"duration": 2.7, "layers": [{"name": "circle", "sheet": "../../../../circles/realistic/ember_sigil", "anchor": "caster",
"orient": "ground", "offset": [0, 0.03, 0], "playback": "segments", "size": 2.6, "rotation": 0, "softness": 0}, ...]}`.

## Data checks the engine reports (parse warnings, never silent)

- A sheet whose drawing lies almost entirely below its pivot (extent down > 0.55, up < 0.12): the pivot was probably
  written v-down; `pivot` y counts from the bottom of the content (v up).
- A Status layer (`feel.class: "status"`) anchored to `target` (the aim point at chest height): a status sits on the
  body, use `targetBody`.
- A Nova whose `ring` layer has `playback: "loop"`: the archetype test `arch_nova_ring` says so in its message (the band
  restarts every loop, so it never crosses 5 m once; use `once`).
- A `front` spawn whose cards live longer than 0.5 s (the disc fills behind the front).

---

# Part 2. Real Spells flipbook sheet format (`sheet.json`, schema `realspells.sheet/1`)

A **sheet** is one flipbook layer of one spell in one style: `<recipe>/sheets/<name>/`.

```
sheet.png          High tier      RGBA8, cell 512 px
sheet_256.png      Standard tier  RGBA8, cell 256 px   (same grid, same schedule)
sheet_128.png      Mobile tier    RGBA8, cell 128 px
sheet_mv.png       motion vectors for High      RGB8 (R,G used, B = 0), same grid and cell as sheet.png
sheet_mv_256.png   motion vectors for Standard
sheet_mv_128.png   motion vectors for Mobile
sheet.json         this description
```

All three tiers are cut from the same master frames; a tier never has its own schedule. `sheet_mv*.png` exist only
when `motionVectors` is present in `sheet.json`.

## 1. Pixel layout

- **Grid**: `grid.cols` x `grid.rows` cells, each `cell` x `cell` pixels. Sheet size = `cols*cell` x `rows*cell`
  (multiples of 4; power of two in every shipped sheet so far: 4x4, 8x4, 8x8).
- **Order**: row-major from the **top-left** cell of the PNG as stored (image row 0 = top). Frame `i` is at
  `col = i % cols`, `row = i / cols` counted from the top. In Unity UV (v up) cell `i` spans
  `u in [col/cols, (col+1)/cols]`, `v in [1-(row+1)/rows, 1-row/rows]`.
- `frameCount <= cols*rows`; unused trailing cells are transparent black.
- **Gutter**: every cell has a transparent border of `gutter` px on all four sides (High 8, Standard 4, Mobile 2).
  The **content rect** of a cell is `[gutter, cell-gutter)` on both axes. Sample only inside it:
  `uvCell = (gutter + uvContent * (cell - 2*gutter)) / cell`, then clamp `uvContent` to `[0.5/content, 1-0.5/content]`
  (content = `cell - 2*gutter`). The gutter keeps bilinear filtering and the first 3 mips free of bleeding from the
  neighbour cell. Mips beyond `log2(gutter)` may bleed; generate mips with a box filter, do not use more than
  `mipMax` levels (given in `tiers`).
- **Colour under zero alpha** is the colour of the nearest covered pixel (dilated), so filtering never pulls a black or
  white fringe into the edge. Do not premultiply on import.

## 2. Channels and colour

| Field | Values | Meaning |
|---|---|---|
| `color.space` | `sRGB` | RGB stored sRGB-encoded (import as sRGB texture). Alpha is linear coverage. |
| `color.alpha` | `straight` | A = coverage/opacity, RGB = the colour of the covered surface (not premultiplied). |
| `color.content` | `emissive` / `lit` / `flat` | `emissive`: fire, sparks, glow - colour is light. `lit`: smoke - colour is a baked grey/tinted lighting that the spell's tint multiplies. `flat`: toon cels, opaque inside the shape. |
| `blend.mode` | `alpha`, `additive`, `glow` | see below - the sheet's recommended default; `spell.json` may override per layer. |
| `blend.intensity` | float >= 0 | HDR multiplier on RGB (emission). |
| `blend.occlusion` | 0..1 | only for `glow`: how much the layer darkens what is behind it. |

Blend equations (linear space, C = sampled RGB linearised, A = alpha, D = destination, I = intensity, k = occlusion):

```
alpha     out = C*I*A + D*(1 - A)              (Blend SrcAlpha OneMinusSrcAlpha, RGB pre-scaled by I)
additive  out = C*I*A + D                      (Blend One One on premultiplied C*I*A)
glow      out = C*I*A + D*(1 - k*A)            (Blend One OneMinusSrcAlpha with the shader writing A' = k*A)
```

`glow` is the fire mode: the dense core occludes the background (reads over a bright sky), the thin outer flame adds
light (reads over a dark night). `glow` with k = 1 is `alpha`, k = 0 is `additive`.

## 3. Time: the schedule

```json
"schedule": {
  "duration": 1.40,
  "loop": false,
  "times": [0.0, 0.012, 0.026, ...],
  "end": "empty",
  "nominalFps": 30
}
```

- `times[i]` = seconds from the start of the layer's life when kept frame `i` is exactly on screen.
  `times` is strictly increasing, `times[0] = 0`, `len(times) = frameCount`.
- Between `times[i]` and `times[i+1]` the shader blends frame `i` into frame `i+1` with
  `f = (t - times[i]) / (times[i+1] - times[i])` (cross-fade, or warped blend when `motionVectors` exists, section 4).
  Blend **premultiplied** values (`rgb*a`, `a`) and divide back, or blend straight values and accept slightly darker
  edges; premultiplied is what the QA previews do.
- `interpolate`: `blend` (default, as above) or `step` - show frame `i` unchanged for the whole interval (drawn toon FX
  "on twos": the hard cut between drawings is the style; motion vectors are then ignored).
- The schedule is **denser at birth and death** (small steps where the shape changes fast); the middle is sparser.
  Do not replace it by a constant fps.
- **One-shot** (`loop: false`): the life ends at `duration`. `end` says what the last frame is:
  `empty` - the last frame is (nearly) empty, the layer is finished at `duration = times[N-1]`;
  `hold` - the last frame is a residue (scorch decal) that stays; how long it stays and how it fades is the spell's
  business (`spell.json`), the sheet only says it is safe to hold.
- **Loop** (`loop: true`): `duration` is the period. Frame `N-1` blends into frame `0` over
  `[times[N-1], duration]`, exactly like any other step, so the loop has no seam. Start each instance at a random
  phase. `end` is `loop`.
- A spell may **retime** a layer (play it 0.8x or 1.2x) by scaling `times` and `duration` together; never by dropping
  frames.
- `nominalFps` is informational: the average density of the schedule.

## 4. Motion vectors (`motionVectors`, optional)

```json
"motionVectors": {
  "tiers": {"high": "sheet_mv.png", "standard": "sheet_mv_256.png", "mobile": "sheet_mv_128.png"},
  "encoding": "rg8",
  "scale": 0.06,
  "direction": "forward",
  "vAxis": "up",
  "strength": 1.0
}
```

- Cell `i` of `sheet_mv.png` holds the **forward optical flow from kept frame i to kept frame i+1**, in
  **content-UV units** (1.0 = the content rect width), with +u to the right and **+v up** (Unity UV convention).
  For a loop the last cell holds the flow from `N-1` to `0`; for a one-shot it is zero.
- Decode (MV texture imported **linear**, no sRGB, bilinear, same gutters):
  `mv = (tex.rg * 2 - 1) * scale`. R = G = 128/255 is zero motion.
- Warped blend between frames `a = i` and `b = i+1` at fraction `f`, all in content UV of the current pixel `uv`:

```
mv   = decode(MV cell a at uv)
colA = sample(cell a, uv - mv * f        * strength)
colB = sample(cell b, uv + mv * (1 - f)  * strength)
out  = lerp(colA, colB, f)               (on straight RGBA; or on premultiplied RGB*A if the engine prefers)
```

- The flow is measured with OpenCV DIS on the premultiplied luminance of consecutive kept frames, smoothed and
  extrapolated past the shape's edge (normalised convolution) so that a growing shape warps outward instead of
  fading in. `strength` 0 gives a plain cross-fade.
- `scale` is the largest flow component of the sheet rounded up to 0.01; the 8-bit step is `2*scale/255`
  (scale 0.06 at High = 0.24 px).

## 5. Placement metadata

| Field | Meaning |
|---|---|
| `pivot` | **v counts UP from the bottom of the content (v = 0 bottom row, v = 1 top row; image row order is the opposite).** A v-down pivot draws ground-standing sheets (spikes, columns) upside down or at the wrong height; the engine warns when a drawing lies almost entirely below its pivot. `[u, v]` in content UV of the point the layer is spawned at: the birth point of an explosion/burst, the centre of the fireball core, the hand point of a cast. |
| `forward` | for directional layers (projectile core, trail puff): the travel direction in content UV, e.g. `[1, 0]` = flies to +u (right). The engine rotates the quad so `forward` follows velocity. Absent = no direction. |
| `extent` | distances from the pivot to the edges of the covered area (all frames), in content-UV units, `[left, right, down, up]`, each 0..1; lets the engine size the quad in metres from `spell.json`'s size without guessing the empty margin. With a centred pivot each value is <= 0.5; with an off-centre pivot (explosion on its ground line, pivot v = 0.2) one side can reach 0.8 - do not clamp to 0.5. |
| `projection` | `billboard` (camera-facing) or `ground` (decal on the ground plane, content +v = world forward). |

## 6. Full example

```json
{
  "schema": "realspells.sheet/1",
  "name": "fireball_explosion",
  "style": "realistic",
  "spell": "fireball",
  "layer": "impact_explosion",
  "frameCount": 64,
  "grid": {"cols": 8, "rows": 8},
  "tiers": {
    "high":     {"file": "sheet.png",     "cell": 512, "gutter": 8, "mipMax": 3},
    "standard": {"file": "sheet_256.png", "cell": 256, "gutter": 4, "mipMax": 2},
    "mobile":   {"file": "sheet_128.png", "cell": 128, "gutter": 2, "mipMax": 1}
  },
  "color": {"space": "sRGB", "alpha": "straight", "content": "emissive"},
  "blend": {"mode": "glow", "intensity": 2.0, "occlusion": 0.75},
  "schedule": {"duration": 1.4, "loop": false, "end": "empty", "nominalFps": 45.7, "times": [0.0, 0.011, "..."]},
  "motionVectors": {"tiers": {"high": "sheet_mv.png", "standard": "sheet_mv_256.png", "mobile": "sheet_mv_128.png"},
                    "encoding": "rg8", "scale": 0.06, "direction": "forward", "vAxis": "up", "strength": 1.0},
  "pivot": [0.5, 0.42],
  "extent": [0.46, 0.47, 0.40, 0.55],
  "projection": "billboard",
  "brightness": {"peakLinear": 0.98, "p99Linear": 0.71, "meanCoveredLinear": 0.22, "clippedFraction": 0.004},
  "motion": {"meanFlowPx": 6.1, "p95FlowPx": 17.4, "flowExplained": 0.62, "maxStillRun": 0, "pops": [],
             "birth": {"coverage": [0.0, 0.002, 0.011]}}
}
```

Versioning: a breaking change bumps `schema` to `realspells.sheet/2`; new optional fields keep `/1`.
