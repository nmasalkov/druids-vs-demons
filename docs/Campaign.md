# Campaign / Meta Progression

## What this system does

Step 1 of turning the single self-contained `BattleScene` battle into a campaign: a series of battles
with a pre-battle loadout phase and a post-battle reward phase around each one, plus
player-improvable stats (max HP, reroll energy capacity) that persist across the run. This system is
the **data layer** only — a `RunState` that saves/loads as JSON via a swappable storage backend
(`PlayerPrefs` by default — see "Save system" below), and the wiring that
makes `BattleScene` pull its starting values from it instead of pure hardcoded defaults. It does not
implement the pre-battle/post-battle UI or reward selection — those are still follow-ups. Per-
encounter enemy avatar/HP variation *is* now solved, one layer up — see `docs/Encounters.md`.

**Both sides still roll from one shared creature/nuke/spell pool.** `SlotMachine.GetActionOptions()`
has no player/enemy distinction — overriding `G`'s creature/nuke/spell defaults from campaign data
keeps that behavior for both sides identically. Only the player's hero max HP is driven by
`RunState` directly (`Hero == G.PlayerHero` check in `Hero.GetMaxHealth()`); the enemy's max HP is
driven by the current `FightSO` when one is active (`docs/Encounters.md`), falling back to its own
`HeroSO.health` otherwise. True per-encounter *creature* composition (different summonable creatures
per battle, not just a different enemy avatar/HP) still needs `SlotMachine` to gain a per-side
loadout source — not solved here.

## Two managers, split by concern

Two cooperating singletons, both cross-scene-persistent (`DontDestroyOnLoad` + duplicate-guard),
both placed as components on `_Prefabs/Campaign/CampaignProgress.prefab` (instanced as a root
GameObject in both `BattleScene.unity` and `MapScene.unity`, so either can be the session's
first-loaded scene):

- **`CampaignStateManager`** — sole owner of `RunState` (loaded/created/saved here) and the
  catalogs that resolve its ids back into assets (`GameCatalog`, `RewardListSO`). Also applies that
  data to whatever scene needs it: resolves a run's loadout ids into `G`'s creature/nuke/spell pool
  and pushes reroll energy/enemy avatar into `BattleScene`. This is the "data + apply" half.
- **`CampaignManager`** — encounter navigation: knows which encounter is current and how to move
  between them (advance/restart/defeat/victory/complete). Reads and mutates `RunState` through
  `CampaignStateManager.Instance.CurrentRun` rather than owning any of it itself; owns
  `EncounterListSO`. This is the "where are you in the campaign" half. See `docs/Encounters.md` for
  its full navigation API.

Splitting these two concerns into separate classes (rather than one doing both) keeps "what the run's
data is" and "where the player currently is in the campaign" independently testable/overridable —
e.g. `CampaignDebugTool` only ever needs to reach into `CampaignStateManager`, never `CampaignManager`,
to replace a whole `RunState`.

## Key files

- `Global/Campaign/RunState.cs` — the save data: a plain `[Serializable]` C# class (not a
  `ScriptableObject` — see Gotchas), not saved directly, held by `CampaignStateManager`.
- `Global/Campaign/Save/ISaveStorage.cs`/`PlayerPrefsSaveStorage.cs`/`SaveStorage.cs` — the storage
  abstraction `RunState` is persisted through. See "Save system" below.
- `Global/Campaign/GameCatalog.cs` — SO listing every `CreatureSO`/`NukeSO`/`SpellSO` asset in the
  game; resolves `RunState`'s loadout id strings back to actual assets at battle start.
- `Global/Campaign/CampaignStateManager.cs` (+ `CampaignStateManager.Debug.cs`) — cross-scene
  persistent singleton; `CurrentRun` is the actual owned `RunState`, loaded once in `Awake()`.
  Applies it to `G` and `EnergyController` whenever `BattleScene` is entered (boot-time, or via a
  `SceneManager.sceneLoaded` subscription for every later load — see "Load → resolve → apply flow"
  below). `Catalog` (public `GameCatalog` accessor, mirrors `RewardList`) lets an encounter backend
  outside this file (e.g. `LoadoutPickEncounter`, see `docs/Encounters.md`) resolve loadout ids
  without a second, independently-wired `GameCatalog` reference (rule 22).
- `Global/Campaign/CampaignManager.cs` (+ `CampaignManager.Debug.cs`) — cross-scene persistent
  singleton; encounter navigation only, reads/mutates `RunState` through
  `CampaignStateManager.Instance.CurrentRun`. See `docs/Encounters.md`.
- `Global/Campaign/CampaignDebugTool.cs` + `Global/Campaign/Editor/CampaignDebugToolEditor.cs` —
  Editor-only override tool, mirrors `Global/Balance/BalanceTool.cs`'s pattern. Check a box, drag in
  a creature/nuke/spell asset (or set a number), press Play. Cross-scene-persistent
  (`DontDestroyOnLoad` + duplicate-guard, like `CampaignStateManager`/`CampaignManager` — see
  `docs/Encounters.md`), reads/writes `CampaignStateManager.Instance.CurrentRun` directly, no
  `CampaignManager` dependency.
- `ScriptableObjects/ActionSO.cs` — gained a `public string id` field: the stable identifier
  `GameCatalog` looks assets up by (separate from `actionName`, which is just a display string).
- `ScriptableObjects/CreaturesSO.cs`/`NukesSO.cs`/`SpellsSO.cs` (renamed from `Default*SO` — the
  asset instances on disk keep their original `Default*.asset` file names since those specific
  instances genuinely are the fallback defaults) — reused both as `G`'s hardcoded fallback and as the
  shape `CampaignStateManager` builds a runtime instance into.
- `Global/G.cs` — gained the static `ApplyCampaignLoadout(CreaturesSO, NukesSO, SpellsSO)`, called
  by `CampaignStateManager` whenever `BattleScene` is entered with a run active. Full detail:
  [`docs/G.md`](G.md).
- `Global/GameManager/EnergyController.cs` — gained `ApplyCampaignEnergy()`, called from
  `CampaignStateManager.ApplyEncounterToScene()` (initial load and every encounter transition). No
  local baseline — `CurrentEnergy` is a read-through onto `RunState.currentEnergy`, its one source of
  truth; see `docs/Energy.md` and Gotchas.
- `Units/Hero.cs` — `GetMaxHealth()` reads `CampaignStateManager.Instance.CurrentMaxHp` for the
  player's `Hero` only (`RunState.maxHp` plus claimed `HpBoostRewardSO` bonuses — see
  `docs/Rewards.md`).
- `Global/Campaign/CampaignProfileSO.cs` — Editor-authorable snapshot of a whole `RunState` (SO
  references instead of ids), pluggable into `CampaignDebugTool`'s "Use Debug Profile" slot. See
  below and CLAUDE.md rule 21.
- `Global/Campaign/Rewards/*.cs`, `Global/Campaign/RewardListSO.cs`, `Global/Campaign/
  RewardBonuses.cs` — the reward-card pick system built on top of this data layer. See
  `docs/Rewards.md`.
- `Global/Campaign/RunStateMonitor.cs` — debug-only, lives as a child GameObject under
  `_Prefabs/Campaign/CampaignProgress.prefab`. While enabled, re-serializes the *entire* live
  `RunState` (via `JsonUtility.ToJson(..., true)`) into an Inspector-visible string every frame —
  no hand-picked field list to keep in sync as `RunState` grows, no resolve button. Disabled by
  default (a disabled `MonoBehaviour` never gets `Update()` called, so there's no per-frame cost
  until a developer opts in).

## `RunState` shape

```csharp
public const int CurrentSaveVersion = 1;
public int saveVersion = CurrentSaveVersion; // see "Save system" below

public int maxHp = 100;               // matches HeroSO.health's existing default
public int energyCapacity = 50;       // not currently enforced anywhere — reserved for a future
                                       // "capacity boost" reward; reward grants are purely additive
public int currentEnergy = 50;        // changed by reroll spend and FightSO.rewardAmount/
                                       // BonusEnergyRewardSO claims, uncapped — see docs/Encounters.md, docs/Rewards.md

public string archerId = "archer", tankId = "tank", mageId = "mage";
public string nukeAId = "firemagic", nukeBId = "starfall", nukeCId = "shock";
public string spellAId = "battlecry", spellBId = "charm", spellCId = "shield";

public int currentEncounterIndex = 0; // now consumed — see docs/Encounters.md

// Reward-pick tracking — see docs/Rewards.md. The first list-shaped RunState fields; ids
// resolved against RewardListSO.Find(), same pattern as the loadout ids above.
public List<string> statusRewardIds = new List<string>();
public List<string> boostRewardIds = new List<string>();

// Gathered/unlocked pools LoadoutPickEncounter reads availability from — see docs/Encounters.md.
// Seeded with each loadout's starting ids (matching archerId/tankId/mageId/nukeAId../spellAId..
// above) so a fresh run's own starting loadout is never locked out of its own picker.
public List<string> gatheredCreatureIds = new List<string> { "archer", "tank", "mage" };
public List<string> gatheredNukeIds = new List<string> { "firemagic", "starfall", "shock" };
public List<string> gatheredSpellIds = new List<string> { "battlecry", "charm", "shield" };
```

The 9 loadout fields are ids, not direct SO references — `ScriptableObject` references don't survive
a `JsonUtility` round-trip through storage. Every default above mirrors today's hardcoded
game exactly (`HeroSO.health`, `EnergyController.startingEnergy`, and — id-for-id — whatever
`_DefaultCreatures.asset`/`DefaultNukes.asset`/`DefaultSpells.asset` already point at), so a
brand-new run (nothing saved yet) behaves identically to the game as it exists without this system,
until something actually changes a field. Note `Fireball.asset` exists in the catalog (id
`"fireball"`) but isn't any run's default — `DefaultNukes.asset` actually points at FireMagic/
Starfall/Shock, not Fireball, despite the filename suggesting otherwise; it only becomes reachable
once something adds `"fireball"` to `gatheredNukeIds` (a future reward, or `LoadoutPickEncounter`'s
`unlockAll` debug bypass).

`gatheredNukeIds`/`gatheredSpellIds` are brand new — no existing save has these keys, so
`JsonUtility` leaves the field-initializer value in place and every existing save retroactively
backfills to this seed. `gatheredCreatureIds`'s reseed does **not** get that same retroactive
backfill for a save made any time after the reward system shipped — that field already exists and
is already explicitly serialized as `[]` in such a save (`JsonUtility` only preserves the
field-initializer value for a key *absent* from the JSON, not one present-but-empty). Clear the
local save via `CampaignDebugTool`'s "Clear Saved Run" once if testing this against an existing
save.

## Load → resolve → apply flow

1. `CampaignStateManager.Awake()`: `CurrentRun = LoadOrCreateRunState()` — see "Save system" below
   for what that actually does now (storage abstraction + version validation). Self-contained — only
   reads via `SaveStorage.Backend`, no other script. `CampaignStateManager` is the sole owner and
   loader of `RunState`; `CampaignManager` never touches storage itself, only reads/mutates
   `CampaignStateManager.Instance.CurrentRun`'s fields.
2. Whenever `BattleScene` is actually entered — either at boot (`CampaignStateManager.Start()`
   checking `SceneManager.GetActiveScene().name == SceneNames.BattleScene` directly, relying on its
   early Script Execution Order — see Gotchas) or on every later load (a `SceneManager.sceneLoaded`
   subscription set up in that same `Start()`, since `CampaignStateManager` is now a persistent
   singleton whose own `Start()` only ever runs once per session) — a private `EnterBattleScene()`
   applies the resolved loadout and current fight's data directly; every real navigation path only
   ever loads `BattleScene` once `MapManager` has already resolved any loadout-pick phase, so the
   current encounter is always the fight being played — see `docs/Encounters.md`'s "MapScene" section.
   - `ApplyLoadoutToG()` builds one runtime `CreaturesSO`/`NukesSO`/`SpellsSO` via
     `ScriptableObject.CreateInstance<T>()`, resolving each of the 9 ids through `GameCatalog`
     (`FindArcher`/`FindTank`/`FindMage`/`FindNuke`/`FindSpell`). Any id that doesn't resolve
     (empty/unknown) falls back to whatever `G`'s own Inspector-wired default asset already has for
     that slot, logging a warning. Result is handed to the static `G.ApplyCampaignLoadout(...)`.
   - `ApplyEncounterToScene()` calls `EnergyController.Instance.ApplyCampaignEnergy()`,
     which fires `OnEnergyChanged` so `EnergyDisplay` picks up the value (belt-and-suspenders —
     the `sceneLoaded` timing already guarantees this runs before any `Start()` in that scene,
     including `EnergyDisplay.Start()`, but the event fire also makes this correct if that ordering
     ever changes). The same method is reused for encounter transitions that don't reload the scene
     — see `docs/Encounters.md`.
3. `Hero.GetMaxHealth()` doesn't get pushed a value — it reads
   `CampaignStateManager.Instance.CurrentMaxHp` directly (lazily) whenever called, which happens to
   be from the player `Hero`'s own `Start()` (via `InitHealth()`). Safe purely from the
   `sceneLoaded`-before-`Start()` guarantee (or, at boot, the Awake-before-Start guarantee plus SEO):
   `CampaignStateManager`'s loadout/energy application has already run by the time any `Start()` runs
   in `BattleScene`.
4. `CampaignStateManager.Save()` — `SaveStorage.Backend.Write(JsonUtility.ToJson(CurrentRun))`.
   Lives on `CampaignStateManager` (not `CampaignManager`) since `RunState`'s owner needs to be
   callable from `MapScene` too, where reward/loadout pick screens run — see `docs/Encounters.md`.
   Called automatically at well-defined points, never per-frame/per-action — see "Save system" below.

## Save system

Single-slot automatic save: created on first launch, updated at well-defined points, never via
manual Save/Load buttons.

- **Storage abstraction** (`Global/Campaign/Save/`): `ISaveStorage` (`Exists()`/`Read()`/
  `Write(string)`/`Delete()`, single-slot — no key/slot parameter) is the only thing `RunState`/
  `CampaignStateManager` know about; neither touches `PlayerPrefs` directly anymore.
  `PlayerPrefsSaveStorage` is the default implementation (same `"DvD_RunState"` key
  `CampaignStateManager` used to write directly, so existing dev saves stay valid).
  `SaveStorage.Backend` is a static, settable property defaulting to `new PlayerPrefsSaveStorage()`
  — a future platform build (CrazyGames/Poki/...) swaps it
  (`SaveStorage.Backend = new CrazyGamesSaveStorage();`) before `CampaignStateManager.Awake()` runs,
  without touching `RunState`'s shape or any calling code. Being plain C# (no Unity lifecycle), it's
  also safely usable from Editor code — `CampaignDebugToolEditor`'s "Saved Run" section reads
  through it too.
- **Versioning**: `RunState.CurrentSaveVersion` is the single source of truth for "what version is
  current." `CampaignStateManager.LoadOrCreateRunState()` → private `ParseRunState(json)`
  try/catches `JsonUtility.FromJson<RunState>` and rejects (returns `null`, logs a warning) anything
  that fails to parse or whose `saveVersion` doesn't match `CurrentSaveVersion`. A rejected/missing
  save is replaced with a fresh `RunState`, immediately written back — this is deliberately the same
  code path whether there was never a save or the save is corrupt/an unsupported version; full save
  migration is out of scope, an unsupported version is just treated as "no save." Nothing can crash
  the game over a bad save.
- **Save trigger points** — exactly these three, never per-frame/per-action:
  - *New run created*: `LoadOrCreateRunState()`'s fresh-write path (first-ever boot, or a
    corrupt/invalid save being replaced) and `CampaignManager.StartNewRun()`'s
    `CampaignStateManager.Instance.ReplaceRunState(new RunState())` + `Save()`.
    `CampaignStateManager` is the sole owner and loader of `RunState` — there's exactly one load
    call, not two.
  - *Entering a new encounter*: `CampaignManager.AdvanceToNextEncounter()` saves (via
    `CampaignStateManager.Instance.Save()`) right after bumping the index, before the new encounter
    loads — every forward path (victory, `BattleRewardPresenter` completing a reward pick,
    `StartNewRun()`) funnels through it. See `docs/Encounters.md`.
  - *End of any encounter*: a `FightSO.hasReward` claim is the one place that calls `Save()`
    immediately, right after mutating `currentEnergy` — a real "end of encounter" persistence point
    on top of (not instead of) the entering-next-encounter save that immediately follows. Battle
    encounters don't get an explicit save at victory itself — but `EnergyController.TrySpendReroll()`
    writes every reroll spend straight into `CurrentRun.currentEnergy` (in-memory only, never calling
    `Save()` per-spend — that would violate "never per-frame/per-action"), so on a **victory**
    (which carries the spend forward — see `docs/Energy.md`'s Restart interaction) whatever was
    actually spent is already sitting in `RunState` by the time the next real trigger point
    (entering the next encounter) writes it to storage. A **defeat retry** instead reverts the
    spend back to what the encounter started with before any save happens, so nothing about the
    abandoned attempt's spending ever reaches storage at all — consistent with "restarting must not
    duplicate rewards or advance campaign progress."
- **Debug tooling — "Use External Save"**: see the `CampaignDebugTool` section below.

## `CampaignDebugTool`

Editor-only, lives on a `CampaignDebugTool` GameObject next to `BalanceTool` in `BattleScene.unity` —
and an identical GameObject placed in `MapScene.unity` too, since the tool is cross-scene-
persistent (`DontDestroyOnLoad` + duplicate-guard, same pattern as `CampaignStateManager`/
`CampaignManager` — see `docs/Encounters.md`): whichever scene loads first is the copy whose
overrides actually apply for the session, the other's copy self-destructs in `Awake()`. One
`override<Field>` bool + matching value field per `RunState` entry. The 9 loadout fields are
**direct SO reference fields** (`ArcherSO archer`, `TankSO tank`, `MageSO mage`, `NukeSO nukeA/B/C`,
`SpellSO spellA/B/C`) — drag an asset into the Inspector like any other object field — not id
strings or a dropdown; the tool converts the picked asset's `.id` into the matching `RunState` id
string in `Awake()`. `RunState` itself still stores plain id strings (see above) — only this debug
tool's own authoring surface uses direct references, for convenience.

- Applies in **`Awake()`**, mutating `CampaignStateManager.Instance.CurrentRun` directly for every
  toggled-on override — before anything reads it out (`CampaignStateManager`'s own `Start()`/
  `sceneLoaded` handler building the runtime loadout SOs in `BattleScene`, `Hero.Start()` reading
  `maxHp`, `MapManager.Start()` resolving the current encounter in `MapScene`). This needs
  `CampaignStateManager.Awake()` to run first, which is the one place in this system that relies on
  Unity's **Script Execution Order** project setting (`CampaignStateManager` at `-100`, before
  `CampaignDebugTool` at `-50`) rather than the Awake-before-Start guarantee alone — see Gotchas. No
  dependency on `CampaignManager.Awake()` at all, so this works identically whether the session boots
  into `BattleScene` or `MapScene`.
- Never calls `Save()` — overrides are in-memory-only for the current Play session, so testing never
  overwrites a real saved run.
- No mid-session "reapply" button. To try a different combination, stop and re-enter Play mode.
- Its custom Editor also draws a **"Saved Run"** section, independent of the override fields above:
  Unity has no built-in PlayerPrefs browser, so this is the debug affordance for it. Reads
  `SaveStorage.Backend.Exists()`/`Read()` and pretty-prints the JSON via
  `JsonUtility.ToJson(JsonUtility.FromJson<RunState>(raw), true)`; shows "no saved run yet" if
  absent (the common case — see the `Save()` bullet above). Works in Edit mode too, not just Play
  mode. A "Clear Saved Run" button calls `SaveStorage.Backend.Delete()` directly — separate from,
  and not gated by, the override toggles.

### "Use External Save" — paste a whole `RunState` JSON blob

The coarsest override, highest precedence (above "Use Debug Profile" and every granular field):
`CampaignDebugTool.useExternalSave` + `externalSaveJson` (a multiline text field). When checked,
`Awake()` parses the pasted text via `CampaignStateManager.ParseExternalRunState(json)` — the exact
same private `ParseRunState` real saves go through (see "Save system" above), so a deliberately
corrupt/wrong-version paste exercises the real rejection path instead of a separate debug-only
parser. A valid result replaces `RunState` wholesale via
`CampaignStateManager.Instance.ReplaceRunState(run)` (the actual owner of `RunState`) and relocates
`CampaignManager`'s index the same way "Use Debug Profile" does (see below). Like every other
override here, it never calls `Save()` — the pasted save only exists in memory for that Play session.

The Editor also has a **"Generate Save JSON from Profile"** button (next to "Use Debug Profile")
that converts the currently assigned `CampaignProfileSO` into save JSON
(`JsonUtility.ToJson(CampaignDebugTool.BuildRunStateFromProfile(profile), true)`) and writes it
straight into `externalSaveJson`, so round-trip testing a profile through the real save-parsing
path doesn't require hand-writing JSON. `BuildRunStateFromProfile` is the same field-mapping "Use
Debug Profile" itself uses (see below) — extracted into a reusable `public static` method for
exactly this.

### "Use Debug Profile" — `CampaignProfileSO`

A coarser alternative to the per-field overrides above: `CampaignDebugTool.useDebugProfile` +
`debugProfile` (a `CampaignProfileSO` asset, `Game/Campaign/Campaign Profile` in the Create menu).
When checked, `Awake()` applies **every** `RunState` field from the profile wholesale and skips the
granular override checks entirely — no mixing the two. Exists so a whole test scenario (loadout +
stats + which encounter) can be saved as one reusable asset instead of re-checking a dozen boxes
every session — drag in `CampaignProfileSO` assets for different scenarios ("mid-run, low energy,"
"final boss, maxed loadout," etc.) and swap between them.

`CampaignProfileSO`'s shape mirrors `RunState` field-for-field, but with direct SO references
(`ArcherSO archer`, `NukeSO nukeA`, etc.) instead of id strings — same convenience tradeoff the
per-field overrides already make, and for the same reason (id strings exist purely so `RunState`
survives a `JsonUtility` round-trip through `SaveStorage`; a debug-only asset has no such
constraint). `CampaignDebugTool.BuildRunStateFromProfile(profile)` converts it into a whole
`RunState` (each reference → its `.id`), which then goes through the same
`ApplyRunStateOverride(run, sourceLabel)` helper "Use External Save" uses — replacing `RunState`
wholesale via `CampaignStateManager.Instance.ReplaceRunState(run)` rather than mutating the existing
object's fields in place (behaviorally equivalent: every consumer reads
`CampaignStateManager.Instance.CurrentRun.<field>` fresh, nothing caches the reference). Profile
fields are treated as mandatory once `useDebugProfile` is checked (rule 5) — an unassigned
archer/tank/mage/nuke/spell throws immediately rather than silently resolving to `null`.

**`currentEncounterIndex` no longer needs a separate relocation step.** `ApplyRunStateOverride` still
calls `CampaignManager.Instance.SetSessionEncounterIndexOverride(run.currentEncounterIndex)`
after `ReplaceRunState(run)`, but it's now a harmless no-op-equivalent (clamps against
`CampaignManager`'s own `EncounterList`, then hands the index to
`CampaignStateManager.ApplySessionEncounterIndexOverride` — which just reassigns the value
`ReplaceRunState` already set). `CampaignStateManager.CurrentRun` *is* the `RunState` every consumer
reads through to, not a separate bootstrap copy, so replacing it wholesale already relocates the
index. `SetSessionEncounterIndexOverride` remains real (non-redundant) for
`CampaignProgressTool`'s narrower case of overriding just the index within the existing `RunState`
— see `docs/Encounters.md`'s "Debug-only surface" section.

**Reminder (CLAUDE.md rule 21): every new persisted `RunState` field needs a matching field on
`CampaignProfileSO` too, plus a granular override pair on `CampaignDebugTool` — nothing enforces
this at compile time, so it's easy to add a `RunState` field and forget the other two.** This
applies equally to list-shaped fields (see `docs/Rewards.md`'s `statusRewardIds`/`boostRewardIds`/
`gatheredCreatureIds` — the first precedent for this, now joined by `gatheredNukeIds`/
`gatheredSpellIds` above, added the same way): `CampaignProfileSO` gets a matching
`List<T>` of direct SO refs, and `CampaignDebugTool`'s override pair is a toggle + `List<T>` drawn
via `SerializedProperty` (`DrawToggleAndList` in `CampaignDebugToolEditor`) since the existing
`ref`-based scalar helpers don't fit a list.

### Exporting a run to a profile — the reverse direction

`CampaignDebugToolEditor`'s **"Export to Debug Profile"** button (in the "Saved Run" section)
captures a whole `RunState` as a brand new `CampaignProfileSO` asset — the reverse of "Use Debug
Profile"/"Generate Save JSON from Profile" above, for the opposite workflow: you're mid-testing a
specific scenario (a specific encounter, loadout, energy level) reached by actually playing, and
want to snapshot it once so you can jump straight back into it later instead of re-creating it by
hand or replaying up to that point again.

- **Source**: prefers the live `CurrentRun` while in Play mode (`CampaignStateManager.Instance`
  non-null) — the exact state currently being tested, including any in-memory-only changes (reroll
  spend, granular/profile/external-save overrides) not yet written to storage. Outside Play mode
  (or if no campaign singleton is alive), falls back to whatever's actually on disk via
  `SaveStorage.Backend.Read()` — the same JSON the "Saved Run" section above it displays. Either
  way this reads real state; it never depends on any of `CampaignDebugTool`'s own override fields.
- **Id resolution**: `CampaignDebugTool.BuildProfileFromRunState(run, catalog, rewardList)` is the
  mirror of `BuildRunStateFromProfile` — same field list, opposite direction, resolving each
  `RunState` id string back to a direct SO reference via `GameCatalog`/`RewardListSO`. In Play mode
  those come from `CampaignStateManager.Instance.Catalog`/`RewardList` (guaranteed present, rule 5);
  in Edit mode the Editor locates the project's `GameCatalog`/`RewardListSO` assets via
  `AssetDatabase.FindAssets` instead (there's no live singleton to ask). Unlike
  `BuildRunStateFromProfile` (which treats every profile field as mandatory and throws if unset —
  it's authored Editor content), this direction runs against real save data that can legitimately
  reference removed/renamed content, so an id that fails to resolve is left `null` with a logged
  warning rather than throwing — the same "don't crash on stale save data" posture
  `GameCatalog.Find*`'s own callers already take (see Gotchas below).
- **Destination**: `EditorUtility.SaveFilePanelInProject` defaults to
  `Assets/Game/_ScriptableObjects/Campaign/Profiles` (created if missing) with a default filename
  of `RunState_Encounter<N+1>`; picking a location writes the asset via `AssetDatabase.CreateAsset`
  and selects/pings it. Cancelling the dialog discards the in-memory profile instance instead of
  leaving an orphaned unsaved object around.
- The resulting asset is a completely ordinary `CampaignProfileSO` — drag it into `debugProfile`
  and check "Use Debug Profile" like any other, or hand-edit its fields afterward the same as a
  profile built from scratch.

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
  `CampaignStateManager.Instance.CurrentRun` directly *before* anything reads it back out
  (`CampaignStateManager`'s own boot-time `EnterBattleScene()` in `BattleScene`, `Hero.Start()`'s
  `maxHp` read, `MapManager.Start()` in `MapScene`) — this depends on `CampaignStateManager.Awake()`
  (`-100`, loads `RunState`) having already run, which `CampaignDebugTool`'s own `-50` Script
  Execution Order entry guarantees. `CampaignDebugTool` has no dependency on `CampaignManager.Awake()`
  at all, which is exactly what makes it work identically whether the session boots into
  `BattleScene` or `MapScene`. `Hero` needs no special ordering of its own: it reads
  `CampaignStateManager.Instance.CurrentMaxHp` lazily, straight from its own `Start()`, which is safe
  purely from the Awake-before-Start guarantee. Don't add a third script that also needs to mutate
  `RunState` pre-`Start()` without reconsidering this — it doesn't scale past the current
  execution-order chain.
- **`EnergyController.CurrentRerollCost` (not `CurrentEnergy`) is the one thing that must still be
  set in `Awake()`, not `Start()` — this was a real bug, caught live in testing, not just reasoned
  about.** An earlier version of this system moved both `CurrentEnergy`/`CurrentRerollCost`
  initialization from `Awake()` into `Start()`, reasoning (wrongly) that it needed to run after
  the loadout applier. That broke `SlotColumn.Start()`, which reads
  `EnergyController.Instance.CurrentRerollCost` synchronously to seed the reroll-cost label —
  Start()-vs-Start() order between unrelated components is unspecified, so the label intermittently
  showed `0` instead of the real cost depending on which `Start()` ran first. `CurrentRerollCost`
  stays self-contained in `Awake()` for exactly this reason (`baseRerollCost`, no `CampaignStateManager`
  dependency). `CurrentEnergy` is different — it's a read-through property onto
  `CampaignStateManager.Instance.CurrentRun.currentEnergy` (`docs/Energy.md`), so unlike
  `CurrentRerollCost` it needs no init of its own at all: it's already correct the instant
  `CampaignStateManager.Awake()` creates `CurrentRun`, before any `Start()` runs anywhere. What
  `CampaignStateManager.ApplyEncounterToScene()` calling `ApplyCampaignEnergy()` actually does is take
  the `_encounterStartEnergy` restart-revert snapshot and fire `OnEnergyChanged` for the UI —
  see `docs/Energy.md`'s Restart interaction — not make `CurrentEnergy` itself valid.
- **`CampaignStateManager`'s early Script Execution Order (`-100`) is why applying directly from its
  own `Start()`/`sceneLoaded` handler is safe**, not despite it. Because `-100` is earlier than
  every default-order script, and `sceneLoaded` itself is guaranteed to fire after every GameObject's
  `Awake()` but before any `Start()` in the newly loaded scene, `CampaignStateManager`'s loadout/
  energy application always runs before `EnergyController.Start()`/`EnergyDisplay.Start()`/etc. For
  `CurrentRerollCost` this no longer matters for correctness (it's set standalone in `Awake()`), only
  for `EnergyDisplay` showing the right value on the very first frame instead of one frame later via
  the `OnEnergyChanged` catch-up — but for `CurrentEnergy` this ordering is load-bearing: nothing
  else ever sets it, so if the loadout/energy application ran *after* some consumer's `Start()`, that
  consumer would read a stale `0` with no catch-up until the next `OnEnergyChanged` fire.
- **`RunState` is plain data, not a `ScriptableObject`, on purpose.** SOs are Editor-time assets;
  mutating one's fields at runtime doesn't persist in a build and risks polluting shared asset state.
  `CreaturesSO`/`NukesSO`/`SpellsSO` stay SOs because they're `G`'s existing shape (and the
  `ScriptableObject.CreateInstance` runtime instances `CampaignStateManager` builds from them are
  disposable, never saved as assets) — don't confuse "SO used at runtime" with "data that needs to
  survive a session," which is what `RunState`/the save system is for.
- **Both `CampaignStateManager` and `CampaignManager` are `DontDestroyOnLoad`, on the same
  `CampaignProgress` prefab instance.** Unlike most other battle-scoped singletons (`G`/`GameManager`/
  `EnergyController`/`RollStateManager`, see `docs/GameLoop.md`), which live on the per-scene
  `Global/GameManager` GameObject and are recreated fresh on every `BattleScene` load, these two need
  to survive scene reloads: `CampaignManager` owns navigation state that must persist across a
  `BattleScene ↔ MapScene` transition, and `CampaignStateManager` owns `RunState` itself, which a
  `MapScene`-hosted reward/loadout pick screen needs to read/write too. `CampaignStateManager`
  compensates for no longer getting a fresh per-scene-load `Start()` (see "Load → resolve → apply
  flow" above) by subscribing to `SceneManager.sceneLoaded` instead.
- **A missing/unresolved catalog id doesn't throw** — `GameCatalog.Find*` returns `null`, and
  `CampaignStateManager` falls back to `G`'s existing default for that slot with a logged warning,
  rather than crashing on stale/bad save data. This is deliberately different from the project's
  usual "let missing mandatory references throw" rule (rule 5) — that rule is about catching
  Editor-wiring bugs, not about tolerating real user save data that can legitimately reference removed
  content after a future update.
- **`RunState`'s field-initializer ids only resolve once the Editor setup is done** — they're plain
  string literals (`"archer"`, `"firemagic"`, etc., picked to match today's default assets
  id-for-id), so until `GameCatalog` actually has entries with matching `id`s assigned (Editor
  checklist), every fresh/no-save run logs fallback warnings and uses `G`'s original Inspector-wired
  defaults instead — harmless, just noisy, and self-resolves once the catalog is populated.
  `CampaignDebugTool` doesn't need `GameCatalog` at all (its fields are direct SO references), only
  `CampaignStateManager` does, for resolving real saved/default `RunState` ids at battle start.

## Related docs

- `docs/G.md` — `G`'s full accessor list and the static `ApplyCampaignLoadout` this system calls.
- `docs/GameLoop.md` — `Global/GameManager` GameObject convention most other battle-scoped
  singletons follow (unlike `CampaignStateManager`/`CampaignManager` — see Gotchas); the
  `OnBattleRestart` event this system's fields interact with (`EnergyController.ResetForRestart`,
  `Hero.InitHealth`).
- `docs/Energy.md` — `EnergyController`'s reroll cost system; `CurrentEnergy` itself is now fully
  owned by this system's `RunState.currentEnergy`, not a local baseline.
- `docs/SlotMachine.md` — `SlotMachine.GetActionOptions()`, the sole consumer of `G.DefaultCreatures`/
  `DefaultNukes`/`DefaultSpells` that this system's loadout override ultimately affects.
- `docs/Encounters.md` — the actual fight sequence built on top of this data layer:
  `EncounterListSO`/`FightSO`, `CampaignManager` (encounter navigation — see "Load → resolve → apply
  flow" above for how it cooperates with `CampaignStateManager`) and its navigation API, the
  `Encounter`/`MapManager`/`BattleRewardPresenter` pick-screen dispatch, and per-encounter enemy
  avatar/HP substitution.
- `docs/Rewards.md` — the reward-card pick system built on top of `RunState`/`FightSO.hasReward`/
  `RewardEncounter`: `RewardSO` hierarchy, `RewardListSO`, `RewardDrawer`'s draw algorithm, and
  `RewardBonuses`' resolver-side stat-boost hook.
