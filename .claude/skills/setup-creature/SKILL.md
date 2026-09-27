---
name: setup-creature
description: Finish wiring hand-added creature prefabs (Mage/Tank/Archer) after the user has added their visual mesh and icon — links the type animator's Spine skeleton reference, the ShockedFeedback position-shaker target, any ranged projectile component present (MissileAnimator/BeamAnimator — not tied to a fixed Archer/Mage pairing), creates the matching balance CreatureSO (balance copied from the default creature of the same type in _DefaultCreatures.asset), registers it in GameCatalog, and — for an archer/tank/mage trio — sets up the enemy encounter that uses them (a per-fight CreaturesSO roster + a FightSO copied from the previous fight by default). Use whenever the user says new creature prefabs were added and need setup, asks to check/finish a creature's wiring before it's playable as an enemy, or asks for a new themed fight built around new creatures.
---

# Setup Creature

The user's workflow: they hand-build a new creature by duplicating (or nesting a new mesh under)
`Assets/Game/_Prefabs/_Characters/_Units/_<Type>/<Type>ParentGreen.prefab`
(`CatapultParentGreen`=Archer, `_GolemParentGreen`=Tank, `DragonParentGreen`=Mage), swap in the Fantazia
monster mesh under `Visual`, and add the matching card icon under
`Assets/Game/_Sprites/_Creatures/`. Everything else — animator/Spine linking, status-feedback targets,
projectile assignment, the balance `ScriptableObject`, catalog registration, and the encounter that
uses the new creatures — is this skill's job.

**Batch mode.** The user usually adds a themed trio at once (one Archer + one Tank + one Mage, e.g.
`GhostArcher`/`GhostTank`/`GhostMage`). Treat that trio as one enemy roster: run steps 1–6 for each
creature, then step 7 (encounter setup) once for the whole trio. A single creature on its own skips
step 7 unless the user asks for an encounter.

`Assets/Game/_Prefabs/_Characters/_Units/_Archer/Demon.prefab` +
`Assets/Game/_ScriptableObjects/_Actions/_Creatures/_Archers/Demon.asset` is the fully-correct reference
example — when in doubt, diff the creature you're setting up against Demon's pattern.

## 1. Locate the prefab and confirm its shape

- `Assets/Game/_Prefabs/_Characters/_Units/_<Type>/<Creature>.prefab`, a Prefab Variant of
  `<Type>ParentGreen.prefab`.
- Root GameObject should be renamed to the creature's proper name — check via
  `get_game_object_info` (`prefabPath` param) or `get_unity_editor_state`; fix with `rename_game_object`
  if it's still the parent's name. **Also check it isn't a sibling's name** — the user often builds a
  trio by duplicating one finished prefab, so a root name left over from the previous one is the common
  slip (`GhostMage.prefab`'s root was still named `GhostTank`). The root name must match the prefab
  file name.
- Confirm the creature has exactly one mesh (`SkeletonAnimation`) and that its **parent is `Visual`**
  (the transform the type animator's `creatureVisual` field points at). The parent's placeholder mesh
  should already be removed by the user when they added their own. If you find more than one mesh, or
  none, stop and ask — don't guess which is the intended one.
- **Mesh parented directly under the root instead of `Visual`** is an authoring slip, not a design
  choice (it happened on `GhostMage` — dropped onto the root, `insertIndex -1`, instead of onto
  `Visual`). A mesh outside `Visual` misses everything that animates/sorts the visual group. Fix it
  without asking: reparent it under `Visual` with `SetParent(visual, worldPositionStays: true)` (keeps
  exactly the pose the user authored) inside a `LoadPrefabContents` block, and say so in the report.

## 2. Animator → Spine link

Root already carries `ArcherAnimator`/`TankAnimator`/`MageAnimator` (inherited from the type parent —
never re-add it). Read the current `skeletonAnimation` field via `get_game_object_info`. If it's `None`
or still points at the parent's placeholder mesh, wire it to the `SkeletonAnimation` component on the
creature's own mesh.

**Known gotcha:** `set_property` has failed on some fields on this project's Feel/MMFeedbacks and
possibly other third-party components regardless of casing tried (`TargetTransform` vs
`targetTransform`) — error was `Property or field '...' not found or not writable`. If the dedicated
tool fails for a field, don't burn more than 1-2 casing retries — fall back straight to `execute_script`
using `PrefabUtility.LoadPrefabContents(path)` → find the target component → for a `[SerializeField]
protected/private` field use `new SerializedObject(component).FindProperty("<fieldName>")` (exact
declared field name, e.g. `"skeletonAnimation"`) → set `.objectReferenceValue` → `ApplyModifiedProperties()`
→ `PrefabUtility.SaveAsPrefabAsset(root, path)` → `PrefabUtility.UnloadPrefabContents(root)`. For a
plain **public** field (e.g. `MMPositionShaker.TargetTransform`) direct assignment works fine inside the
same loaded-contents block — no `SerializedObject` needed, just remember `EditorUtility.SetDirty(...)`
before saving. To find "this creature's own mesh" robustly without knowing its exact instance name in
advance, search `visualTransform.GetComponentsInChildren<Spine.Unity.SkeletonAnimation>(true)` and take
the (should be single) result rather than hardcoding a path string.

## 3. Shocked feedback (the bug this skill exists partly to prevent recurring)

`StatusFeedbacks/ShockedFeedback`'s `MMPositionShaker.TargetTransform` must point at the **same mesh's
Transform** (not its `SkeletonAnimation` component — different field, different fileID). Left null, it
silently shakes its own empty anchor object instead of the creature — no exception, no visible effect.
This exact bug was found and fixed on 6 creatures (`Bat`, `OrkMage`, `Cyclop`, `OrkTank`, `Skeleton`,
`Kodo`) in one pass — always check it explicitly, don't assume a new creature got it right by copying an
already-broken sibling.

## 4. Projectile (skip entirely for Tank — it's melee, no such component)

**Don't assume "Archer → `MissileAnimator`, Mage → `BeamAnimator`" as a fixed rule** — that's just
today's default pairing, not a strict constraint. A future creature may carry the other type's
component, or (later) both at once. What actually matters: **whichever ranged/projectile animator
component(s) are present on the root** (`MissileAnimator` and/or `BeamAnimator`, both extend
`ProjectileAnimatorBase`, fields `projectilePrefab`/`projectileSpeed`; `BeamAnimator` also has
`fireRate`, `MageAnimator` separately has `freezeAfter`/`freezeDuration`) **must be explicitly wired,
not left at its default.** Check by component presence (`GetComponents<Component>()` / `get_game_object_info`
lists whatever's actually on the object), not by assumed creature type — and if you find more than one
such component on the same creature, wire each one independently. Confirm via `get_game_object_info`
(these are plain `SimpleProjectile`/`float` fields, `set_property` works fine for them — not the
shaker's problem field).

Before picking a `projectilePrefab`, check what every sibling creature with that same component already
uses (`Assets/Game/_Prefabs/_Projectiles/*` — `_Apple`, `_FireBall`, `_FireMagic`, `_GreenMissile`,
`_LightningBall`, `_Sonar`, `_Star`, `_Stone`), **including type-parent-based creatures that use the parent prefab directly** (e.g. `Bubka`'s
`CreatureSO.creaturePrefab` points straight at `CatapultParentGreen.prefab`, so its runtime projectile
*is* whatever `CatapultParentGreen`'s own `MissileAnimator` default currently is). If the creature you're
setting up would otherwise silently collide with another creature's projectile (including a parent's
un-overridden default), **assign a different one and say so in your report** — don't ask the user to
pick unless nothing sensible is free.

## 5. Matching CreatureSO

`Assets/Game/_ScriptableObjects/_Actions/_Creatures/_<Type>s/<Creature>.asset` (subclass `ArcherSO`/
`TankSO`/`MageSO`, extends `CreatureSO` → `ActionSO`).

**Template = the default creature of the same type**, not an arbitrary sibling. Read
`Assets/Game/_ScriptableObjects/_Actions/_Creatures/_DefaultCreatures.asset` (a `CreaturesSO`) and take its
`archer`/`tank`/`mage` entry matching the new creature's type — resolve the guid to its asset, don't
hardcode a name (today it's `Bubka`/`Golem`/`Dragon`, but that pointer can change). If the SO doesn't
exist yet, `duplicate_asset` that default into `_<Type>s/<Creature>.asset`. This gives a new enemy
creature starter-level balance with no special rules baked in — a sibling like `Kodo` would silently
drag along its own tuned stats and `specialDamageModifiers` (ShieldBreaker).

What to keep vs replace after duplicating:

- **Keep as copied** (unless the user gives numbers): `levelStats`, `specialDamageModifiers`,
  `critChancePercent`/`critDamageBonusPercent` (0/0 on the defaults — see `docs/Battle.md`'s
  "Critical strike"), `experienceReward`, `xpThresholds`, `healAmounts`.
- **HitFeedback needs nothing per creature**: every unit inherits `_Feedbacks/HitFeedback.prefab` from
  `ParentUnit`, and its hit anchor offset from the type parent. Never add a second one to a variant.
- **Always replace** (never leave the default's values in place — a duplicate that still says
  `id: archer` collides with the real default in `GameCatalog`): `id`, `actionName`, `description`,
  `cardSprite`, `creaturePrefab`.

Fields (`get_game_object_info`/`set_property` with `asset_path`, or `execute_script` +
`AssetDatabase.LoadAssetAtPath` for anything the tool can't reach):

- **`id`** — unique across `GameCatalog.asset`'s `allCreatures` (check first). The project's existing
  convention is inconsistent (`camelCase`, plain lowercase, `<name><Type>`) — just guarantee
  uniqueness, don't force one fixed formula (`ghostArcher`/`ghostTank`/`ghostMage` is fine).
- **`actionName`** / **`description`** — creature-specific. If it reads identical to another creature's
  (copy-paste leftover from templating off a sibling — already happened for 3 Tanks in this project),
  rewrite it distinctly.
- **`cardSprite`** — must point at `Assets/Game/_Sprites/_Creatures/<Creature>.png` (or close variant).
  **List that folder first** (`ls`/`Glob`) rather than assuming a file is missing — check before
  reporting "no icon exists." If it genuinely doesn't exist, don't leave `cardSprite` silently pointing
  at a wrong/reused sprite without saying so loudly in your final report — that's the single most
  likely thing the user still needs to do themselves (they said they'll usually add icon + visual,
  everything else is this skill's job).
- **`creaturePrefab`** — must reference this creature's own `.prefab` (verify the guid matches the
  prefab's own `.meta` file, not a sibling's).
- **The other direction: the prefab's `Creature.Data` must point back at this SO.**
  `CreaturesManager.SpawnCreatures` only instantiates `creaturePrefab` — it never assigns `Data` — so
  the spawned unit runs on whatever `Data` the prefab serializes, and a fresh variant inherits its type
  parent's (`Bubka`/`Golem`/`Dragon`). Left alone, the new creature silently uses the default's asset
  for stats, XP, and id-keyed reward boosts, which aren't its own. Set it with `set_property`
  (`component_type: Creature`, `property_name: Data`, `prefab_path`) — that works; confirm the
  `<Data>k__BackingField` override landed in the prefab YAML. `Kodo`/`OrkMage`/`OrkTank` are the
  correct precedent.
- **`levelStats`/`experienceReward`/`xpThresholds`/`healAmounts`** — confirm they match the default
  template (and are non-empty). Don't invent new balance numbers unless asked.

## 6. Register in GameCatalog

Add the SO's guid to `Assets/Game/_ScriptableObjects/_Campaign/GameCatalog.asset`'s `allCreatures` list
if it isn't already there. Easy to forget — doesn't fail to compile, just silently makes the creature
unreachable via `GameCatalog.FindArcher/FindTank/FindMage`. This is a plain `List<CreatureSO>` on a
`ScriptableObject` — editing it needs either `set_property` (if it supports list append cleanly; verify
by re-reading the asset after) or `execute_script` with `AssetDatabase.LoadAssetAtPath<GameCatalog>` →
`allCreatures.Add(so)` → `EditorUtility.SetDirty` → `AssetDatabase.SaveAssets()`.

## 7. Encounter setup (batch mode — a new archer/tank/mage trio)

On by default whenever a full trio is set up; skip only if the user says "creatures only". Read
`docs/Encounters.md` first if you haven't this session. Everything here is asset-only (no scene edits).

**Inputs.** Fight number `N` and a theme name (e.g. `4` + `Ghosts`). If the user doesn't give `N`, use
the first `AllEncounters.fights` slot after the last fight that already has its own themed folder/roster.
**Template fight** defaults to fight `N-1` (e.g. `Fight3 Orks` for `Fight4 Ghosts`) unless the user names
another. Asset root: `Assets/Game/_ScriptableObjects/_Campaign/_Encounters/`.

**Layout** (one folder per fight, precedent `_Fight2 Skeleton/`, `_Fight3 Orks/`):

```
_Encounters/_Fight<N> <Theme>/
    Fight<N> <Theme>.asset        (FightSO)
    <Singular> Creatures.asset    (CreaturesSO — "Skeleton Creatures", "Ork Creatures", "Ghost Creatures")
```

### 7a. Roster (`CreaturesSO`)

Create `<Singular> Creatures.asset` (`CreaturesSO`, `Assets/Game/_Scripts/_ScriptableObjects/CreaturesSO.cs`
— plain `archer`/`tank`/`mage` fields) and point each field at the new SOs from step 5. Easiest path:
`duplicate_asset` the template fight's own roster asset into the new folder and then reassign all three
fields — never leave one pointing at the template's creature.

### 7b. The `FightSO`: move a placeholder, or create a new one

Look at `AllEncounters.asset`'s `fights[N-1]` first:

- **It exists and is an unthemed placeholder** (loose file directly under `_Encounters/`, e.g.
  `Fight4 Elite.asset`, `enemyData.creatures` null): **move/rename it**, don't create a new asset —
  `AssetDatabase.MoveAsset(old, "…/_Fight<N> <Theme>/Fight<N> <Theme>.asset")` (create the folder first
  with `AssetDatabase.CreateFolder`). The guid survives the move, so `AllEncounters.fights` and
  `MapScene`'s `MapManager.points` array (one point per fight, positional) need no changes at all. This
  is how `Fight3 Shield Breaker.asset` became `_Fight3 Orks/Fight3 Orks.asset`.
- **It doesn't exist** (campaign is getting longer): `duplicate_asset` the template fight into the new
  folder and append it to `AllEncounters.fights`. **Then call out loudly in the report** that
  `MapScene.unity`'s `MapManager.points` needs one more `MapEncounterPoint` placed and appended —
  `MapManager.AssignEncounters()` throws when `points.Length < fights.Count`. Don't build map points
  yourself unless asked; that's scene layout work.
- **It exists and is already themed** (has its own folder/roster): stop and ask — the user may want to
  insert rather than replace.

### 7c. Copy values from the template fight

Unless the user specifies values, the fight is a copy of the template — every field (enemy avatar, HP,
AI tuning, loadout/reward flags, reward amount, Dirty/Clean Triple indices and stabilization, both
comeback ladders, ludo progress). Do it in one `execute_script` rather than field-by-field, so a
future `FightSO` field is never silently missed:

```csharp
var template = AssetDatabase.LoadAssetAtPath<FightSO>(templatePath);
var fight = AssetDatabase.LoadAssetAtPath<FightSO>(fightPath);
var roster = AssetDatabase.LoadAssetAtPath<CreaturesSO>(rosterPath);
string keepName = fight.name;
string keepId = string.IsNullOrEmpty(fight.fightId) ? $"battle_{N}" : fight.fightId;
EditorUtility.CopySerialized(template, fight);   // copies m_Name too — restore it below
fight.name = keepName;
fight.fightId = keepId;
fight.enemyData.creatures = roster;               // EnemyData is a struct: if assigning through a copy,
                                                  // write the whole struct back
EditorUtility.SetDirty(fight);
AssetDatabase.SaveAssets();
```

Then re-read the `.asset` YAML and check: `m_Name` matches the file name, `fightId` is unique across
all fights (`battle_<N>`), `enemyData.creatures` → the new roster's guid, and everything else is
byte-for-byte the template's values.

**Always say in the report that the enemy avatar, HP and AI tuning were copied from the template** —
the avatar in particular is usually themed to the template's fight (e.g. `OrkBreaker` on a ghost
fight), and swapping it is a one-field change the user will likely want.

### 7d. Docs

`docs/Encounters.md` lists the fight assets and which fights still fall back to `G.DefaultCreatures`
(no roster). Update both whenever a fight is moved/created or gains a roster (CLAUDE.md rule 18).

## 8. Do not touch player-visibility lists

**Never** add the new SO to `_DefaultCreatures.asset` (the single starter-per-type pointer) or
`RewardListSO.asset` (the reward-card unlock pool) as part of this skill — those decide whether a
creature is player-usable, a separate decision from "is this creature wired correctly." Only touch them
if explicitly told this specific creature should become player-obtainable. Same for the roster: a
fight's `CreaturesSO` is enemy-only; never point `_DefaultCreatures`, `G`, or any `CampaignDebugTool`
override at it (CLAUDE.md rule 29).

## 9. Compare against the type parent / a known-good sibling

`Demon.prefab` (Archer) is the reference. Diff structurally — same components present, same fields
overridden, nothing extra added. Don't go deeper than the checks above (rule from the user: "don't go
deep here").

## 10. Report

For each creature processed, give a short per-item status: what was already correct, what you fixed,
and — called out clearly, not buried — anything still blocked on the user (almost always: a missing
icon file). If you made a judgment call (which projectile to assign, rewritten flavor text), say so
explicitly so it's easy to override.

For the encounter (step 7): whether the `FightSO` was moved from a placeholder or newly created, which
template fight its values came from, the roster asset path, and whether `MapScene`'s `MapManager.points`
needs a new point (only when a fight was appended). Flag the copied enemy avatar.

## Testing

Unless told not to: `check_compile_errors`, then use the `playtest-creature-attack` skill's `BalanceTool`
pattern to spawn the creature and confirm it attacks with no exceptions and the correct projectile.

- **Spawn the new SO directly** instead of relying on `SpawnEnemyArcher()` etc. (those read
  `G.EnemyCreatures`, which only holds the new roster if the current encounter is the new fight):
  `tool.SpawnEnemy(AssetDatabase.LoadAssetAtPath<CreatureSO>("…/GhostArcher.asset"))` plus something on
  the player side, then `tool.PlayBattle()`. Don't tick `CampaignProgressTool`/`CampaignDebugTool`
  checkboxes to reach the fight — those are persisted scene state that silently keeps applying later
  (CLAUDE.md rule 29).
- **Shocked fix:** call `StatusesManager.ApplyShock()` directly (it's public) via `execute_script` and
  confirm the shaker's resolved `TargetTransform` is the creature's own mesh, not null and not the
  `ShockedFeedback` anchor itself.
- **Encounter (step 7), edit mode is enough:** load `AllEncounters.asset` and confirm
  `fights[N-1]` is the `Fight<N> <Theme>` asset and its `enemyData.creatures.archer/tank/mage` resolve to
  the new SOs, and each resolves through `GameCatalog.FindArcher/FindTank/FindMage(id)`.

## Related

- `docs/Battle.md` — `Health`/`StatusesManager` architecture.
- `docs/Campaign.md` — `GameCatalog`, `RunState`, why `id` fields need to survive a JSON round-trip.
- `docs/Encounters.md` — `FightSO`/`EnemyData`/`EncounterListSO`, `ResolveEnemyCreatures`' fallback,
  `MapManager.points` ordering (step 7).
- `playtest-creature-attack` skill — spawning/attack verification pattern reused in Testing above.
