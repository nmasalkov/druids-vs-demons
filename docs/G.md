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
  exposed as `G.DefaultCreatures`/`DefaultNukes`/`DefaultSpells`. These are Inspector-assigned by
  default (pointing at the `_DefaultCreatures.asset`/`DefaultNukes.asset`/`DefaultSpells.asset`
  fallback assets), but can be overridden at runtime — see `ApplyCampaignLoadout` below.
- `playerView`/`enemyView` (`HeroView`) — exposed as `G.PlayerView`/`EnemyView`, and further as
  `G.PlayerCreaturesManager`/`EnemyCreaturesManager` (`.CreaturesManager`) and `G.PlayerHero`/
  `EnemyHero` (`.Hero`). Which `HeroView` is "player" vs "enemy" is purely which Inspector slot it's
  dragged into on `G` — nothing on `HeroView`/`Hero` itself encodes side.
- `testMode` (`bool`) — exposed as `G.TestMode`. Gates a debug-only `Update()` check
  (`Keyboard.current.jKey` spawns a test XP gem via `ExperienceManager`).
- `G.EncounterList` — `CampaignManager.Instance.EncounterList` (`docs/Encounters.md`), not a
  field of `G` itself. Added for consistency with `G`'s "reach other systems through here" role
  rather than routing through `CampaignManager` directly for this one lookup.

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
loadout in place of the Inspector-wired defaults. If `CampaignStateManager` is absent from a scene,
this is never called and
`G` keeps working exactly as it always has, off its own serialized fields — no hard dependency in
either direction. Full detail on what builds the `CreaturesSO`/`NukesSO`/`SpellsSO` passed in here,
and why it's safe regardless of component initialization order: [`docs/Campaign.md`](Campaign.md).

## Gotchas

- **No side flag anywhere.** `SlotMachine.GetActionOptions()` (see `docs/SlotMachine.md`) reads
  `G.DefaultNukes`/`DefaultSpells`/`DefaultCreatures` unconditionally regardless of which side's
  machine is asking — both sides roll from the exact same pool. Don't assume overriding
  `G.DefaultCreatures` etc. only affects the player; today it affects both sides identically, by
  design (that's the existing, pre-campaign behavior too).
- **`Awake()` only sets `Instance`.** Nothing else in `G` runs cross-script logic in `Awake()`, per
  the project-wide rule — anything that reads another singleton (e.g. `CampaignStateManager`) does so
  from its own `Start()` or later.
