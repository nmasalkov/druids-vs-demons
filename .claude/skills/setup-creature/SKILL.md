---
name: setup-creature
description: Finish wiring a hand-added creature prefab (Mage/Tank/Archer) after the user has added its visual mesh and icon — links the type animator's Spine skeleton reference, the ShockedFeedback position-shaker target, any ranged projectile component present (MissileAnimator/BeamAnimator — not tied to a fixed Archer/Mage pairing), the matching balance CreatureSO, and GameCatalog registration. Use whenever the user says a new creature prefab was added and needs setup, or asks to check/finish a creature's wiring before it's playable as an enemy.
---

# Setup Creature

The user's workflow: they hand-build a new creature by duplicating (or nesting a new mesh under)
`Assets/Game/_Prefabs/Characters/Units/<Type>/<Type>ParentGreen.prefab`
(`CatapultParentGreen`=Archer, `_GolemParentGreen`=Tank, `DragonParentGreen`=Mage), swap in the Fantazia
monster mesh under `Visual`, and add the matching card icon under
`Assets/Game/_Sprites/_Creatures/`. Everything else — animator/Spine linking, status-feedback targets,
projectile assignment, the balance `ScriptableObject`, catalog registration — is this skill's job.

`Assets/Game/_Prefabs/Characters/Units/Archer/Demon.prefab` +
`Assets/Game/_ScriptableObjects/Actions/Creatures/Archers/Demon.asset` is the fully-correct reference
example — when in doubt, diff the creature you're setting up against Demon's pattern.

## 1. Locate the prefab and confirm its shape

- `Assets/Game/_Prefabs/Characters/Units/<Type>/<Creature>.prefab`, a Prefab Variant of
  `<Type>ParentGreen.prefab`.
- Root GameObject should be renamed to the creature's proper name — check via
  `get_game_object_info` (`prefabPath` param) or `get_unity_editor_state`; fix with `rename_game_object`
  if it's still the parent's name.
- Confirm `Visual` has exactly one mesh child (the parent's placeholder should already be removed by the
  user when they added their own mesh). If you find more than one, or none, stop and ask — don't guess
  which is the intended one.

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
uses (`Assets/Game/_Prefabs/Projectiles/*` — `Apple`, `FireBall`, `FireMagic`, `GreenMissile`,
`LightningBall`, `Sonar`, `Star`, `Stone`), **including type-parent-based creatures that use the parent prefab directly** (e.g. `Bubka`'s
`CreatureSO.creaturePrefab` points straight at `CatapultParentGreen.prefab`, so its runtime projectile
*is* whatever `CatapultParentGreen`'s own `MissileAnimator` default currently is). If the creature you're
setting up would otherwise silently collide with another creature's projectile (including a parent's
un-overridden default), **assign a different one and say so in your report** — don't ask the user to
pick unless nothing sensible is free.

## 5. Matching CreatureSO

`Assets/Game/_ScriptableObjects/Actions/Creatures/<Type>s/<Creature>.asset` (subclass `ArcherSO`/
`TankSO`/`MageSO`, extends `CreatureSO` → `ActionSO`). If it doesn't exist, `duplicate_asset` a sibling
in the same subfolder as a template, then fix every field below — never leave a duplicated asset's stale
values in place.

Fields (`get_game_object_info`/`set_property` with `asset_path`, or `execute_script` +
`AssetDatabase.LoadAssetAtPath` for anything the tool can't reach):

- **`id`** — unique across `GameCatalog.asset`'s `allCreatures` (check first). The project's existing
  convention is inconsistent (`camelCase`, plain lowercase, `<name><Type>`) — just guarantee
  uniqueness, don't force one fixed formula.
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
- **`levelStats`/`experienceReward`/`xpThresholds`/`healAmounts`** — just confirm non-empty/plausible.
  Don't invent new balance numbers unless asked.

## 6. Register in GameCatalog

Add the SO's guid to `Assets/Game/_ScriptableObjects/Campaign/GameCatalog.asset`'s `allCreatures` list
if it isn't already there. Easy to forget — doesn't fail to compile, just silently makes the creature
unreachable via `GameCatalog.FindArcher/FindTank/FindMage`. This is a plain `List<CreatureSO>` on a
`ScriptableObject` — editing it needs either `set_property` (if it supports list append cleanly; verify
by re-reading the asset after) or `execute_script` with `AssetDatabase.LoadAssetAtPath<GameCatalog>` →
`allCreatures.Add(so)` → `EditorUtility.SetDirty` → `AssetDatabase.SaveAssets()`.

## 7. Do not touch player-visibility lists

**Never** add the new SO to `_DefaultCreatures.asset` (the single starter-per-type pointer) or
`RewardListSO.asset` (the reward-card unlock pool) as part of this skill — those decide whether a
creature is player-usable, a separate decision from "is this creature wired correctly." Only touch them
if explicitly told this specific creature should become player-obtainable.

## 8. Compare against the type parent / a known-good sibling

`Demon.prefab` (Archer) is the reference. Diff structurally — same components present, same fields
overridden, nothing extra added. Don't go deeper than the checks above (rule from the user: "don't go
deep here").

## 9. Report

For each creature processed, give a short per-item status: what was already correct, what you fixed,
and — called out clearly, not buried — anything still blocked on the user (almost always: a missing
icon file). If you made a judgment call (which projectile to assign, rewritten flavor text), say so
explicitly so it's easy to override.

## Testing

Unless told not to: `check_compile_errors`, then use the `playtest-creature-attack` skill's `BalanceTool`
pattern to spawn the creature and confirm it attacks with no exceptions and the correct projectile. To
confirm the Shocked fix specifically, call `StatusesManager.ApplyShock()` directly (it's public) via
`execute_script` and confirm the shaker's resolved `TargetTransform` is the creature's own mesh, not
null and not the `ShockedFeedback` anchor itself.

## Related

- `docs/Battle.md` — `Health`/`StatusesManager` architecture.
- `docs/Campaign.md` — `GameCatalog`, `RunState`, why `id` fields need to survive a JSON round-trip.
- `playtest-creature-attack` skill — spawning/attack verification pattern reused in Testing above.
