---
name: validate-rewards
description: Audit the between-encounter reward system end to end — every RewardSO in RewardListSO checked for functional wiring (does the effect actually reach gameplay code, player-only), text/effect integrity (card copy matches the real numbers and behavior), icon integrity (explicit or fallback sprite, placeholder/vendor-pack art, boost overlay), reachability/data issues (drawn in a normal run, uniqueness, stale ids, broken script refs), and a balance sanity pass against current live numbers. Produces a compact summary plus a severity-ranked table of only the rewards needing attention. Use when the user asks to validate/audit/check rewards, before a playtest/alpha build, or after adding/changing a reward, boost, action balance, or reward icon ("ValidateRewards").
---

# Validate Rewards

A read-only audit of every reward the player can be offered between fights. **Do not change
anything** unless the user asks — the output is a cleanup checklist. Prefer live data over docs; when
`docs/Rewards.md` disagrees with code/assets, report the mismatch.

Read `docs/Rewards.md` first (hierarchy, `RewardBonuses`, draw rule), then work from the actual
assets and code below.

## 0. Inventory

- Catalog: `Assets/Game/_ScriptableObjects/_Campaign/RewardListSO.asset` (`allRewards`) → assets in
  `_ScriptableObjects/_Campaign/_Rewards/`. Also list any reward asset in that folder that is **not**
  in `allRewards` (orphan) and any `allRewards` entry that is null/missing.
- Dump every reward through the Editor, not just YAML — a broken `m_Script` guid only shows up as
  a null/wrong type when loaded. Use `execute_script` (class with `public static string Execute()`,
  `filePath` in the scratchpad) that loads `RewardListSO` and prints per entry: asset name, runtime
  type, `id`, `rewardName`, `description`, `typeLabel`, `unique`, `EffectiveIcon` name +
  `AssetDatabase.GetAssetPath(icon)`, `ShowsBoostOverlay`, and the subtype fields
  (`CreatureClassBoostSO.creatureClass/percentage`, `ActionStatBoostSO.action/percentage`,
  improvement tunables, `bonusHp`, `bonusEnergy`, `CreatureRewardSO.creature`).
  - Gotcha: after changing an asset's script type via YAML the loaded `RewardListSO` can hold stale
    `null`s — `Resources.UnloadAsset(list)` and reload before trusting a null (verify the serialized
    reference with `SerializedObject` too).
- Pull the balance numbers the rewards touch: the linked `ActionSO`s (`_ScriptableObjects/_Actions/`
  — `damagePerLevel`, `chancePerLevel`, `buffMultiplierPerLevel`, `hpPerLevel`/`healPerLevel`),
  `CreatureSO.levelStats`, `RunState` defaults (`maxHp`, `currentEnergy`), `CampaignStateManager.startingEnergy`,
  `EnergyController.baseRerollCost`, and every `FightSO` in `AllEncounters.asset`
  (`hasReward`, `rewardAmount`, `hasLoadoutPick`, `enemyData.hp`).

## 1. Functionality (does it actually work?)

For each reward, trace claim → storage → read site:

- `Claim()` writes the right `RunState` list/field; `IsOwned()` reads the same one.
- The stored id is actually **read** somewhere in gameplay: grep `RewardBonuses.` call sites and
  confirm each boost type has a live reader —
  - `CreatureClassBoostSO` → `AttacksResolver.Mechanics.cs` `ResolveTeam` (damage) **and**
    `EstimateFirepower`, plus `Creature.GetMaxHealth` (HP).
  - `ActionStatBoostSO` → the action's resolver calls `Boosted(...)` / `ActionBonusFraction`.
  - `ActionImprovementSO` subclass → the action's resolver has `IsImproved(caster, out <Type>)`.
    An improvement type with no resolver branch = **BLOCKER** (inert card).
  - `HpBoostRewardSO` → `CampaignStateManager.GetMaxHpBonus` → `Hero.GetMaxHealth`.
  - `BonusEnergyRewardSO` → `run.currentEnergy +=` (uncapped).
  - `CreatureRewardSO` → `gatheredCreatureIds` → `LoadoutPickEncounter.IsGathered`; the creature
    must be in `GameCatalog.allCreatures` and a later fight must have `hasLoadoutPick`.
- **Player-only rule:** every read passes `isPlayer` (resolvers via `caster == G.PlayerHero`). Any
  new read site that doesn't — or any SO that the enemy also rolls (`G.enemyNukes/enemySpells`
  share the player's assets) being boosted without a side check — is **HIGH**.
- Instant/animated parity (CLAUDE.md rule 7): the effect lives in a shot's `Apply()`, RNG is rolled
  in the resolver, and the animation calls `Apply()` on every path (e.g. Charm's fail branch).
- Does the effect change something the player can *see*? (A buried number nobody notices is a
  design flag, not a functional one.)
- Optional live proof in Play mode (see the `staged-board-decays-in-play-mode` memory — stage and
  assert in one coroutine, log a tagged line, read it with `get_unity_logs(search_term:)`):
  temporarily set `CurrentRun.boostRewardIds` to all boosts **before** spawning, spawn via
  `BalanceTool.SpawnEnemy*/SpawnPlayer*`, construct resolvers directly (`new ShockResolver()` etc.),
  `Resolve(...)` once with the player hero as caster and once with the enemy hero, compare shots,
  then restore `boostRewardIds` in a `finally`. Never `Save()` during the test.

## 2. Text / effect integrity

- Description names the exact effect and number, and the number equals the data
  (`bonusEnergy`, `bonusHp`, `percentage`, improvement tunables). A mismatch (e.g. card says +50,
  asset grants 20) is **HIGH**.
- Wording matches the implementation's semantics: "+20% damage and HP" must boost both; a
  percentage applied to a *multiplier's bonus part* must say so; relative vs absolute % is clear.
- No developer notes in player-facing copy ("currently no effect", "TODO").
- `rewardName`/`typeLabel` are consistent across the same kind (Boost/Improve/Status/Creature).
- For creature unlocks, the creature's own `description` must be true to its stats — compare its
  `levelStats` with the base creature of the same class ("hits harder" with identical stats = false).
- Could a player compare this card against the other two in a draw? If not, suggest showing numbers.

## 3. Icon integrity

- Every reward resolves a non-null `EffectiveIcon` (explicit `icon`, else `FallbackIcon` — the
  linked action/creature `cardSprite`). Null = **HIGH** (blank card).
- Expected sources: action/creature art in `Assets/Game/_Sprites/_Slot_Cards/_Creatures|_Nukes|_Spells/`,
  reward art in `Assets/Game/_Sprites/_Rewards/`. Any icon path outside `Assets/Game/`
  (vendor packs — Feel demos, Spine atlases, GUI packs) is a placeholder → flag it.
- Every `ActionSO.cardSprite` for a reward-linked action should come from `_Slot_Cards/`.
- Two different rewards sharing the exact same sprite with nothing to tell them apart (e.g. a Big
  creature using the base creature's icon) → **MEDIUM**.
- Boost overlay: `RewardCard.boostOverlay` (the `BoostOverlay` child of
  `_Prefabs/_UI/_Cards/RewardCard.prefab`) must be wired, and `ShowsBoostOverlay` must be true for
  every `BoostSO` and false for rewards whose icon already carries the plus (Vitality, Energy Cell)
  and for creature unlocks.
- **Import mode:** every icon/card PNG must be imported as a **Single** sprite (`spriteMode: 1` in its
  `.meta`). Unity sometimes imports new art as Multiple and auto-slices it — detached bits (sparkles,
  extra stars) become separate sprites and the card shows only the `_0` piece (a real bug: Charm and
  Starfall showed one heart / one star). Even a one-slice Multiple is auto-cropped, so it frames
  differently from full 512px sprites. Check it with `grep spriteMode <png>.meta` plus the sprite
  count. To fix, set `TextureImporter.spriteImportMode = Single`, run `SaveAndReimport`, then
  **re-point every reference**: the sprite's fileID changes, so grep the png's guid across
  `.asset`/`.prefab`/`.unity` files first.
- **Render the cards, don't guess:** build a temporary additive scene with a Screen Space – Camera
  canvas, instantiate `RewardCard.prefab` once per reward, call `Init(reward)`, render the camera to
  a RenderTexture, save a PNG to the scratchpad and `Read` it. Close the temp scene afterward.
  Check that each icon is complete, the overlay shows only on boosts, and the description fits the
  text box (the box holds about 3 lines; longer copy overflows the card frame).
- Look at questionable sprites with the `Read` tool (it renders PNGs) before judging readability/style.

## 4. Reachability / data

- Simulate `RewardDrawer.DrawThree` for representative `RunState`s (fresh; all creatures owned;
  all boosts owned; everything owned) — every reward appears in at least one, no duplicate cards in a
  draw while alternatives exist, no null slot.
- Count reward screens in a run (`FightSO.hasReward`) vs pool sizes: does any category run dry or
  dominate slots in a normal run?
- Unique rewards are excluded once owned (`IsOwned` reads the list `Claim` writes).
- Ids: unique across the catalog, stable, matching the asset's purpose; flag renamed ids that old
  saves may still hold (they resolve to null and are silently ignored).
- Linked `action`/`creature` references are non-null and point at current assets (not a moved or
  obsolete copy); boosted actions are actually obtainable by the player (in the default loadout or
  `gatheredNukeIds/gatheredSpellIds` seeds).

## 5. Balance sanity (flag, don't rebalance)

Evaluate each card against the *current* numbers from step 0:

- Express the effect in concrete terms (e.g. "+2.4 pp charm chance per enemy creature",
  "Starfall L1 5 → 6 dmg vs a 100 HP hero", "×1.24 → ×1.384 buff") and compare with the other
  cards it will be drawn against (slot 1 is always Vitality/Energy; slot 2 creature; slot 3 boost).
- Flag: too weak to notice, likely auto-pick, likely never-pick, redundant with another card, too
  situational, or a card whose value collapses after recent balance changes.
- Energy: compare `bonusEnergy` against fight `rewardAmount`s and the doubling reroll cost.
- HP: compare `bonusHp` with `RunState.maxHp` and enemy damage output.

## Output format

Keep it short — a checklist, not a design doc.

1. **Summary:** total reviewed / fine / with issues / broken-placeholder-or-needs-design-decision.
2. **Table (only rewards needing attention):**
   `| Reward | Type | Problem | Severity | Suggested Next Action |`
   Severity: **BLOCKER** (broken / no effect / unfinished), **HIGH** (bad for balance or very
   confusing), **MEDIUM** (fix before playtest if cheap), **LOW** (cosmetic/polish).
   Next action is a few words ("Replace placeholder icon", "Fix text: data grants 20").
3. **Docs vs live data mismatches** (one line each), if any.
4. **Design decisions required** — only where the fix needs deciding what the reward should do.
5. **Quick wins** — icons, copy, obvious data errors, tiny number tweaks.
6. **Safe to leave for later.**

State what was verified live vs by reading code/assets only.
