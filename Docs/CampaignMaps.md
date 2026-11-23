# Campaign space maps

All four combat stages use the downloaded asteroid, rock, and terrain assets. Original asset materials are preserved; generated materials use URP.

| Scene | Theme | Layout | Connected floor cells |
|---|---|---|---:|
| Stage1 | Asteroid Mining Outpost | Cyan mining outpost, cross lanes, rock cover | 4,384 |
| Stage2 | Frozen Reach | Blue ice outcrops, diagonal lanes and circular outer route | 4,354 |
| Stage3 | Solar Refinery | Orange smelters, warning stripes and square circulation loop | 4,296 |
| StageBoss | Void Core | Purple/cyan arena, clear central disc, peripheral anchors and gate | 4,524 |

## Runtime integration

The existing scene names, GUIDs, build order and gameplay scripts remain in use. `MapGenerator` selects `CampaignMapLayout` when its object has `CampaignMapTheme`, so the themed layout is generated during actual play as well as shown in the editor. Stage1 retains its existing `OrbitalMapLayout`.

`OrbitGroundUV` runs after map generation to apply continuous ground texture coordinates. Floor and obstacle colliders still come from `MapRenderer`. Decorations are outside playable space or fitted inside obstacle footprints and excluded from the navigation layer.

The boss arena preserves the existing boss, spawner, camera and combat components. Its central 31 × 31 cells are checked for obstacles.

## Editing and recovery

- Scene decoration root: `Campaign Environment`.
- Generated assets: `Assets/OrbitMap/Campaign/Stage2`, `Stage3`, `StageBoss`.
- Layout definitions: `Assets/Scripts/Map/CampaignMapTheme.cs`.
- Editor builder and validation: `Assets/Editor/CampaignMapSetup.cs`.
- Backups: `Assets/Scenes/MapBackups/Stage2_BeforeCampaign.unity`, `Stage3_BeforeCampaign.unity`, `StageBoss_BeforeCampaign.unity`.

`CampaignMapSetup.Build(sceneName, sector)` refuses Play Mode, a dirty current scene, or rebuilding an existing campaign environment. It creates the backup, generates and checks the map, then saves. Change both the layout and decoration placement when modifying obstacle footprints.

## Validation

All three new scene layouts have fully connected walkable cells and 8/8 complete representative NavMesh routes. Editor overview screenshots are in `Assets/Screenshots/Campaign-Stage2.png`, `Campaign-Stage3.png` and `Campaign-Boss.png`.

Runtime smoke checks completed on 2026-10-20:

- Lobby `StartGame()` loaded Stage1. The existing `OnNextStage` event advanced Stage1 → Stage2 → Stage3 → IntroScene. `SequenceController.OnButtonClick()` loaded StageBoss.
- Stage2 and Stage3 selected their intended themes at runtime and passed all eight navigation routes. Observed active agents: Stage2 7/7 and Stage3 14/14 on NavMesh, with pursuit movement observed.
- StageBoss selected VoidCore, passed all eight routes and the central clearance check. The boss spawned, moved away from its spawn position and had a complete navigation path.
- No missing or unsupported materials were found in the three runtime scenes. Actual Game view captures verified blue, orange and purple maps alongside combat UI.
- Temporary runtime invulnerability used during inspection was cleared, and Play Mode was stopped afterward.

These are transition, rendering and navigation smoke checks; they do not replace a full playthrough or combat balance testing.

## Stage branch integration

The stage branch incorporates the main gameplay/UI baseline used by these scenes. Its existing `StageObjectPlacer` and optional `MapGenerator.stageObjectPlacer` hook are preserved. The four authored combat scenes do not assign that optional placer; their obstacle footprints come from the themed layouts.

Use Unity 6000.3.24f1. The map change keeps the repository's direct UPM dependencies; the lockfile records the transitive dependencies resolved by this Editor during validation. The local AI assistant/Pipeline additions are not required for gameplay. Only referenced vendor resources and the resources used by the editor builders are included, preserving original asset paths and GUIDs.

For a fresh checkout, `CampaignMapValidation.Run` can be executed with Unity in batch mode. It opens all four combat scenes, checks missing scripts and map prefabs, regenerates geometry and navigation, checks material/shader references and validates routes. It does not save the generated test geometry.

This validation passed in the separate upload checkout on 2026-10-20 with Unity exiting successfully: Stage1 4/4 routes, Stage2 8/8, Stage3 8/8 and StageBoss 8/8. All exported runtime dependency paths exist and their metadata GUIDs match the working project.
