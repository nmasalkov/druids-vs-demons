# `G` — Global Service Locator

## What this system does

`G` (`Assets/Game/_Scripts/Global/G.cs`) is a scene singleton that most gameplay code goes through to
reach the two sides' `HeroView`/`CreaturesManager`/`Hero` and the shared creature/nuke/spell pool,
instead of holding direct references to those objects. It's the standard "reach other systems through
here" entry point for this codebase.

## Key file

- `Global/G.cs` — the whole system is one file: a `MonoBehaviour` singleton (`Instance` set in
  `Awake()`), a handful of `[SerializeField]` Inspector-wired references, and static properties/
  methods wrapping them.

## Fields and static accessors

- `defaultCreatures` (`CreaturesSO`), `defaultNukes` (`NukesSO`), `defaultSpells` (`SpellsSO`) —
  exposed as `G.DefaultCreatures`/`DefaultNukes`/`DefaultSpells`. This is the **player's** pool for
  real battles (`SlotMachine`'s player instance reads it — see `docs/SlotMachine.md`) and is also what
  `BalanceTool` spawns test units from for both sides. Inspector-assigned by default (pointing at the
  `_DefaultCreatures.asset`/`DefaultNukes.asset`/`DefaultSpells.asset` fallback assets), but overridden
  at runtime by a campaign run — see `ApplyCampaignLoadout` below.
- `enemyCreatures`/`enemyNukes`/`enemySpells` — exposed as `G.EnemyCreatures`/`EnemyNukes`/
  `EnemySpells`. The **enemy's** pool for real battles — `ApplyCampaignLoadout` never touches these.
  `enemyCreatures` is now overridden per fight by `ApplyCampaignEnemyCreatures` (below), driven by
  `FightSO.enemyData.creatures` — see `docs/Encounters.md`. `enemyNukes`/`enemySpells` stay
  Inspector-assigned fixed defaults; no per-fight data exists for those yet.
- `playerView`/`enemyView` (`HeroView`) — exposed as `G.PlayerView`/`EnemyView`, and further as
  `G.PlayerCreaturesManager`/`EnemyCreaturesManager` (`.CreaturesManager`) and `G.PlayerHero`/
  `EnemyHero` (`.Hero`). Which `HeroView` is "player" vs "enemy" is purely which Inspector slot it's
  dragged into on `G` — nothing on `HeroView`/`Hero` itself encodes side.
- `testMode` (`bool`) — exposed as `G.TestMode`. Gates a debug-only `Update()` check
  (`Keyboard.current.jKey` spawns a test XP gem via `ExperienceManager`).
- `G.EncounterList` — `CampaignManager.Instance.EncounterList` (`docs/Encounters.md`), not a
  field of `G` itself. Added for consistency with `G`'s "reach other systems through here" role
  rather than routing through `CampaignManager` directly for this one lookup.
- `G.Rigger` — `SlotMachineRigger.Instance`, same forwarding pattern as `G.EncounterList`/
  `G.RewardList` (not a field of `G` itself — `SlotMachineRigger` is a sibling component on `Global`,
  not data `G` owns). `SlotMachine`/`SlotColumn` call this to decide every roll's results — see
  `docs/SlotMachine.md`.

## `ApplyCampaignLoadout` — the one mutation point

```csharp
public static void ApplyCampaignLoadout(CreaturesSO creatures, NukesSO nukes, SpellsSO spells)
{
    Instance.defaultCreatures = creatures;
    Instance.defaultNukes = nukes;
    Instance.defaultSpells = spells;
}
```

A **static** method (not an instance method) — callers write `G.ApplyCampaignLoadout(...)`, not
`G.Instance.ApplyCampaignLoadout(...)`, matching every other `G.*` access pattern in the codebase.
Called by `CampaignStateManager` whenever `BattleScene` is entered, to swap in a campaign-run-resolved
loadout in place of the Inspector-wired defaults. Only ever touches `defaultCreatures`/`defaultNukes`/
`defaultSpells` (the player's pool) — `enemyCreatures`/`enemyNukes`/`enemySpells` are never written by
this, by design (see Gotchas below). If `CampaignStateManager` is absent from a scene, this is never
called and `G` keeps working exactly as it always has, off its own serialized fields — no hard
dependency in either direction. Full detail on what builds the `CreaturesSO`/`NukesSO`/`SpellsSO`
passed in here, and why it's safe regardless of component initialization order:
[`docs/Campaign.md`](Campaign.md).

## `ApplyCampaignEnemyCreatures` — per-fight enemy roster

```csharp
public static void ApplyCampaignEnemyCreatures(CreaturesSO creatures)
{
    Instance.enemyCreatures = creatures;
}
```

Sibling to `ApplyCampaignLoadout` but for the enemy side's creature pool only (`enemyNukes`/
`enemySpells` stay untouched — no per-fight data for those yet). Called from
`CampaignStateManager.ApplyEncounterToScene()` (`docs/Encounters.md`) with
`ResolveEnemyCreatures(fight)`'s result: `fight.enemyData.creatures` if set, **else `G.DefaultCreatures`**
(logged as a warning) — never `G.EnemyCreatures` itself. See the Gotcha below for why the fallback
target matters.

## Gotchas

- **The enemy-creatures fallback must target `G.DefaultCreatures`, never `G.EnemyCreatures` itself.**
  `G.EnemyCreatures` is a plain mutable field with no reset between encounters — if a fight without
  its own `enemyData.creatures` fell back to "whatever `G.EnemyCreatures` currently holds", it would
  silently inherit whatever the *previous* fight last wrote there (Fight 2 running right after Fight 1
  set a demon roster would keep rolling demons forever, with nothing to blame in Fight 2's own data).
  `G.DefaultCreatures` is safe to fall back to instead specifically because `ApplyLoadoutToG()` always
  runs first in `EnterBattleScene()` and recomputes it fresh from `RunState` every single encounter —
  it's never a leftover value. See CLAUDE.md's player/enemy creature pool rule and
  `docs/Encounters.md`'s Gotchas for the real, live-caught bug this caused (a leftover
  `CampaignDebugTool` override plus this exact wrong fallback made both sides roll the same demon
  roster).
- **`SlotMachine` picks its pool via its own `isPlayerMachine` flag, not by asking `G` which side is
  active.** `SlotMachine.GetActionOptions()` (see `docs/SlotMachine.md`) reads
  `G.DefaultCreatures`/`DefaultNukes`/`DefaultSpells` when `isPlayerMachine` is true, or
  `G.EnemyCreatures`/`EnemyNukes`/`EnemySpells` when false — a plain Inspector-set bool per instance
  (`true` on `SlotMachinePlayer/SlotMachine`, `false` on `SlotMachineEnemy/SlotMachine`, both in
  `BattleScene`), not derived from `GameManager.ActiveSide` (which tracks whose *turn* it is, not
  which physical machine a given `SlotMachine` component is). This was a real, live-caught bug: before
  the split existed, both sides' machines read the exact same shared pool, so a player's campaign
  loadout pick (e.g. swapping in a stronger tank via the loadout screen) leaked straight into the
  enemy's roster for the next battle too. If you add a third `SlotMachine` instance anywhere, remember
  to set `isPlayerMachine` explicitly — it defaults to `true`, so a forgotten enemy-side instance
  would silently re-introduce the shared-pool bug rather than fail loudly.
- **`Awake()` only sets `Instance`.** Nothing else in `G` runs cross-script logic in `Awake()`, per
  the project-wide rule — anything that reads another singleton (e.g. `CampaignStateManager`) does so
  from its own `Start()` or later.
