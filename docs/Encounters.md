# Encounters & Campaign Progress

## What this system does

Builds the actual **fight sequence** on top of `docs/Campaign.md`'s data layer:
`EncounterListSO` is the ordered campaign (`List<FightSO> fights`), and `CampaignManager` is the real
API — not debug-only logic — for starting a run, restarting the current encounter, and advancing to
the next one. `RunState` itself is owned by the sibling `CampaignStateManager` (see "Two cross-scene-
persistent managers" below) — `CampaignManager` reads/mutates it through
`CampaignStateManager.Instance.CurrentRun` rather than owning any of it itself.

**`FightSO` is now the only campaign-list entry type.** It used to share the list with two sibling
marker types, `LoadoutPickSO` (pre-fight loadout picker) and `RewardPickSO` (post-fight energy/card
reward) — both were folded directly into `FightSO` as flags, since neither ever carried data unique
enough to justify being its own list entry: `hasLoadoutPick` (bool) and `hasReward`/`rewardAmount`
(bool + int, replacing `RewardPickSO.energyReward`). A fight's loadout-pick and reward-pick are now
optional **sub-phases wrapped around it** rather than separate list entries — neither phase advances
`RunState.currentEncounterIndex` or moves visible progress on the map; only completing the whole node
(fight + optional reward) does. `EncounterSO` (the old abstract base both marker types and `FightSO`
shared) was removed entirely along with them — with a single concrete type left, the abstraction had
no purpose. `FightSO` carries its own enemy avatar/HP (`fightId` renamed from `battleId` — campaign-
layer naming only, unrelated to the per-turn `BattleState`/`RunBattle()` combat-resolution machinery,
which keeps its own "Battle" naming).

**Sequencing, per node:**
- On `MapScene`, arriving at the current node: if `hasLoadoutPick` is false, the point plays its
  fight-arrival shake directly and `BattleScene` loads. If true, the point plays the plain "you've
  arrived" yoyo/punch instead, the loadout picker shows in place (no index change), and only once
  it's confirmed — after a short pause — does the fight-arrival shake play and `BattleScene` load.
- On victory in `BattleScene`: if `hasReward` is false, the campaign completes/advances immediately.
  If true, the reward picker shows **as an overlay inside `BattleScene` itself** (not `MapScene` —
  right there on the game-over screen, after the round loop has already permanently halted); only
  once it's confirmed does the campaign actually advance and transition to `MapScene`, which then
  plays its normal gradual-progress reveal to the next node.

`GameOverState` drives `CampaignManager`'s API automatically at the end of a fight: victory (after a
delay) either shows the reward pick or advances/completes the campaign; defeat reloads the current
encounter — see "Fight results" below.

## Key files

- `Global/Campaign/FightSO.cs` — the sole `EncounterListSO` entry type (renamed from `BattleSO`,
  `[MovedFrom]`-guarded so the existing asset files kept resolving through the rename). `fightId`
  (string, `[FormerlySerializedAs("battleId")]`), `isTutorial` (bool, still inert — forward-looking),
  `enemyData` (`EnemyData`), `hasLoadoutPick` (bool), `hasReward` (bool), `rewardAmount` (int — the
  guaranteed energy grant, only meaningful when `hasReward` is true), `neutralDirtyTripleIndex`
  (int, default 100 — the starting point the Comeback Adjustment is added to, applied to
  `SlotMachineRigger.neutralDirtyTripleIndex`) + `firstRoundDirtyTripleIndex` (int, default 50 —
  forced Dirty Triple Index for either side's opening turn while `GameManager.IsFirstRound`,
  skipping Comeback Settings entirely, applied to `SlotMachineRigger.firstRoundDirtyTripleIndex`),
  `dirtyTripleStabilization`
  (int, 0 = disabled — subtracted from the acting side's Dirty Triple Index each bonus turn earned
  from a triple; see `docs/SlotMachine.md`), `playerCleanTripleIndex`/`enemyCleanTripleIndex` (int,
  default 100 — per-fight base for `SlotMachineRigger`'s Clean Triple Index, applied once at fight
  start), `playerComebackSettings`/`enemyComebackSettings` (addable `List<ComebackSetting>` each,
  where `ComebackSetting` is `(hpPercent 0-100 slider, opponentAdvantage, tripleAdjustment)` — the
  per-side comeback ladder, matched on HP-at-or-below **or** opponent firepower lead, biggest
  matching `tripleAdjustment` wins and is added to **both** that side's Dirty and Clean Triple Index;
  an empty list disables the mechanism for that side. `GetComebackAdjustment(isPlayer, hpPercent,
  opponentAdvantage)` is the lookup. Replaced the old `HpAdjustmentSettings` blocks and their
  enable toggles), `ludoProgressIndex` (int, 0 = disabled — how much a
  reroll bumps the acting side's Dirty Triple Index during their first roll phase of a turn) +
  `perRoundLudoProgressOverrides` (addable `List<RoundLudoProgressOverride>`, each a `(round, value)`
  pair — `GetLudoProgressIndexForRound(round)` returns the first matching round's value, else falls
  back to `ludoProgressIndex`). See `docs/SlotMachine.md` for all of the above.
- `EnemyData` (nested `[Serializable] struct` in `FightSO.cs`) — `enemyAvatarPrefab` (`GameObject`),
  `hp` (int), `creatures` (`CreaturesSO` — the enemy's per-fight archer/tank/mage roster, applied to
  `G.EnemyCreatures` via `G.ApplyCampaignEnemyCreatures` — see `docs/G.md`), plus the AI-tuning fields
  (`stupidityChance`/`criticalFailureChance`/`rerollsAmount` — see `docs/AI.md`). Expandable later
  without touching `FightSO` itself.
- `Global/Campaign/Encounter.cs` — abstract `MonoBehaviour` base for a pick-screen phase played by
  `MapManager` (loadout) or `BattleRewardPresenter` (reward): `event Action OnCompleted` + protected
  `Complete()`, plus `Headless`/`CompletePresentation()` (CLAUDE.md rule 28 — the backend/view split
  and headless-testability mechanism). No shared `Play(...)` signature anymore — each subclass exposes
  its own concrete `Play(...)` (`LoadoutPickEncounter.Play()` takes nothing, `RewardEncounter.Play(int
  rewardAmount)` takes the granted amount) since there's no longer a common `EncounterSO` data type to
  abstract over. Mirrors `GameState`'s "one runner, self-contained states" shape (`docs/GameLoop.md`).
- `Global/Campaign/RewardEncounter.cs` — **backend only** (rule 28): grants `currentEnergy` and draws
  3 reward cards on `Play(int rewardAmount)`, exposes `SelectReward(RewardSO)`/`Confirm()` plus
  `OnRewardsDrawn`/`OnSelectionChanged`/`OnClaimed` events — no UI reference of any kind, fully
  drivable headlessly (`Headless = true`, no prefab needed). See `docs/Rewards.md`.
- `Global/Campaign/RewardEncounterView.cs` — the paired **view** (rule 28):
  `[RequireComponent(typeof(RewardEncounter))]`, owns `messageText`/`claimButton`/`cardSlots`/
  `cardPrefab`, subscribes to the backend's events in `Awake()`, forwards clicks to
  `SelectReward`/`Confirm`, and calls `CompletePresentation()` once its discard animation finishes.
  See `docs/Rewards.md`.
- `Global/Campaign/LoadoutPickEncounter.cs` — **backend only** (rule 28): owns the pending edit state
  for all 9 loadout slots (`SlotKind`/`SlotRef`-addressed), exposes `SelectSlot`/`SelectCandidate`/
  `Swap()`/`Confirm()` plus `OnLoadoutLoaded`/`OnPoolChanged`/`OnSelectionChanged`/`OnSwapped`/
  `OnConfirmed` events, gated by `gatheredCreatureIds`/`gatheredNukeIds`/`gatheredSpellIds`
  (`docs/Campaign.md`) unless an `unlockAll` debug bypass is set — fully drivable headlessly. See
  `docs/Loadout.md`.
- `Global/Campaign/LoadoutPickEncounterView.cs` — the paired **view** (rule 28):
  `[RequireComponent(typeof(LoadoutPickEncounter))]`, owns the 9 equipped-slot anchors, the Available
  pool container, the Comparison panel, and the Swap/Finish buttons, subscribes to the backend's
  events in `Awake()`. See `docs/Loadout.md`.
- `Global/Campaign/BattleRewardPresenter.cs` — `BattleScene`-local dispatcher (scene-local singleton,
  recreated on every `BattleScene` load, **not** `DontDestroyOnLoad` — same pattern as
  `RollStateManager`) for the post-victory reward pick: instantiates `RewardEncounter.prefab` as an
  overlay inside `BattleScene`, waits for its `OnCompleted`, then calls
  `CampaignManager.Instance.CompleteCurrentEncounter()`. Replaces the old generic `EncounterPlayer` —
  see "Encounter dispatch" below.
- `Global/Campaign/EncounterListSO.cs` — `List<FightSO> fights`, the ordered campaign sequence,
  indexed by `CampaignManager.CurrentEncounterIndex`.
- `Global/Campaign/CampaignManager.cs` — the navigation API (below): which fight is current,
  advance/restart/defeat/victory/complete. Reads/mutates `RunState` through
  `CampaignStateManager.Instance.CurrentRun` (see "Two cross-scene-persistent managers" below) —
  pure navigation logic, owns no data of its own beyond `EncounterListSO`.
- `Global/Campaign/CampaignManager.Debug.cs` — `partial class` split of the same type
  holding the debug-only `SetSessionEncounterOverride`/`SetSessionEncounterIndexOverride` surface
  (CLAUDE.md rule 20).
- `Global/Campaign/CampaignStateManager.cs` (+ `CampaignStateManager.Debug.cs`) — sole owner/loader
  of `RunState` (see "Two cross-scene-persistent managers" below) plus the loadout/energy/enemy-avatar
  application that used to live on a `BattleScene`-only `CampaignManager`.
- `Global/Campaign/Editor/CampaignManagerEditor.cs` — read-only Inspector view of the
  current encounter index/asset (CLAUDE.md rule 19).
- `Global/Campaign/CampaignProgressTool.cs` + `Global/Campaign/Editor/CampaignProgressToolEditor.cs`
  — Editor-only debug tool mirroring `CampaignDebugTool`'s pattern.
- `PlayerView/HeroView.cs` — `ReplaceHeroAvatar(GameObject avatarPrefab)`.
- `Global/Campaign/CampaignStateManager.cs` — `CurrentRun` is the actual owned `RunState`; has
  `CurrentFight` (renamed from `CurrentBattle`) and `ApplyEncounterToScene()` (called whenever
  `BattleScene` is entered, applies both energy and enemy avatar/HP — see `docs/Campaign.md`'s "Load
  → resolve → apply flow").
- `Units/Hero.cs` — `GetMaxHealth()` gained a symmetric enemy-side branch.
- `UI/PauseMenuController.cs` — `HandleRestartClicked()` calls
  `CampaignManager.Instance.ResetCurrentEncounter()` instead of calling
  `GameManager.RestartBattle()` directly.
- `Global/GameManager/GameOverState.cs` — no longer a pure dead end; calls
  `CampaignManager.Instance.ResolveVictory()`/`ResolveDefeat()` after logging. See
  "Fight results" below.
- `Global/Campaign/CampaignProfileSO.cs` — Editor-authorable whole-`RunState` snapshot for
  `CampaignDebugTool`'s "Use Debug Profile" — see `docs/Campaign.md`.

Assets: `_ScriptableObjects/Campaign/Encounters/AllEncounters.asset` (an `EncounterListSO`) +
`Fight1 Tutorial.asset`/`Fight2 Easy Demon.asset`/`Fight3 Shield Breaker.asset`/`Fight4 Elite.asset`/
`Fight5 Final Boss.asset`, referencing enemy avatar prefabs under
`_Prefabs/Characters/Units/Avatars/Enemies/`. `_Prefabs/Campaign/LoadoutEncounter.prefab`/
`RewardEncounter.prefab` are the two `Encounter` prefabs `MapManager`/`BattleRewardPresenter`
instantiate directly (no longer resolved generically through an `EncounterSO.EncounterPrefab` field —
see "Encounter dispatch" below).

## Ownership split: `CampaignStateManager` vs `CampaignManager`

- **`CampaignStateManager`** owns *the run's state* (`RunState` itself — loaded/created once in
  its own `Awake()`, see "Two cross-scene-persistent managers" below) and *applying the run to the
  current scene* (loadout/energy/enemy-avatar application — see `docs/Campaign.md`).
- **`CampaignManager`** owns *where you are in the campaign* (current encounter index/asset, just
  `CampaignStateManager.Instance.CurrentRun.currentEncounterIndex` read live — no separate cached
  field) and the three navigation actions (`StartNewRun`, `ResetCurrentEncounter`,
  `AdvanceToNextEncounter`). It is the only thing that knows how to move forward — both
  `CampaignProgressTool` and real gameplay UI call into it, never duplicate its logic.
- `AdvanceToNextEncounter()`/`StartNewRun()` mutate `CampaignStateManager.Instance.CurrentRun`'s
  `currentEncounterIndex`/replace it wholesale via `CampaignStateManager.Instance.ReplaceRunState()`
  (single source of truth — CLAUDE.md rule 22), call `CampaignStateManager.Instance.Save()`, then
  call the shared `LoadCurrentEncounter()` (see "Soft reload" below) instead of unconditionally
  reloading the scene. `ResetCurrentEncounter()` never touches the index at all — the current
  encounter's enemy avatar/HP are already correct, so it just delegates to the existing in-place
  `GameManager.RestartBattle()` machinery (`OnBattleRestart` → `Hero.InitHealth()`, which now reads
  the encounter-aware `GetMaxHealth()`).

```csharp
// CampaignManager.cs — pure navigation logic, no defensive Instance null-checks (CLAUDE.md
// rule 5: CampaignStateManager/GameManager are guaranteed to co-exist wherever this runs).
public partial class CampaignManager : MonoBehaviour
{
    public static CampaignManager Instance { get; private set; }
    [SerializeField] private EncounterListSO encounterList;

    public EncounterListSO EncounterList => encounterList;

    public FightSO CurrentFight => encounterList.fights[
        Mathf.Clamp(CampaignStateManager.Instance.CurrentRun.currentEncounterIndex, 0, encounterList.fights.Count - 1)];
    public bool HasNextEncounter => CampaignStateManager.Instance.CurrentRun.currentEncounterIndex < encounterList.fights.Count - 1;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartNewRun() { /* CampaignStateManager.Instance.ReplaceRunState(new RunState()), .Save(), LoadCurrentEncounter() */ }
    public void ResetCurrentEncounter() { GameManager.Instance.RestartBattle(); }
    public void AdvanceToNextEncounter() { /* CampaignStateManager.Instance.CurrentRun.currentEncounterIndex++, .Save(), LoadCurrentEncounter() */ }

    private void LoadCurrentEncounter() { /* CampaignStateManager.Instance.ApplyEncounterToScene() + GameManager.Instance.RestartBattle() */ }
}
```

`SetSessionEncounterOverride`, `SetSessionEncounterIndexOverride` live in `CampaignManager.Debug.cs`
instead — see "Debug-only surface" below. `ParseExternalRunState` and the `_overrideWindowOpen` field
live on `CampaignStateManager.Debug.cs`, since they mutate/gate `RunState` directly.

## Two cross-scene-persistent managers

Every other singleton (`G`, `GameManager`, `EnergyController`, `RollStateManager`,
`BattleRewardPresenter`) lives on the per-scene `Global` GameObject and is recreated on every scene
load (`docs/GameLoop.md`). `CampaignStateManager` and `CampaignManager` are the exception: both live
on `_Prefabs/Campaign/CampaignProgress.prefab` (a **root-level** GameObject — `DontDestroyOnLoad` only
works on scene roots, see CLAUDE.md rule 26), instanced in **both** `BattleScene.unity` and
`MapScene.unity` so whichever loads first wins the session regardless of entry point, and both use
the standard Unity duplicate-guard singleton pattern:

```csharp
void Awake()
{
    if (Instance != null) { Destroy(gameObject); return; }
    Instance = this;
    DontDestroyOnLoad(gameObject);
    ...
}
```

On the first scene load their `Awake()`/`Start()` run once and they survive forever after (within
the session). On every later `BattleScene` reload (triggered by `AdvanceToNextEncounter`/
`StartNewRun`), the scene file's own placed copy of `CampaignProgress` is instantiated fresh, sees
`Instance != null` on both components, and self-destructs immediately — the original, persistent
instances keep their in-memory state (`RunState`, including `currentEncounterIndex`) untouched by
the reload.

**Why `CampaignStateManager` owns `RunState` directly, on a persistent object, instead of a
battle-scoped manager owning it**: a battle-scoped manager living on the per-scene `Global`
GameObject is recreated on every `BattleScene` load and isn't even loaded at all while `MapScene` is
active — and `CampaignManager`/reward pick screens need to answer "what's the current encounter"/
"what's in `RunState`" regardless of which scene is active. Since both persistent managers' own
`Start()` only ever runs once per session, they can't rely on querying a battle-scoped manager fresh
at arbitrary later points either. So `RunState` lives on the one object guaranteed to persist and be
queryable regardless of which scene is active: `CampaignStateManager` loads it once via the
self-contained static `LoadOrCreateRunState()` helper, and `CampaignManager` reads/mutates it through
`CampaignStateManager.Instance.CurrentRun` rather than caching anything of its own.

**Since `CampaignStateManager` no longer gets a fresh per-`BattleScene`-load `Start()`** (it lived
`BattleScene`-only before this split, recreated fresh every load — see `docs/Campaign.md`'s "Load →
resolve → apply flow"), it compensates via a `SceneManager.sceneLoaded` subscription (fires after
every GameObject's `Awake()` in the newly loaded scene, but before any of their `Start()`s) plus a
direct boot-time check in its own `Start()` for the case where the session's first-loaded scene is
already `BattleScene`.

## Enemy avatar substitution: `HeroView.ReplaceHeroAvatar`

```csharp
public void ReplaceHeroAvatar(GameObject avatarPrefab)
{
    var oldGO = hero.gameObject;
    var parent = oldGO.transform.parent;
    var localPosition = oldGO.transform.localPosition;
    var localRotation = oldGO.transform.localRotation;
    var localScale = oldGO.transform.localScale;
    Destroy(oldGO);

    var newGO = Instantiate(avatarPrefab, parent);
    newGO.transform.SetLocalPositionAndRotation(localPosition, localRotation);
    newGO.transform.localScale = localScale;

    hero = newGO.GetComponent<Hero>();
    hero.Slot = heroSlot;
    heroSlot.Unit = hero;
}
```

Destroys the current avatar (`EnemyView/AvatarPosition/Player` before this system existed) and
instantiates the replacement as a sibling under the same parent (`EnemyView/AvatarPosition`),
preserving the old avatar's local transform — this keeps the existing enemy-side mirroring
(`localScale.x = -1`, applied at the `EnemyView` root, per `CLAUDE.md` rule 15) intact without any
special-casing. Rewires `hero`/`heroSlot.Unit` the same way `HeroView.Start()` already does.

Called from `CampaignStateManager.ApplyEncounterToScene()` — see below — which runs whenever
`BattleScene` is entered, either from `CampaignStateManager.Start()` directly (boot case, Script
Execution Order `-100`, the earliest `Start()` in the scene) or from its `SceneManager.sceneLoaded`
handler (every later load — fires after every GameObject's `Awake()` in the scene but before any
`Start()`, so ordering is preserved either way). **Ordering is safe without any new staleness
machinery**: `Destroy()` on the old avatar happens before its own `Start()` would otherwise fire
(Unity skips `Start()` for objects destroyed earlier in the same Awake/Start pass), so the old
`Hero` never runs `InitHealth()`/subscribes to `OnBattleRestart`. The newly instantiated `Hero`'s
`Start()` runs later in the same frame, by which point `CampaignStateManager.CurrentFight` is
already set, so its first `GetMaxHealth()` read is correct.

## `CampaignStateManager.ApplyEncounterToScene()`

```csharp
public FightSO CurrentFight { get; private set; }

private void EnterBattleScene()
{
    // Every real navigation path only ever loads BattleScene once MapManager has already resolved
    // any loadout-pick phase — the current encounter is always the fight being played.
    ApplyLoadoutToG();
    ApplyEncounterToScene();
}

public void ApplyEncounterToScene()
{
    EnergyController.Instance.ApplyCampaignEnergy();

    var fight = CampaignManager.Instance.CurrentFight;
    CurrentFight = fight;
    G.EnemyView.ReplaceHeroAvatar(fight.enemyData.enemyAvatarPrefab);
    G.ApplyCampaignEnemyCreatures(ResolveEnemyCreatures(fight));
}
```

`public` (not `private`) so it's callable from outside this object's own lifecycle (e.g. debug
tooling). No `CampaignManager.Instance == null` guard (per CLAUDE.md rule 5) — `CampaignManager`
is placed on the same `CampaignProgress.prefab` as `CampaignStateManager` and is guaranteed present
wherever this runs. Unlike before this refactor, there's no longer any `is not FightSO` check here —
`CampaignManager.CurrentFight` is unconditionally a `FightSO` now, since it's the only type left in
`EncounterListSO.fights`.

**Enemy creature roster now varies per fight.** `ResolveEnemyCreatures(fight)` returns
`fight.enemyData.creatures` if set, otherwise falls back to **`G.DefaultCreatures`** (logging a
warning) — this is the guard for the "new `FightSO` fields silently default to unset on existing
assets" Gotcha below, since `creatures` was added after `Fight2`–`Fight5` were authored and they don't
have it set yet. The fallback deliberately targets `G.DefaultCreatures`, not `G.EnemyCreatures` itself
— see CLAUDE.md's player/enemy creature pool rule for why (a mutable field with no per-encounter reset
would silently leak the previous fight's roster forward). The result is pushed into `G.EnemyCreatures`
via `G.ApplyCampaignEnemyCreatures` (`docs/G.md`), which `SlotMachine`'s enemy-side instance and
`PreferredCreatureTypeDecision` (`docs/AI.md`) both read live — no caching, so this is a single write
per encounter, not something every read site needs to know about.

## Soft reload and scene-aware navigation

`SceneManager.LoadScene` collapses/rebuilds the entire scene hierarchy — disruptive and unnecessary
when the player is already sitting in `BattleScene` and just wants to restart. `MapScene` exists (see
"MapScene" below) so `CampaignManager.LoadCurrentEncounter()` (the shared tail end of
`StartNewRun()`/`AdvanceToNextEncounter()`) is scene-aware:

```csharp
private void LoadCurrentEncounter()
{
    if (SceneManager.GetActiveScene().name == SceneNames.MapScene)
        MapManager.Instance.RefreshForCurrentEncounter();
    else
        SceneManager.LoadScene(SceneNames.MapScene);
}
```

Every transition routes through `MapScene` first — `MapManager` decides whether the new current
fight's loadout-pick phase needs to show in place first, or whether it hands off to `BattleScene`
directly — so this method only ever needs to refresh `MapManager` in place (already in `MapScene`) or
load `MapScene` (arriving from `BattleScene` after a fight/reward). `BattleScene` is now only ever
entered by `MapManager`.

**The old debug `ProcessAllEncountersInBattleScene` escape hatch (which kept the entire campaign in
`BattleScene`, skipping `MapScene` entirely) was removed** — fully preserving it under the new
per-fight loadout/reward sub-phase design would have required duplicating the
loadout→shake→battle sequencing inside `BattleScene` as well as `MapScene`. In exchange, the new
per-node sequencing gets its own headless/instant-resolve building blocks — see "Instant-resolve
counterparts" below.

## `Hero.GetMaxHealth()` — symmetric enemy branch

```csharp
protected override float GetMaxHealth()
{
    if (this == G.PlayerHero)
        return CampaignStateManager.Instance.CurrentMaxHp;
    if (this == G.EnemyHero && CampaignStateManager.Instance.CurrentFight != null)
        return CampaignStateManager.Instance.CurrentFight.enemyData.hp;
    return Data.health;
}
```

`CurrentMaxHp` (`CampaignStateManager.cs`) is `CurrentRun.maxHp` plus every claimed `HpBoostRewardSO`'s
`bonusHp` — see `docs/Rewards.md`.

Keeps `Hero.cs`'s dependency surface unchanged — still only one campaign singleton, now
`CampaignStateManager` instead of `CampaignManager`. No `CampaignStateManager.Instance != null`
guard (rule 5) — any `Hero` only ever exists inside `BattleScene`, where `CampaignStateManager` is
guaranteed present. The `CurrentFight != null` check that remains is defense against reading before
the first `ApplyEncounterToScene()` call in a session, not a genuine "might not be a fight" case
anymore (every list entry is a `FightSO`).

## Fight results: victory/defeat resolution

`GameOverState` (`docs/GameLoop.md`) used to be a genuine dead end — it logged the winner and never
called `CompleteState()`, leaving the game frozen until a manual restart. It now delegates to
`CampaignManager` right after logging, still without calling `CompleteState()` itself (the
round loop still stops there — see `docs/GameLoop.md`'s "Gotchas"):

```csharp
protected override void OnEnter()
{
    bool playerDead = G.PlayerHero.Health.IsDead();
    bool enemyDead = G.EnemyHero.Health.IsDead();

    if (playerDead && enemyDead)
    {
        Debug.Log("[GameOver] Draw! Both heroes have fallen.");
        CampaignManager.Instance.ResolveDefeat();
    }
    else if (playerDead)
    {
        Debug.Log("[GameOver] Enemy wins! Your hero has fallen.");
        CampaignManager.Instance.ResolveDefeat();
    }
    else if (enemyDead)
    {
        Debug.Log("[GameOver] Player wins! Enemy hero has fallen.");
        CampaignManager.Instance.ResolveVictory();
    }
}
```

**Win condition simplified for the campaign era**: `GameManager.IsGameOver()` used to require
eliminating a side entirely (hero *and* every summoned creature — `IsSideAlive` checked both). It's
now purely hero death (`G.PlayerHero.Health.IsDead() || G.EnemyHero.Health.IsDead()`) — each
encounter's enemy hero is the boss, so killing it ends the fight immediately regardless of any
creatures it still has on the field. `IsSideAlive` was removed from both `GameManager` and
`GameOverState` (it was duplicated between them).

A draw is treated the same as a defeat (reload the current encounter) — the user-facing spec only
distinguishes victory from "not victory," and a simultaneous double-KO is rare enough not to warrant
its own path.

`ResolveVictory()`/`ResolveDefeat()` (in `CampaignManager.cs`, real game logic — not
debug-only) both wait `BattleResultDelaySeconds` (4s) via `Utils.DoAfterDelay.Execute` before doing
anything, so the "Player wins!"/"Enemy wins!" console message (and whatever UI eventually shows it)
has time to actually be seen:

```csharp
public void ResolveVictory()
{
    int generation = GameManager.Instance.Generation;
    DoAfterDelay.Execute(() =>
    {
        if (GameManager.IsStale(generation)) return;

        if (CurrentFight.hasReward)
            BattleRewardPresenter.Instance.ShowReward(CurrentFight);
        else
            CompleteCurrentEncounter();
    }, BattleResultDelaySeconds);
}

public void ResolveDefeat()
{
    int generation = GameManager.Instance.Generation;
    DoAfterDelay.Execute(() =>
    {
        if (GameManager.IsStale(generation)) return;
        ResetCurrentEncounter();
    }, BattleResultDelaySeconds);
}

public void CompleteCurrentEncounter()
{
    if (HasNextEncounter) AdvanceToNextEncounter();
    else CompleteCampaign();
}
```

- **Victory grants a reward only if the fight's `hasReward` flag is set.** If it is,
  `ResolveVictory()` calls `BattleRewardPresenter.Instance.ShowReward(CurrentFight)`, which
  instantiates `RewardEncounter.prefab` as an overlay directly inside `BattleScene` (see "Encounter
  dispatch" below) — the campaign only actually advances once that pick is confirmed
  (`BattleRewardPresenter.HandleRewardCompleted()` calls `CompleteCurrentEncounter()`). If `hasReward`
  is false, `ResolveVictory()` calls `CompleteCurrentEncounter()` directly, same as before.
- **`CompleteCurrentEncounter()`** is the shared "advance or finish the campaign" branch — used by
  `ResolveVictory()` when there's no reward, and by `BattleRewardPresenter` once a reward pick is
  confirmed — so a fight that happens to be the campaign's last entry correctly completes the
  campaign instead of hitting `AdvanceToNextEncounter()`'s "already at the last encounter" no-op.
- **The staleness guard (`GameManager.Generation`/`IsStale`, see `docs/GameLoop.md`) protects the
  *delayed* transition specifically**, following the same pattern `AIController`/`ExperienceManager`
  already use for their own `DoAfterDelay` closures. Without it: if the player manually restarts
  (pause menu → `ResetCurrentEncounter()` → `GameManager.RestartBattle()`, which bumps `Generation`)
  during the 4-second window, the still-pending delayed callback would later fire anyway and call
  `AdvanceToNextEncounter()`/`ResetCurrentEncounter()` a second time — for victory, incorrectly
  advancing past an encounter the player just manually restarted.
- **`CompleteCampaign()`** is a deliberately minimal placeholder — logs
  `"[Campaign] Congratulations! You've completed the campaign."` and sets `Time.timeScale = 0f`.
  No dedicated "campaign complete" screen exists yet; this is just a clear, unmistakable stop
  distinct from `GameOverState`'s old silent freeze. `Time.timeScale = 0f` doesn't disable
  `PauseMenuController` (`Update()` still runs, so Escape/Restart still work) — revisit once a real
  end screen exists.
- **`ResetCurrentEncounter()` (defeat path) never touches `RunState` at all** — no reward, no index
  change — satisfying "restarting must not duplicate rewards or advance campaign progress" and
  "restarting must not reset the entire run" directly: it's the exact same method the pause menu
  and debug tool already use for a plain mid-battle restart.

## Encounter dispatch: `MapManager` (loadout) and `BattleRewardPresenter` (reward)

Both `RewardEncounter` and `LoadoutPickEncounter` split into a headless-testable backend plus a
paired `<Name>View` that owns all the UI (rule 28). There is no longer a single generic dispatcher
that reads an `EncounterSO.EncounterPrefab` field — each pick-screen type has exactly one fixed
scenario it plays in, so its own dispatcher instantiates its own fixed prefab reference directly:
`MapManager` (`Global/Map/MapManager.cs`, `MapScene`-local, see "MapEncounterPoint and MapManager"
below) owns `loadoutEncounterPrefab` and plays the loadout pick in place, while
`BattleRewardPresenter` (`Global/Campaign/BattleRewardPresenter.cs`, `BattleScene`-local singleton)
owns `rewardEncounterPrefab` and plays the reward pick after victory. Both wait for the instantiated
`Encounter`'s `OnCompleted` and clean it up the same way:

```csharp
public class BattleRewardPresenter : MonoBehaviour
{
    public static BattleRewardPresenter Instance { get; private set; }

    [SerializeField] private RewardEncounter rewardEncounterPrefab;
    [SerializeField] private Transform encounterParent;

    private RewardEncounter _activeReward;

    void Awake() => Instance = this;

    void Start() => GameManager.OnBattleRestart += DestroyActiveReward;
    void OnDestroy() => GameManager.OnBattleRestart -= DestroyActiveReward;

    public void ShowReward(FightSO fight)
    {
        DestroyActiveReward();
        _activeReward = Instantiate(rewardEncounterPrefab, encounterParent);
        _activeReward.OnCompleted += HandleRewardCompleted;
        _activeReward.Play(fight.rewardAmount);
    }

    private void HandleRewardCompleted()
    {
        DestroyActiveReward();
        CampaignManager.Instance.CompleteCurrentEncounter();
    }

    private void DestroyActiveReward() { /* unsubscribe + Destroy + null out _activeReward */ }
}
```

- **`RewardEncounter` (backend) `Play(int rewardAmount)`, `Confirm()` completes only via the Claim
  button** — deliberately not click-anywhere, so a stray selection change alone can't grant a reward
  early. The guaranteed energy reward is applied and saved immediately in `Play()`; `Confirm()`
  (called by `RewardEncounterView` from the Claim button, or directly by test code) claims whichever
  `RewardSO` was selected via `SelectReward()` — see `docs/Rewards.md` for the full card
  hierarchy/draw algorithm and the `RewardEncounterView` walkthrough. `Confirm()`'s
  `RunState.Claim`/`Save()` always run unconditionally; only the *timing* of `Complete()` differs —
  immediate if `Headless`, otherwise whenever `RewardEncounterView.HandleClaimed()` finishes its
  discard animation and calls `CompletePresentation()`.
- **`LoadoutPickEncounter` (backend) `Play()` (no data — it never needed any), `Confirm()` completes
  only via the Finish button** — browsing/comparing/swapping slots never completes the encounter by
  itself. See `docs/Loadout.md` for the full `SlotKind`/`SlotRef`-addressed API.
- **Why `RewardEncounterView`/`LoadoutPickEncounterView` subscribe in `Awake()`, not `Start()`**
  (CLAUDE.md rule 28): `MapManager`/`BattleRewardPresenter` call `Instantiate()` then `Play()`
  synchronously in the same method — Unity runs `Awake()` synchronously inside `Instantiate()` but
  defers `Start()` to later that frame, so a `Start()`-based subscription would miss `Play()`'s
  inline events and never spawn any UI in real (non-headless) play.
- **Why `BattleRewardPresenter` subscribes to `GameManager.OnBattleRestart`**: `GameOverState` halts
  the round loop but doesn't disable `PauseMenuController` — a player can hit Escape → Restart while
  the reward overlay is still showing, which fires `OnBattleRestart` and would otherwise resume
  battle underneath a stale pick screen. `DestroyActiveReward()` cleans it up (CLAUDE.md rule 16 —
  this is battle-scoped transient state, same reasoning the old `EncounterPlayer` followed).

**Why the round loop can no longer accidentally progress during a pick screen — no gate needed
anymore.** `SlotMachine.Update()` reads `Keyboard.current.spaceKey` directly, bypassing UI raycasts
entirely, so a full-screen overlay alone was never enough to stop a stray Space press — this used to
require an explicit `is not FightSO` gate in `RollStateManager.ActivateSlotMachine()`/
`RollState.OnEnter()`. That gate is gone now, and it's provably safe to have removed it: every
`EncounterListSO` entry is a `FightSO`, so `BattleScene`'s round loop is now *always* running the
actual fight it's supposed to — loadout pick never occupies `BattleScene` at all (it's fully resolved
in `MapScene`, before `SceneManager.LoadScene(BattleScene)` is ever called), and the reward pick only
ever shows strictly *after* `GameOverState`, once `GameManager.HandleHeroDied()` has already
permanently stopped the round loop coroutine (it never restarts except via `RestartBattle()`). See
`docs/SlotMachine.md`.

**Known gap: instant-resolve counterparts exist, but a full end-to-end headless test harness doesn't
yet.** Both `LoadoutPickEncounter` and `RewardEncounter` are fully headless-drivable in isolation — a
bare `GameObject`, `AddComponent<LoadoutPickEncounter>()`/`AddComponent<RewardEncounter>()`,
`Headless = true`, `Play()` + their respective methods called directly, no prefab/UI/dispatcher
involved at all — see `docs/Loadout.md`'s and `docs/Rewards.md`'s verification steps. On top of that,
`MapManager.ResolveCurrentPointInstant()` and `BattleRewardPresenter.ShowRewardInstant(FightSO)` (new,
CLAUDE.md rule 7 — matching how `BattleState.ResolveBattleInstant()`/`NukeState.ResolveNukesInstant()`
etc. currently have no real caller either) collapse the *sequencing* around those backends to zero
animation delay: `ResolveCurrentPointInstant()` marks the point current, headlessly confirms the
loadout pick unchanged (if any), and loads `BattleScene` immediately; `ShowRewardInstant()` auto-picks
the first drawn reward and confirms immediately, chaining into `CompleteCurrentEncounter()` the same
way the real UI path does. What's still an accepted gap: nothing yet composes these into a full
"run the whole campaign end-to-end headlessly" harness — that's forward-looking infrastructure for a
future automated balance-testing tool, same as every other `...Instant()` method in this codebase.
Battle/roll resolution itself has the same kind of gap, one level down — see `docs/GameLoop.md`'s
Gotchas.

## Debug-only surface: `CampaignManager.Debug.cs`

Per CLAUDE.md rule 20, `SetSessionEncounterOverride`/`SetSessionEncounterIndexOverride` — called by
nothing except `CampaignProgressTool`/`CampaignDebugTool` — live in a separate
`CampaignManager.Debug.cs` partial class file instead of the main `CampaignManager.cs`, the same
partial-class split pattern `AttacksResolver.cs`/`AttacksResolver.Mechanics.cs` already uses for a
different reason. `SetSessionEncounterOverride` now takes a `FightSO` directly (there's no other
`EncounterSO` subtype left to accept). The `_overrideWindowOpen` field and the actual guarded
`RunState` write instead live on `CampaignStateManager.Debug.cs` (see `docs/Campaign.md`), since they
mutate/gate `RunState` directly — `SetSessionEncounterIndexOverride(int)` here clamps against this
object's own `EncounterList`, then calls
`CampaignStateManager.Instance.ApplySessionEncounterIndexOverride(clampedIndex)` to actually
apply it.

**Why `SetSessionEncounterIndexOverride(int)` exists alongside `SetSessionEncounterOverride(FightSO)`**:
historically (before `RunState`'s owner exposed a single source of truth for the current index —
see "Ownership split" above), mutating `RunState.currentEncounterIndex` directly had zero
effect on which encounter actually loaded, since navigation bootstrapped its own
separate index copy independently — a real, caught-live bug. Now that
`CampaignStateManager.CurrentRun` *is* the one `RunState` object every consumer reads
through, `CampaignDebugTool`'s direct `ReplaceRunState(run)` call already relocates the index on
its own, making the `ApplyRunStateOverride` helper's follow-up call to this method a
harmless no-op-equivalent for that caller (see `docs/Campaign.md`). It remains genuinely useful for
`CampaignProgressTool`'s narrower case: overriding just the current encounter/index *within* the
existing `RunState`, without replacing the whole thing — `CampaignDebugTool` doesn't hold an
`EncounterListSO` reference to resolve an index into a `FightSO` the way `CampaignProgressTool`
does, so the index-based overload exists specifically for that caller too — same `_overrideWindowOpen`
gating (on `CampaignStateManager`) and `Mathf.Clamp` bounds-safety (on `CampaignManager`, against its
own `EncounterList`) as the `FightSO` overload (which just resolves its argument to an index and
forwards to this one).

## `CampaignProgressTool`

Editor-only, lives on `Global/CampaignProgressTool` next to `CampaignDebugTool`/`BalanceTool` in
`BattleScene.unity`. One field pair: `overrideEncounter` (bool) + `encounter` (`FightSO`, plain
object-reference field — same pattern `CampaignDebugTool` uses for its archer/tank/mage/nuke/spell
overrides, not a custom named dropdown). Applies in `Awake()`, calling
`CampaignManager.Instance.SetSessionEncounterOverride(encounter)`, which resolves the
picked asset to its index in `encounterList.fights` — never persisted, mirrors
`CampaignDebugTool`'s overrides never calling `Save()`.

**Requires Script Execution Order `CampaignManager` (`-150`) and `CampaignStateManager` (`-100`)
before `CampaignProgressTool` (default order)** — the same class of Awake-vs-Awake ordering issue
`docs/Campaign.md` already documents between `CampaignStateManager` and `CampaignDebugTool` (two
same-phase callbacks with no default ordering guarantee). No SEO relationship is needed between
`CampaignProgressTool` and `CampaignStateManager`'s own `Start()`/`sceneLoaded` handling — those are
safe purely from the universal Awake-phase-precedes-Start-phase guarantee, the same reasoning
`Hero.Start()` already relies on for `CampaignStateManager`.

**The override only takes effect once per session, even though `CampaignProgressTool.Awake()` fires
on every `BattleScene` load.** `CampaignProgressTool` is a plain scene-local component — its
serialized `overrideEncounter` checkbox stays checked across reloads, so its `Awake()` calls
`SetSessionEncounterOverride(encounter)` again every time the scene loads. Without a guard, that
would silently override any real navigation. `CampaignStateManager`'s `_overrideWindowOpen` flag
closes this off: it's `true` only during the `Awake()` phase of the one scene load where
`CampaignStateManager` itself was first created (closed in its own `Start()`, which — being
`DontDestroyOnLoad` — only ever runs once per session). Any later `SetSessionEncounterOverride` call,
from a later reload's fresh `CampaignProgressTool.Awake()`, is a no-op with a warning. In practice,
since navigation is now a soft in-place reload (see above) rather than a scene reload,
`CampaignProgressTool.Awake()` mostly won't even fire again after the first load — the window flag is
defense-in-depth for the cases that do still reload the scene (arriving from elsewhere, or the
override window itself).

Its custom Editor draws three buttons — "Start New Run", "Reset Current Encounter", "Advance To
Next Encounter" — disabled outside Play mode, calling the exact same `CampaignManager`
methods real gameplay UI will use later. No separate debug-only logic exists.

## `CampaignManagerEditor` — Inspector visibility

Per CLAUDE.md rule 19 ("surface important runtime state in the Inspector"), `CampaignManager`
gets a custom Editor (`Global/Campaign/Editor/CampaignManagerEditor.cs`) showing, read-only
and live-updating during Play mode: the current encounter index, the resolved `FightSO` object
reference, and its `fightId` string. Selecting the `CampaignProgress`
GameObject during Play always shows what encounter you're actually on — no debugger needed. Guards
against an unassigned/empty `encounterList` with a help box instead of throwing.

## Editor setup

- Both `Assets/Game/_Scenes/BattleScene.unity` and `Assets/Game/_Scenes/MapScene.unity` are
  registered in Build Settings' Scenes In Build (required for `SceneManager.LoadScene` to resolve
  either by name).
- `CampaignProgress` is `_Prefabs/Campaign/CampaignProgress.prefab` (root-level GameObject,
  `CampaignManager` + `CampaignStateManager` + `DebugRewards` components, `encounterList`/
  `catalog`/`rewardList` wired, plus a `RunStateMonitor` child — see `docs/Campaign.md` and
  `docs/Rewards.md`), instanced in **both** `BattleScene.unity` and `MapScene.unity` — whichever
  loads first survives (`DontDestroyOnLoad` duplicate-guard), the other's copy self-destructs. Being
  an actual prefab means adding a new component/child (like `RunStateMonitor`) only needs doing once
  and both scene instances pick it up automatically; per-instance field values (`encounterList`/
  `catalog`/`rewardList` references) still need matching independently if they ever diverge, same as
  any prefab instance override. `encounterList` points at
  `_ScriptableObjects/Campaign/Encounters/AllEncounters.asset`. `Global/CampaignProgressTool`
  (`CampaignProgressTool` component) stays `BattleScene`-only — no equivalent tooling exists in
  `MapScene` yet.
- `CampaignDebugTool` (root-level GameObject, `CampaignDebugTool` component) is likewise placed in
  **both** scenes, same `DontDestroyOnLoad` duplicate-guard pattern as `CampaignProgress` — needed so
  its overrides apply regardless of which scene the session actually boots from. Unlike
  `CampaignProgress`, it is **not** a shared prefab — each placed copy's fields are independently
  serialized, so only whichever one wins the duplicate-guard race actually has its fields read;
  configure overrides on the copy in the scene you're actually about to press Play from.
- `Global/BattleRewardPresenter` (`BattleRewardPresenter` component, `rewardEncounterPrefab` →
  `RewardEncounter.prefab`, `encounterParent` → the scene's root-level gameplay `Canvas`) stays in
  `BattleScene.unity` (replaces the old `Global/EncounterPlayer`). `MapScene.unity`'s `Global/
  MapManager` GameObject (`MapManager` component) owns `loadoutEncounterPrefab` → `LoadoutEncounter
  .prefab` — see "`MapEncounterPoint` and `MapManager`" below.
- `_Prefabs/Campaign/LoadoutEncounter.prefab` (`Canvas`, 9 equipped-slot anchors, an Available pool
  grid, a Comparison panel, Swap/Finish buttons — see `docs/Loadout.md`'s Editor setup checklist)
  and `_Prefabs/Campaign/RewardEncounter.prefab` (`Canvas` + tint `Image`, a Claim `Button`, three
  `cardSlots` anchors (`CardSlot1`/`CardSlot2`/`CardSlot3`, each holding a disabled placeholder card
  — CLAUDE.md's anchor+disabled-template rule) and `cardPrefab` (`_Prefabs/UI/Cards/RewardCard.prefab`)
  for the 3 drawn reward cards — see `docs/Rewards.md`) — each is a fully self-contained `Canvas` (own
  `CanvasScaler`/`GraphicRaycaster`, `sortingOrder 5`), instantiated fresh by whichever dispatcher is
  active (`MapManager` or `BattleRewardPresenter`) and destroyed on completion, never left placed in a
  scene. **Both prefabs' root GameObjects carry both their backend and paired `<Name>View`
  components** (rule 28) — `LoadoutEncounter.prefab` carries `LoadoutPickEncounter` +
  `LoadoutPickEncounterView`, `RewardEncounter.prefab` carries `RewardEncounter` +
  `RewardEncounterView` — every UI reference lives on the `View` component, never the backend.
- `_Prefabs/Map/MapEncounterPoint.prefab` needs a `MapEncounterPoint` component, its
  `futureEncounterVisual`/`currentEncounterVisual`/`completeEncounterVisual` fields wired to the
  prefab's existing named children, an `MMPositionShaker` on the root, an `ArriveFightFeedback` child
  with an `MMF_Player` (an `MMF_PositionShake` feedback targeting that `MMPositionShaker`), and an
  `ArriveRegularFeedback` child with an `MMF_Player` (an `MMF_Scale` feedback, `AnimateScaleTarget` set
  to `CurrentEncounterVisual`'s transform — its default punch-shaped curve already reads as a yoyo pop).
- `MapScene.unity`'s `MapManager` GameObject's `points` array needs one entry per placed
  `MapEncounterPoint` instance, **in the same order as `EncounterListSO.fights`** —
  `AssignEncounters()` maps them positionally, not by any per-instance reference, so array order is the
  only thing that determines which point represents which fight. Adding/removing/reordering fights in
  the list means updating this array to match.
- Script Execution Order: `CampaignManager` = `-150`, `CampaignStateManager` = `-100`. No new SEO
  entries needed for `MapManager` — it only reads `CampaignManager` from its own `Start()`, never
  `Awake()`, so the universal Awake-before-Start guarantee already covers it.
- `MapScene.unity` needs its own root-level `EventSystem` GameObject (`EventSystem` +
  `InputSystemUIInputModule`, same Input Actions asset as `BattleScene`'s) — see Gotchas below.

## Gotchas

- **Every scene with clickable UGUI needs its own `EventSystem` GameObject — it doesn't carry over
  between scenes.** Caught live: `MapScene`'s `LoadoutPickEncounter` panel has a real
  `Canvas`/`GraphicRaycaster`/`Button` (see above), but with no `EventSystem` anywhere in `MapScene`,
  `EventSystem.current` was `null` and clicks were never routed to the Button at all — the panel looked
  interactive but silently ignored every click. `BattleScene` already has one; `MapScene` didn't, since
  it never needed clickable UI before pick screens moved there. Fixed by adding a root-level
  `EventSystem` GameObject to `MapScene.unity` (`EventSystem` + `InputSystemUIInputModule`, CLAUDE.md
  rule 8 — new Input System, not the legacy `StandaloneInputModule`) mirroring `BattleScene`'s.
- **`CampaignProgress` and `CampaignDebugTool` must both stay root-level GameObjects, in every scene
  they're placed in.** `DontDestroyOnLoad` only works on scene roots — parenting either under `Global`
  (where `CampaignProgressTool`/`BattleRewardPresenter` correctly live, since neither needs to persist)
  would silently fail to persist it across scene reloads.
- **`CampaignManagerEditor`/`MapManagerEditor` must guard `!Application.isPlaying` before
  reading anything that touches `RunState`.** `RunState` is only populated by `CampaignStateManager`'s
  `Awake()`, which never runs outside Play mode — a custom Editor that reads
  `manager.CurrentEncounterIndex`/`CurrentFight` unconditionally throws a
  `NullReferenceException` on every Inspector repaint the moment `CampaignProgress` is selected in
  Edit mode.
- **`AdvanceToNextEncounter()` itself still just logs a warning and no-ops if called at the last
  encounter** — but `ResolveVictory()`/`BattleRewardPresenter` never call it in that case, they call
  `CompleteCampaign()` instead (see "Fight results" above). The no-op path only fires if something
  calls `AdvanceToNextEncounter()` directly at the last encounter (e.g. mashing
  `CampaignProgressTool`'s button) — that's an intentionally inert dead-end, not a bug.
- **`fightId`/`isTutorial` on `FightSO` are still inert** — no logic reads them yet, forward-
  looking data for a future pre-fight phase.
- **A left-over saved `RunState` can look like "the wrong default encounter loads."** Progress is
  designed to persist across Editor Play sessions (the whole point of `RunState`/the save system) — if
  `currentEncounterIndex` was previously advanced and never reset, a fresh Play without an override will
  correctly resume from that saved index, not restart at encounter 0. Use `CampaignProgressTool`'s
  "Start New Run" or `CampaignDebugTool`'s "Clear Saved Run" button to get back to a genuinely fresh
  state.
- **New `FightSO` fields silently default to `false`/`0`/`null` on existing assets.** Unity backfills
  `hasLoadoutPick`/`hasReward`/`rewardAmount` to their C# type defaults on any `FightSO` asset that
  predates those fields — a fight that should have a loadout pick or reward will silently have neither
  until the asset is explicitly authored in the Inspector. No error, no warning — it just quietly
  behaves like a bare fight. Always double-check these three fields on a new or renamed `FightSO`
  asset rather than assuming they carried over from wherever the asset was copied from.
  `dirtyTripleStabilization`/`ludoProgressIndex` backfill to `0` the same way, but that's the
  intended "disabled" default for every existing fight, so no action is needed unless a specific
  fight should use it. `playerCleanTripleIndex`/`enemyCleanTripleIndex` backfill to `100` (neutral),
  which matches current global behavior exactly, so no action is needed unless a fight should
  diverge. `playerComebackSettings`/`enemyComebackSettings` are the dangerous ones: they backfill to
  an **empty list**, which silently means "no comeback assistance at all for that side" — no error,
  no warning, the fight just plays without the mechanism. All five existing fights are authored with
  the standard 5-row ladder (`100 → -20`, `60 → 0`, then `25`/`15`/`10` carrying advantage
  thresholds `30`/`40`/`50`); a new fight asset must be given one explicitly.
  `neutralDirtyTripleIndex`/`firstRoundDirtyTripleIndex` backfill to `100`/`50` the same way
  (matching `SlotMachineRigger`'s own prior hardcoded defaults exactly), so an existing fight's
  round-1/base Dirty Triple Index math is unchanged unless explicitly re-authored.
  `enemyData.creatures` is the same story but with a soft landing: `Fight2`–`Fight5` don't have it set
  yet (only `Fight1 Tutorial` does, pointing at its own `Creatures.asset`), so until each is authored
  with its own roster, `ResolveEnemyCreatures` falls back to `G.DefaultCreatures` (logged as a warning)
  rather than crashing.

- **A leftover `CampaignDebugTool` granular override can make the player's pool match the enemy's,
  and it'll look exactly like a bug in whichever creature-pool code was touched most recently.** Real,
  live-caught: `BattleScene.unity`'s `CampaignDebugTool` had `overrideArcher`/`overrideTank`/
  `overrideMage` left checked (pointing at the same `DemonArcher`/`Cyclop`/`Bat` trio used for
  `Fight1`'s enemy roster) from earlier ad-hoc testing — harmless while `MapScene`'s clean copy won
  the cross-scene singleton race in normal play, but the instant `BattleScene` was played directly
  (a common way to test in isolation), `CampaignDebugTool`'s own copy won instead and silently forced
  `RunState.archerId`/`tankId`/`mageId` to the demon ids in `Awake()`, before `ApplyLoadoutToG()` ever
  ran. Combined with the (correct, working-as-designed) new per-fight enemy override, both sides ended
  up rolling the same demon roster — looked like the new enemy-roster code had leaked into the player
  side, but the actual cause was this pre-existing, unrelated checked box. Always leave
  `CampaignDebugTool`'s granular overrides unchecked (bool `0` + referenced asset cleared to
  `{fileID: 0}`, matching every currently-unused override already sitting that way in the same
  component) once done testing with them — see CLAUDE.md's player/enemy creature pool rule.

## `MapScene`

`Assets/Game/_Scenes/MapScene.unity` sits between battles — the pre-battle loadout pick now plays
there, in place at the current node (the same `LoadoutEncounter.prefab` asset — see `docs/Loadout.md`
— this system doesn't change its presentation, only where/when it's shown), and it's the scene that
visually shows campaign progress as points on a path. The post-battle reward pick, by contrast, now
plays in `BattleScene` — see "Encounter dispatch" above. Campaign navigation still isn't player-driven
choice yet — `EncounterListSO` stays a flat linear list, walked in order; `MapScene` visualizes that
order rather than letting the player pick a branch. `CampaignManager`/`CampaignStateManager` both work
from it exactly as designed for (they're cross-scene by construction — see "Two cross-scene-persistent
managers" above): the same root-level `CampaignProgress` GameObject (same components, same
`encounterList`/`catalog`/`rewardList` references) is placed in `MapScene.unity` too, so either scene
can be the session's first-loaded scene under the existing duplicate-guard singleton pattern.

### `MapEncounterPoint` and `MapManager`

- **`Global/Map/MapEncounterPoint.cs`** — one fight's point on the map, three visual states
  (`futureEncounterVisual`/`currentEncounterVisual`/`completeEncounterVisual`, exactly one active at a
  time — `SetFuture()`/`SetCurrent()`/`SetComplete()`, visuals only, no path side effects). Path
  visibility is separate and explicit: `HidePaths()`/`ShowPathIn()`/`ShowPathOut()`, each null-checked
  (`pathIn`/`pathOut` are optional `SplineContainer` references — today just toggled active/inactive,
  no actual spline-draw-in animation yet; see the `MapManager` TODO below). Two feedbacks, both
  `MMF_Player`s per CLAUDE.md rule 15: `arriveFightFeedback` (an `MMF_PositionShake` + sibling
  `MMPositionShaker`, played via `PlayArriveFightFeedback()` right before the fight actually starts —
  either immediately on arrival if the fight has no loadout-pick phase, or after that phase is
  confirmed) and `arriveRegularFeedback` (an `MMF_Scale` punch, played via `PlayArriveRegularFeedback()`
  when the point first becomes current, only if it has a loadout-pick phase to show before the fight —
  a distinct "yoyo" pop instead of the fight's shake). Which `FightSO` a point represents (`Fight`
  getter) is **not** wired per-instance in the Inspector anymore — `SetFight(...)` is called once by
  `MapManager.AssignEncounters()` (see below), positionally against `CampaignManager.EncounterList`.
  `fight` itself stays `[SerializeField]` even though nothing authors it directly (CLAUDE.md rule 19 —
  load-bearing runtime state stays visible in the Inspector by default, not hidden behind a debugger);
  any value seen on it in Edit mode is stale and gets overwritten the instant Play mode starts.
- **`Global/Map/MapManager.cs`** — `MapScene`'s orchestrator, singleton (`Instance`). Holds a
  serialized `MapEncounterPoint[] points` — one entry per fight in `EncounterList`, same order;
  `AssignEncounters()` (called once from `Start()`, before the first `RefreshForCurrentEncounter()`)
  walks `EncounterList.fights` and calls `points[i].SetFight(fights[i])` for each index —
  **throws** if `points.Length < fights.Count` (a scene-setup/content-authoring mismatch that
  should surface immediately, not fail silently or wrap around). Purely driven by explicit calls
  (`Start()`, and `CampaignManager.LoadCurrentEncounter()`'s direct call when already in
  `MapScene`) — there is no `GameManager`/`OnBattleRestart` in `MapScene` to subscribe to.
  `RefreshForCurrentEncounter()` is a gradual, step-by-step reveal — every step visible, nothing jumps
  straight to its end state, and is only ever called when moving to a genuinely new node (run start, or
  after the campaign actually advances past a fight+reward — never mid-node for a loadout pick):
  1. Every point's paths are hidden first — a clean slate, since a point's leftover path state from a
     previous refresh isn't trustworthy (e.g. a debug override jumping the index backward).
  2. Every already-passed point (`index < CurrentEncounterIndex`) snaps to `SetComplete()` with both
     paths shown — old history, nothing new to animate about it. (This is also where the point that was
     current a moment ago — the "previous" point — visually flips to Complete; there's nothing special
     about it once its index is behind the new current one.)
  3. The current point's `PathIn` is revealed, then a pause (`revealDelaySeconds`) — "the player has
     walked up to it." If `CurrentEncounterIndex` is `0` there's no earlier point at all, so step 2 is a
     no-op and this is the only path shown.
  4. `RevealCurrentPoint()`: the current point flips `SetFuture() -> SetCurrent()`. If
     `CurrentFight.hasLoadoutPick` is false: `PlayArriveFightFeedback()` (shake), pause
     (`arriveFeedbackDuration`), then `StartFight()` loads `BattleScene` — done, exactly like a plain
     fight always worked. If true: `PlayArriveRegularFeedback()` (yoyo) instead, pause
     (`arriveFeedbackDuration`), then `StartLoadoutPick()` instantiates `loadoutEncounterPrefab` in
     place and calls `Play()`.
  5. (Only when the fight has a loadout-pick phase) Once the loadout pick's `OnCompleted` fires
     (`HandleLoadoutPickCompleted()`): destroy it, pause again (`postLoadoutPauseSeconds` — the "wait a
     bit" beat before the fight actually starts), then `ArriveAtFightAfterLoadout()` plays
     `PlayArriveFightFeedback()` (the shake, now happening right before the fight rather than on
     arrival), pauses `arriveFeedbackDuration` again, then `StartFight()` loads `BattleScene`.

  Completing the loadout pick **does not** call `CampaignManager.Instance.CompleteCurrentEncounter()`
  — unlike the old design (where a pick screen was its own list entry and completing it advanced the
  index), it just continues into the same node's fight. The campaign only actually advances once the
  fight is won and any reward pick (in `BattleScene`, see "Encounter dispatch" above) is confirmed.

  **TODO, not yet built**: step 2's path reveal is instant for every point, including the one that just
  became history — the ask was for that one specifically to reveal knot-by-knot along the spline
  (imitating the hero steadily walking it) once path *rendering* exists (`SplineContainer`s are data-only
  today, no visual component). Instant-enable is the interim behavior.

  No re-entrancy guard and no staleness/`Generation` guard — normal gameplay only ever calls this again
  after the previous call's full sequence has resolved (calls never overlap); `CampaignProgressTool`'s
  Play-mode buttons are the one exception that could call in mid-sequence, left unguarded as debug-only
  exposure (YAGNI).
- **`Global/Map/Editor/MapManagerEditor.cs`** — rule-19 read-only live Inspector view (mirrors
  `CampaignManagerEditor`): the resolved current `MapEncounterPoint` and current encounter
  index/asset.

### `SceneNames`

`Global/Campaign/SceneNames.cs` — `BattleScene`/`MapScene` string constants for every
`SceneManager.LoadScene` call site above.

## Related docs

- `docs/Campaign.md` — `RunState`/`CampaignStateManager`/`GameCatalog`, the data layer this system
  builds the actual sequence on top of, and the Save system section (storage abstraction, versioning,
  save trigger points).
- `docs/GameLoop.md` — `GameManager.RestartBattle()`/`OnBattleRestart`, what
  `CampaignManager.ResetCurrentEncounter()` delegates to and what `BattleRewardPresenter`
  subscribes to.
- `docs/SlotMachine.md` — why the round loop is provably inert during any pick screen without needing
  an explicit gate anymore.
- `docs/G.md` — `G.EnemyView`/`G.EnemyHero`/`G.EncounterList`, read by
  `CampaignStateManager.ApplyEncounterToScene()` and `Hero.GetMaxHealth()`.
- `docs/Rewards.md` — the reward-card pick `BattleRewardPresenter` shows alongside the guaranteed
  energy reward: `RewardSO` hierarchy, `RewardListSO`, `RewardDrawer`'s draw algorithm,
  `RewardBonuses`' resolver-side stat-boost hook.
- `docs/Loadout.md` — the pre-battle loadout picker `MapManager` shows: `SlotKind`/`SlotRef`
  addressing, `MiniCard`, `LoadoutPickEncounterView`.
