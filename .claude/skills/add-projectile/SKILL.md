---
name: add-projectile
description: Create a new SimpleProjectile missile prefab (the kind MissileAnimator/BeamAnimator fire) from Epic Toon FX (or other vendor) parts — copies the in-flight missile, muzzle flash and impact prefabs into a new Assets/Game/_Prefabs/_Projectiles/_<Name>/ folder, builds <Name>.prefab wired to those local copies (never to the vendor assets), and puts it on the projectile sorting layer. Use when the user asks for a new projectile/missile/shot ("AddProjectile"), wants one built from a vendor missile like FireballSoftMissileGreen, or a creature needs its own projectile look.
---

# Add Projectile

`SimpleProjectile` mimics Epic Toon FX's projectile setup: a root with the `SimpleProjectile`
component and three effect prefabs —

| Field | Part | Lifetime |
| --- | --- | --- |
| `projectileParticle` | in-flight **missile** visual, spawned as a child | until arrival |
| `muzzleParticle` | **muzzle** flash at the launch point | 1.5s |
| `impactParticle` | **impact**/explosion where it lands | 3s |

Every projectile lives in its own folder under `Assets/Game/_Prefabs/_Projectiles/_<Name>/` holding
those 4 prefabs. The three parts are **copies** (`AssetDatabase.CopyAsset`, independent prefabs, not
variants), so the user can edit them freely without touching vendor content.

## 1. Find the three parts

Epic Toon FX names them per family + color. For a missile
`Assets/Epic Toon FX/Prefabs/Combat/Missiles/<Family>/<X>Missile<Color>.prefab`:

- muzzle: `Assets/Epic Toon FX/Prefabs/Combat/Muzzleflash/<X>Muzzle/Muzzle<X><Color>.prefab`
- impact: `Assets/Epic Toon FX/Prefabs/Combat/Explosions/<X>Explosion/Explosion<X><Color>.prefab`

e.g. `FireballSoftMissileGreen` → `MuzzleFireballSoftGreen` + `ExplosionFireballSoftGreen`. Confirm each
with `Glob` (`**/*<X>*<Color>*.prefab`); if a family has no matching muzzle/impact, ask the user or
pass `""` for that part (the field stays empty and the effect is skipped at runtime).

## 2. Build it

Dry run, then real run, with the same arguments (pass all five):

```
mcp__coplay-mcp__execute_script
  filePath:  .claude/skills/add-projectile/AddProjectile.cs
  arguments: {"name": "Spore",
              "missilePath": "Assets/Epic Toon FX/Prefabs/Combat/Missiles/FireballSoft/FireballSoftMissileGreen.prefab",
              "muzzlePath":  "Assets/Epic Toon FX/Prefabs/Combat/Muzzleflash/FireballSoftMuzzle/MuzzleFireballSoftGreen.prefab",
              "impactPath":  "Assets/Epic Toon FX/Prefabs/Combat/Explosions/FireballSoftExplosion/ExplosionFireballSoftGreen.prefab",
              "dryRun": true}
```

It refuses to overwrite an existing `<Name>.prefab`. The root is copied from
`_FireBall/Fireball.prefab` (tag `Missile`, Direct trajectory, empty `trailParticles`) and renamed. The
report lists the new prefab's prefab dependencies — every one must be inside the new folder (an
`<-- OUTSIDE FOLDER` line means a vendor reference leaked).

## 3. Sorting layer

Run the **`set-projectile-layer-order`** skill with `onlyPrefab: "_<Name>/"` (dry run, then real) so
all three parts render on the `Shield` layer above units (CLAUDE.md rule 15).

## 4. Optional follow-ups

- Assign it: set `projectilePrefab` on the creature's `MissileAnimator` (prefab edit via
  `LoadPrefabContents`/`SaveAsPrefabAsset`, or the `setup-creature` skill).
- Trails that should fade instead of vanish on arrival: add the missile's trail child objects to
  `trailParticles` (matched by name).
- Sprite-based missile art (not particles) needs `BillboardCorrector` on the visual (rule 15).
- Ballistic arc/spin: `trajectoryType`, `arcHeight`, `spinRotationsPerSecond` on the root.
- Verify visually with `playtest-creature-attack`.

## Existing projectiles

`_Spore` (ETFX FireballSoft Green — HealingShroom's damage + heal shots) was the first one built with
this skill. `_AcidSpit` (ETFX Liquid Acid — Lizard's beam) was the second.
