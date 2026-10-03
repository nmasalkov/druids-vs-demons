---
name: set-projectile-layer-order
description: Put every renderer of every projectile effect (the SimpleProjectile prefab itself plus its projectileParticle / muzzleParticle / impactParticle prefabs — particle systems, sprites, trails, sorting groups) onto one sorting layer, and optionally shift or set their order in layer, in one scripted pass. Use when a projectile, muzzle flash or impact/explosion draws behind creatures or hero avatars, after adding a new projectile or swapping in a new VFX prefab, or when the user asks to change projectile layer/order ("SetProjectileLayerOrder").
---

# Set Projectile Layer / Order

Units draw on the `Default` sorting layer (`DepthSortingOrder` gives each one a y-derived order, hero
avatars included). Anything a projectile spawns on `Default` with a small order (0–3, typical for
Epic Toon FX effects) therefore draws **behind** the creature or avatar it hits. The fix is to render
projectile effects on the **`Shield`** sorting layer, which is above `Default` and below `UI`
(layers: `Background` < `Default` < `Shield` < `UI`). The project uses URP's 2D Renderer, so sorting
layer and order fully decide draw order.

`SetProjectileLayerOrder.cs` (next to this file) does the whole pass. It is **not** part of the Unity
project (it lives outside `Assets/`), so it only compiles when run through Coplay.

## What it touches

- Every prefab under `Assets/Game` whose **root** has `SimpleProjectile` (creature `MissileAnimator`/
  `BeamAnimator` shots and nuke shots like `MagicFire` all go through these).
- For each: the prefab itself, plus whatever `projectileParticle`, `muzzleParticle` and `impactParticle`
  point at. `trailParticles` are children of the in-flight visual, so they're covered.
- Inside each prefab: every `Renderer` (`ParticleSystemRenderer`, `SpriteRenderer`, `TrailRenderer`,
  `LineRenderer`, `MeshRenderer`, ...) and every `SortingGroup`, including inactive children and
  sub-emitters.
- **Vendored prefabs are never edited.** If a field points outside `Assets/Game` (e.g. straight at an
  Epic Toon FX prefab), the script copies that prefab into the projectile's own folder and repoints the
  field at the copy — the same per-projectile-folder pattern the rest of `_Prefabs/_Projectiles` uses.
  A dry run only reports this.

## Run it

Always dry run first, read the report, then run for real with the same arguments:

```
mcp__coplay-mcp__execute_script
  filePath:  .claude/skills/set-projectile-layer-order/SetProjectileLayerOrder.cs
  arguments: {"sortingLayer": "Shield", "orderMode": "keep", "orderValue": 0, "onlyPrefab": "", "dryRun": true}
```

Pass **all five** arguments every time (Coplay maps them to method parameters by name).

| Argument | Meaning |
| --- | --- |
| `sortingLayer` | Target layer name. Must exist (the script lists valid names if not). Default choice: `Shield`. |
| `orderMode` | `keep` = leave each renderer's order alone (preserves the effect's internal layering — the default choice). `add` = order += `orderValue` (lift a whole effect above something, keeping internal layering). `set` = every renderer gets exactly `orderValue` (flattens internal layering — rarely what you want). |
| `orderValue` | Used by `add`/`set`, ignored by `keep`. |
| `onlyPrefab` | Case-insensitive substring of a `SimpleProjectile` prefab's path (e.g. `"Skull"`, `"_Apple/"`) to limit the pass to that projectile and its three effect prefabs. `""` = all. |
| `dryRun` | `true` = report only. `false` = write. |

Report format: `change <path> [who uses it]` followed by `Renderer 'child': OldLayer/order -> NewLayer/order`
lines, or `ok <path>` when already correct. A second dry run right after applying must say
`prefabsChanged=0`.

## After running

1. **Stale Prefab Mode gotcha (live-caught).** If one of the changed prefabs is open in Prefab Mode,
   that stage can keep showing the old layers even though the file on disk is correct. If the user then
   edits anything there, Prefab Mode's auto-save writes the stale layers back over the fix. Check with
   `PrefabStageUtility.GetCurrentPrefabStage()`: if it's one of the changed prefabs and not dirty, reopen
   it (`StageUtility.GoToMainStage()` then `PrefabStageUtility.OpenPrefab(path)`). If it *is* dirty,
   don't touch it — tell the user.
2. `check_compile_errors` isn't needed (no project code changes), but the vendor-copy step does add new
   prefab assets — mention their paths in the report.
3. Visual check (it's a draw-order bug, so a render is the real proof — see `playtest-creature-attack`):
   spawn a shooter + a target with `BalanceTool`, start a battle, and snapshot from a coroutine while an
   impact instance (`<ImpactPrefab>(Clone)`) is alive, then `Read` the PNG. **Check
   `get_unity_editor_state` first — if Play mode is already running, the user is in a session: don't
   inject spawns or battles into it, ask them to eyeball it or wait for them to stop.**

## Order within the Shield layer

Other things already on `Shield`, all at order 0: the `Shield`/`ShieldRed` construct, unit status
effects and feedbacks (`ShockedFeedback`, `BattleCry*`, `Debuff`, `EnhanceBuff`), and the hero views.
Projectile effects keep their authored orders (0–3), so an impact at order 0 ties with those and their
relative order isn't guaranteed. If an impact should always draw over a shield bubble or status effect,
rerun with `orderMode: "add"` and e.g. `orderValue: 10`.

## Related

- `Assets/Game/_Scripts/_Units/SimpleProjectile.cs` — the three effect fields this reads.
- CLAUDE.md rule 15 — particle/projectile rendering gotchas (`Scaling Mode = Local`, `BillboardCorrector`,
  `DepthSortingOrder`).
- `playtest-creature-attack` skill — spawning/battle/screenshot recipe for the visual check.
