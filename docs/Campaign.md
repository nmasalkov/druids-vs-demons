# Campaign / Meta Progression

## What this system does

Step 1 of turning the single self-contained `BattleScene` battle into a campaign: a series of battles
with a pre-battle loadout phase and a post-battle reward phase around each one, plus
player-improvable stats (max HP, reroll energy capacity) that persist across the run. This system is
the **data layer** only — a `RunState` that saves/loads via `PlayerPrefs`/JSON, and the wiring that
makes `BattleScene` pull its starting values from it instead of pure hardcoded defaults. It does not
implement the pre-battle/post-battle UI or reward selection — those are still follow-ups. Per-
encounter enemy avatar/HP variation *is* now solved, one layer up — see `docs/Encounters.md`.

**Both sides still roll from one shared creature/nuke/spell pool.** `SlotMachine.GetActionOptions()`
has no player/enemy distinction — overriding `G`'s creature/nuke/spell defaults from campaign data
keeps that behavior for both sides identically. Only the player's hero max HP is driven by
`RunState` directly (`Hero == G.PlayerHero` check in `Hero.GetMaxHealth()`); the enemy's max HP is
driven by the current `BattleSO` when one is active (`docs/Encounters.md`), falling back to its own
`HeroSO.health` otherwise. True per-encounter *creature* composition (different summonable creatures
per battle, not just a different enemy avatar/HP) still needs `SlotMachine` to gain a per-side
loadout source — not solved here.

## Key files

- `Global/Campaign/RunState.cs` — the save data: a plain `[Serializable]` C# class (not a
  `ScriptableObject` — see Gotchas), not saved directly, held by `CampaignManager`.
- `Global/Campaign/GameCatalog.cs` — SO listing every `CreatureSO`/`NukeSO`/`SpellSO` asset in the
  game; resolves `RunState`'s loadout id strings back to actual assets at battle start.
- `Global/Campaign/CampaignManager.cs` — singleton MonoBehaviour; loads/creates `CurrentRun` in
  `Awake()`, applies it to `G` and `EnergyController` in `Start()`, exposes `Save()`.
- `Global/Campaign/CampaignDebugTool.cs` + `Global/Campaign/Editor/CampaignDebugToolEditor.cs` —
  Editor-only override tool, mirrors `Global/Balance/BalanceTool.cs`'s pattern. Check a box, drag in
  a creature/nuke/spell asset (or set a number), press Play.
- `ScriptableObjects/ActionSO.cs` — gained a `public string id` field: the stable identifier
  `GameCatalog` looks assets up by (separate from `actionName`, which is just a display string).
- `ScriptableObjects/CreaturesSO.cs`/`NukesSO.cs`/`SpellsSO.cs` (renamed from `Default*SO` — the
  asset instances on disk keep their original `Default*.asset` file names since those specific
  instances genuinely are the fallback defaults) — reused both as `G`'s hardcoded fallback and as the
  shape `CampaignManager` builds a runtime instance into.
- `Global/G.cs` — gained the static `ApplyCampaignLoadout(CreaturesSO, NukesSO, SpellsSO)`, called
  once by `CampaignManager.Start()`. Full detail: [`docs/G.md`](G.md).
- `Global/GameManager/EnergyController.cs` — gained `ApplyCampaignEnergy(int)`, called from
  `CampaignManager.ApplyEncounterToScene()` (initial load and every encounter transition). No more
  local baseline — `CurrentEnergy` comes from `RunState.currentEnergy` exclusively; see
  `docs/Energy.md` and Gotchas.
- `Units/Hero.cs` — `GetMaxHealth()` reads `CampaignManager.Instance.CurrentRun.maxHp` for the
  player's `Hero` only.
- `Global/Campaign/CampaignProfileSO.cs` — Editor-authorable snapshot of a whole `RunState` (SO
  references instead of ids), pluggable into `CampaignDebugTool`'s "Use Debug Profile" slot. See
  below and CLAUDE.md rule 21.

## `RunState` shape

```csharp
public int saveVersion = 1;          // for future save migration

public int maxHp = 100;               // matches HeroSO.health's existing default
public int energyCapacity = 50;       // upper bound a victory reward clamps currentEnergy to
public int currentEnergy = 50;        // the only field a battle itself changes — see docs/Encounters.md

public string archerId = "archer", tankId = "tank", mageId = "mage";
public string nukeAId = "firemagic", nukeBId = "starfall", nukeCId = "shock";
public string spellAId = "battlecry", spellBId = "charm", spellCId = "shield";

public int currentEncounterIndex = 0; // now consumed — see docs/Encounters.md
```

The 9 loadout fields are ids, not direct SO references — `ScriptableObject` references don't survive
a `JsonUtility` round-trip through `PlayerPrefs`. Every default above mirrors today's hardcoded
game exactly (`HeroSO.health`, `EnergyController.startingEnergy`, and — id-for-id — whatever
`_DefaultCreatures.asset`/`DefaultNukes.asset`/`DefaultSpells.asset` already point at), so a
brand-new run (nothing saved yet) behaves identically to the game as it exists without this system,
until something actually changes a field. Note `Fireball.asset` exists in the catalog (id
`"fireball"`) but isn't any run's default — `DefaultNukes.asset` actually points at FireMagic/
Starfall/Shock, not Fireball, despite the filename suggesting otherwise.

## Load → resolve → apply flow

1. `CampaignManager.Awake()`: `CurrentRun = PlayerPrefs.HasKey(SaveKey) ? JsonUtility.FromJson<RunState>(...) : new RunState()`.
   Self-contained — only reads `PlayerPrefs`, no other script.
2. `CampaignManager.Start()` (runs before every default-order `Start()` in the scene — see the Script
   Execution Order note in Gotchas):
   - `ApplyLoadoutToG()` builds one runtime `CreaturesSO`/`NukesSO`/`SpellsSO` via
     `ScriptableObject.CreateInstance<T>()`, resolving each of the 9 ids through `GameCatalog`
     (`FindArcher`/`FindTank`/`FindMage`/`FindNuke`/`FindSpell`). Any id that doesn't resolve
     (empty/unknown) falls back to whatever `G`'s own Inspector-wired default asset already has for
     that slot, logging a warning. Result is handed to the static `G.ApplyCampaignLoadout(...)`.
   - `ApplyEncounterToScene()` calls `EnergyController.Instance.ApplyCampaignEnergy(CurrentRun.currentEnergy)`,
     which fires `OnEnergyChanged` so `EnergyDisplay` picks up the value (belt-and-suspenders —
     `CampaignManager`'s execution order already guarantees this runs before `EnergyDisplay.Start()`
     reads it once, but the event fire also makes this correct if that ordering ever changes). The
     same method is reused for encounter transitions that don't reload the scene — see
     `docs/Encounters.md`.
3. `Hero.GetMaxHealth()` doesn't get pushed a value — it reads `CampaignManager.Instance.CurrentRun.maxHp`
   directly (lazily) whenever called, which happens to be from the player `Hero`'s own `Start()` (via
   `InitHealth()`). Safe purely from the Awake-before-Start guarantee: `CampaignManager.Awake()` has
   already run by the time any `Start()` runs, anywhere in the scene.
4. `CampaignManager.Save()` — `PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(CurrentRun))`. Not
   called automatically by anything yet (no pre-battle/post-battle phase exists to trigger it) —
   available for whatever calls it next (a future reward-selection step, or manual testing).
   `SaveKey` (`"DvD_RunState"`) is `public const` specifically so tooling (see the debug tool's
   "Saved Run" section below) can reference it instead of duplicating the string.

## `CampaignDebugTool`

Editor-only, lives on a `CampaignDebugTool` GameObject next to `BalanceTool` in `BattleScene.unity`.
One `override<Field>` bool + matching value field per `RunState` entry. The 9 loadout fields are
**direct SO reference fields** (`ArcherSO archer`, `TankSO tank`, `MageSO mage`, `NukeSO nukeA/B/C`,
`SpellSO spellA/B/C`) — drag an asset into the Inspector like any other object field — not id strings
or a dropdown; the tool converts the picked asset's `.id` into the matching `RunState` id string in
`Awake()`. `RunState` itself still stores plain id strings (see above) — only this debug tool's own
authoring surface uses direct references, for convenience.

- Applies in **`Awake()`**, mutating `CampaignManager.Instance.CurrentRun` directly for every
  toggled-on override — before `CampaignManager.Start()` reads it back out to build the runtime
  loadout SOs, and before `Hero.Start()` reads `CurrentRun.maxHp`. This needs `CampaignManager.Awake()`
  to run first, which is the one place in this system that relies on Unity's **Script Execution
  Order** project setting (`CampaignManager` before `CampaignDebugTool`) rather than the
  Awake-before-Start guarantee alone — see Gotchas.
- Never calls `Save()` — overrides are in-memory-only for the current Play session, so testing never
  overwrites a real saved run.
- No mid-session "reapply" button. To try a different combination, stop and re-enter Play mode.
- Its custom Editor also draws a **"Saved Run (PlayerPrefs)"** section, independent of the override
  fields above: Unity has no built-in PlayerPrefs browser, so this is the debug affordance for it.
  Reads `PlayerPrefs.HasKey/GetString(CampaignManager.SaveKey)` directly and pretty-prints the JSON
  via `JsonUtility.ToJson(JsonUtility.FromJson<RunState>(raw), true)`; shows "no saved run yet" if the
  key is absent (the common case — see the `Save()` bullet above). Works in Edit mode too, not just
  Play mode. A "Clear Saved Run" button calls `PlayerPrefs.DeleteKey` + `Save()` directly — separate
  from, and not gated by, the override toggles.

### "Use Debug Profile" — `CampaignProfileSO`

A coarser alternative to the per-field overrides above: `CampaignDebugTool.useDebugProfile` +
`debugProfile` (a `CampaignProfileSO` asset, `Game/Campaign/Campaign Profile` in the Create menu).
When checked, `Awake()` applies **every** `RunState` field from the profile wholesale
(`ApplyDebugProfile`) and skips the granular override checks entirely — no mixing the two. Exists
so a whole test scenario (loadout + stats + which encounter) can be saved as one reusable asset
instead of re-checking a dozen boxes every session — drag in `CampaignProfileSO` assets for
different scenarios ("mid-run, low energy," "final boss, maxed loadout," etc.) and swap between
them.

`CampaignProfileSO`'s shape mirrors `RunState` field-for-field, but with direct SO references
(`ArcherSO archer`, `NukeSO nukeA`, etc.) instead of id strings — same convenience tradeoff the
per-field overrides already make, and for the same reason (id strings exist purely so `RunState`
survives a `JsonUtility`/`PlayerPrefs` round-trip; a debug-only asset has no such constraint).
`ApplyDebugProfile` converts each reference to its `.id` the same way the granular overrides do.
Profile fields are treated as mandatory once `useDebugProfile` is checked (rule 5) — an unassigned
archer/tank/mage/nuke/spell throws immediately rather than silently resolving to `null`.

**`currentEncounterIndex` needs one more step than the other fields**: setting
`run.currentEncounterIndex` alone has no effect on which encounter loads, since
`CampaignProgressManager` (`docs/Encounters.md`) bootstraps its own index from PlayerPrefs
independently of the live `CurrentRun` object. `ApplyDebugProfile` also calls
`CampaignProgressManager.Instance.SetSessionEncounterIndexOverride(profile.currentEncounterIndex)`
— see `docs/Encounters.md`'s "Debug-only surface" section for why that method exists.

**Reminder (CLAUDE.md rule 21): every new persisted `RunState` field needs a matching field on
`CampaignProfileSO` too, plus a granular override pair on `CampaignDebugTool` — nothing enforces
this at compile time, so it's easy to add a `RunState` field and forget the other two.**

## Gotchas

- **If you change `CampaignDebugTool`'s field layout (add/remove/retype fields), remove and re-add
  the component on the scene GameObject afterward instead of trusting the old serialized values.**
  Caught live: after changing its 9 loadout fields from `string ...Id` to direct SO references
  (`ArcherSO archer`, etc.), the scene's already-serialized `CampaignDebugTool` instance ended up with
  `overrideMaxHp`/`maxHp` silently corrupted to stale values (`true`/`1`) across a couple of
  recompiles, despite `set_property` calls and Editor inspection both showing a clean `false`/`100`
  state moments earlier — the corruption only showed up when actually read at runtime. Removing and
  re-adding the component (`remove_component` + `add_component` in Coplay terms, or delete-and-re-add
  in the Editor) forces genuinely fresh class-default values with no leftover serialized baggage, and
  resolved it. Re-verify with a runtime read (not just an Editor inspector snapshot) after any such
  layout change — the Editor's displayed value and the value a running instance actually holds can
  diverge across recompiles.
- **`CampaignDebugTool` is the one place Awake-vs-Awake ordering matters**, because it needs to mutate
  `CurrentRun` *before* `CampaignManager` reads it back out — but both of those are still within the
  Awake phase relative to each other, which Unity doesn't order by itself. `Hero` needs no special
  ordering: it reads `CampaignManager.Instance.CurrentRun.maxHp` lazily, straight from its own
  `Start()`, which is safe purely from the Awake-before-Start guarantee. Don't add a third script that
  also needs to mutate `CurrentRun` pre-`Start()` without reconsidering this — it doesn't scale past
  the two-script execution-order pin.
- **`EnergyController.CurrentRerollCost` (not `CurrentEnergy`) is the one thing that must still be
  set in `Awake()`, not `Start()` — this was a real bug, caught live in testing, not just reasoned
  about.** An earlier version of this system moved both `CurrentEnergy`/`CurrentRerollCost`
  initialization from `Awake()` into `Start()`, reasoning (wrongly) that it needed to run after
  `CampaignManager`. That broke `SlotColumn.Start()`, which reads
  `EnergyController.Instance.CurrentRerollCost` synchronously to seed the reroll-cost label —
  Start()-vs-Start() order between unrelated components is unspecified, so the label intermittently
  showed `0` instead of the real cost depending on which `Start()` ran first. `CurrentRerollCost`
  stays self-contained in `Awake()` for exactly this reason (`baseRerollCost`, no `CampaignManager`
  dependency). `CurrentEnergy` is different — it's campaign-persistent data now (`docs/Energy.md`),
  so it's deliberately left unset until `CampaignManager.ApplyEncounterToScene()` calls
  `ApplyCampaignEnergy()`, safe because of `CampaignManager`'s early Script Execution Order (next
  bullet), not because of the Awake/Start split.
- **`CampaignManager`'s early Script Execution Order (`-100`) is why applying directly from its own
  `Start()` is safe**, not despite it. Because `-100` is earlier than every default-order script,
  `CampaignManager.Start()` runs before `EnergyController.Start()`/`EnergyDisplay.Start()`/etc. For
  `CurrentRerollCost` this no longer matters for correctness (it's set standalone in `Awake()`), only
  for `EnergyDisplay` showing the right value on the very first frame instead of one frame later via
  the `OnEnergyChanged` catch-up — but for `CurrentEnergy` this ordering is load-bearing: nothing
  else ever sets it, so if `CampaignManager.Start()` ran *after* some consumer's `Start()`, that
  consumer would read a stale `0` with no catch-up until the next `OnEnergyChanged` fire.
- **`RunState` is plain data, not a `ScriptableObject`, on purpose.** SOs are Editor-time assets;
  mutating one's fields at runtime doesn't persist in a build and risks polluting shared asset state.
  `CreaturesSO`/`NukesSO`/`SpellsSO` stay SOs because they're `G`'s existing shape (and the
  `ScriptableObject.CreateInstance` runtime instances `CampaignManager` builds from them are
  disposable, never saved as assets) — don't confuse "SO used at runtime" with "data that needs to
  survive a session," which is what `RunState`/`PlayerPrefs` is for.
- **`CampaignManager` itself is still not `DontDestroyOnLoad`.** It lives on the same
  `Global/GameManager` GameObject as `G`/`GameManager`/`EnergyController`/`RollStateManager` (see
  `docs/GameLoop.md`) and is recreated on every `BattleScene` load — `DontDestroyOnLoad` on that
  GameObject would drag every battle-scoped sibling singleton into persistent scope too.
  `CampaignProgressManager` (see `docs/Encounters.md`) is now the one exception in the codebase: a
  dedicated, separately-placed root GameObject that *is* `DontDestroyOnLoad`, specifically because it
  owns campaign navigation state that needs to survive scene reloads — read that doc for why it works
  as a separate object instead of needing this same revisit for `CampaignManager`.
- **A missing/unresolved catalog id doesn't throw** — `GameCatalog.Find*` returns `null`, and
  `CampaignManager` falls back to `G`'s existing default for that slot with a logged warning, rather
  than crashing on stale/bad save data. This is deliberately different from the project's usual
  "let missing mandatory references throw" rule (rule 5) — that rule is about catching Editor-wiring
  bugs, not about tolerating real user save data that can legitimately reference removed content after
  a future update.
- **`RunState`'s field-initializer ids only resolve once the Editor setup is done** — they're plain
  string literals (`"archer"`, `"firemagic"`, etc., picked to match today's default assets
  id-for-id), so until `GameCatalog` actually has entries with matching `id`s assigned (Editor
  checklist), every fresh/no-save run logs fallback warnings and uses `G`'s original Inspector-wired
  defaults instead — harmless, just noisy, and self-resolves once the catalog is populated.
  `CampaignDebugTool` doesn't need `GameCatalog` at all (its fields are direct SO references), only
  `CampaignManager` does, for resolving real saved/default `RunState` ids at battle start.

## Related docs

- `docs/G.md` — `G`'s full accessor list and the static `ApplyCampaignLoadout` this system calls.
- `docs/GameLoop.md` — `Global/GameManager` GameObject convention `CampaignManager` follows; the
  `OnBattleRestart` event this system's fields interact with (`EnergyController.ResetForRestart`,
  `Hero.InitHealth`).
- `docs/Energy.md` — `EnergyController`'s reroll cost system; `CurrentEnergy` itself is now fully
  owned by this system's `RunState.currentEnergy`, not a local baseline.
- `docs/SlotMachine.md` — `SlotMachine.GetActionOptions()`, the sole consumer of `G.DefaultCreatures`/
  `DefaultNukes`/`DefaultSpells` that this system's loadout override ultimately affects.
- `docs/Encounters.md` — the actual battle sequence built on top of this data layer:
  `EncounterListSO`/`BattleSO`, `CampaignProgressManager`'s navigation API, and per-encounter enemy
  avatar/HP substitution. Consumes `currentEncounterIndex` and is what makes it not "abstract"
  anymore.
