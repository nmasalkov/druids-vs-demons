# Encounters & Campaign Progress

## What this system does

Builds the actual **fight sequence** on top of `docs/Campaign.md`'s data layer:
`EncounterListSO` is the ordered campaign, and `CampaignManager` is the real API — not
debug-only logic — for starting a run, restarting the current encounter, and advancing to the next
one. `RunState` itself is owned by the sibling `CampaignStateManager` (see "Two cross-scene-persistent
managers" below) — `CampaignManager` reads/mutates it through
`CampaignStateManager.Instance.CurrentRun` rather than owning any of it itself. Three `EncounterSO` subclasses exist:
`FightSO` (a real fight, each carrying its own enemy avatar/HP; renamed from `BattleSO` — campaign-
layer naming only, unrelated to the per-turn `BattleState`/`RunBattle()` combat-resolution machinery,
which keeps its own "Battle" naming), `LoadoutPickSO` (a placeholder pre-fight briefing screen), and
`RewardPickSO` (a claimable energy reward screen — this is now where a victory's energy reward
actually comes from, not an automatic grant). `EncounterPlayer` is a generic dispatcher: it
instantiates whatever `Encounter` component `CurrentEncounter.EncounterPrefab` points at, calls
`Play()`, and waits for `OnCompleted` — see "Encounter/EncounterPlayer" below. `FightSO` leaves
`EncounterPrefab` unset, since a fight is driven entirely by `GameManager`'s own round loop, untouched
by this dispatch mechanism. The campaign designer arranges the three types freely in `EncounterListSO`
(e.g. `Fight → RewardPick → LoadoutPick → Fight`); nothing in this system assumes a particular order
or that fights are first/last/adjacent.
`GameOverState` drives `CampaignManager`'s API automatically at the end of a fight: victory
(after a delay) advances to the next encounter or completes the campaign; defeat reloads the current
encounter — see "Fight results" below.

## Key files

- `Global/Campaign/EncounterSO.cs` — abstract marker base for anything that can appear in an
  `EncounterListSO`. Holds one field, `EncounterPrefab` (an `Encounter` reference — see
  "Encounter/EncounterPlayer" below), so `EncounterPlayer` can dispatch generically without a type
  switch; left unset on `FightSO` since a fight isn't played through this mechanism.
- `Global/Campaign/FightSO.cs` — concrete `EncounterSO` for a real fight (renamed from `BattleSO`,
  `[MovedFrom]`-guarded so the existing asset files kept resolving through the rename). `fightId`
  (string, `[FormerlySerializedAs("battleId")]`), `isTutorial` (bool), `enemyData` (`EnemyData`).
  `fightId`/`isTutorial` aren't consumed by any logic yet — forward-looking data for a future
  pre-fight phase. No longer carries an energy reward — see `RewardPickSO`.
- `EnemyData` (nested `[Serializable] struct` in `FightSO.cs`) — `enemyAvatarPrefab` (`GameObject`),
  `hp` (int). Expandable later (e.g. an AI tactic enum) without touching `FightSO` itself.
- `Global/Campaign/LoadoutPickSO.cs` — placeholder pre-fight encounter: just `briefingText`
  (string, defaults to `"This will be a battle!"`). Played by `LoadoutEncounter`.
- `Global/Campaign/RewardPickSO.cs` — post-fight reward encounter: `energyReward` (int) — moved
  off `FightSO`, see "Fight results" below. Played by `RewardEncounter`, which also offers a pick
  of 3 reward cards on top of the guaranteed energy — see `docs/Rewards.md`.
- `Global/Campaign/Encounter.cs` — abstract `MonoBehaviour` base for anything `EncounterPlayer`
  instantiates: `Play(EncounterSO data)` (abstract) + `event Action OnCompleted` + protected
  `Complete()`. Mirrors `GameState`'s "one runner, self-contained states" shape (`docs/GameLoop.md`).
- `Global/Campaign/LoadoutEncounter.cs` — plays `LoadoutPickSO`: shows `briefingText`, completes on
  a mouse click anywhere.
- `Global/Campaign/RewardEncounter.cs` — plays `RewardPickSO`: grants + clamps `currentEnergy`
  immediately on `Play()`, then draws and offers 3 reward cards (`RewardDrawer`/`RewardCard` — see
  `docs/Rewards.md`). Completes **only** via its Claim button (a stray click elsewhere must not
  grant a reward early), which claims the selected card and calls `Save()` before completing.
- `Global/Campaign/EncounterPlayer.cs` — generic instantiate/wait/cleanup dispatcher. Knows nothing
  about any individual `Encounter`'s presentation — see "Encounter/EncounterPlayer" below.
- `Global/Campaign/EncounterListSO.cs` — `List<EncounterSO> encounters`, the ordered campaign
  sequence, indexed by `CampaignManager.CurrentEncounterIndex`. Can mix all three encounter
  types in any order.
- `Global/Campaign/CampaignManager.cs` — the navigation API (below): which encounter is current,
  advance/restart/defeat/victory/complete. Reads/mutates `RunState` through
  `CampaignStateManager.Instance.CurrentRun` (see "Two cross-scene-persistent managers" below) —
  pure navigation logic, owns no data of its own beyond `EncounterListSO` and the
  `ProcessAllEncountersInBattleScene` debug flag.
- `Global/Campaign/CampaignManager.Debug.cs` — `partial class` split of the same type
  holding the debug-only `SetSessionEncounterOverride`/`SetSessionEncounterIndexOverride` surface
  (CLAUDE.md rule 20) and the `ProcessAllEncountersInBattleScene` flag.
- `Global/Campaign/CampaignStateManager.cs` (+ `CampaignStateManager.Debug.cs`) — sole owner/loader
  of `RunState` (see "Two cross-scene-persistent managers" below) plus the loadout/energy/enemy-avatar
  application that used to live on a `BattleScene`-only `CampaignManager`.
- `Global/Campaign/Editor/CampaignManagerEditor.cs` — read-only Inspector view of the
  current encounter index/asset (CLAUDE.md rule 19).
- `Global/Campaign/CampaignProgressTool.cs` + `Global/Campaign/Editor/CampaignProgressToolEditor.cs`
  — Editor-only debug tool mirroring `CampaignDebugTool`'s pattern.
- `PlayerView/HeroView.cs` — gained `ReplaceHeroAvatar(GameObject avatarPrefab)`.
- `Global/Campaign/CampaignStateManager.cs` — `CurrentRun` is the actual owned `RunState`; has
  `CurrentFight` (renamed from `CurrentBattle`) and `ApplyEncounterToScene()` (called whenever
  `BattleScene` is entered, applies both energy and enemy avatar/HP — see `docs/Campaign.md`'s "Load
  → resolve → apply flow").
- `Units/Hero.cs` — `GetMaxHealth()` gained a symmetric enemy-side branch.
- `UI/PauseMenuController.cs` — `HandleRestartClicked()` now calls
  `CampaignManager.Instance.ResetCurrentEncounter()` instead of calling
  `GameManager.RestartBattle()` directly.
- `Global/GameManager/GameOverState.cs` — no longer a pure dead end; calls
  `CampaignManager.Instance.ResolveVictory()`/`ResolveDefeat()` after logging. See
  "Fight results" below.
- `Global/Campaign/CampaignProfileSO.cs` — Editor-authorable whole-`RunState` snapshot for
  `CampaignDebugTool`'s "Use Debug Profile" — see `docs/Campaign.md`.

Assets: `_ScriptableObjects/Campaign/Encounters/EncounterList.asset` +
`1Battle.asset`/`2Battle.asset`/`3Battle.asset` (physical filenames deliberately left as-is across
the `BattleSO`→`FightSO` rename — low-value churn, not required), referencing
`_Prefabs/Characters/Units/Avatars/Enemies/EnemyAvater.prefab` / `EnemyAvater 1.prefab` /
`EnemyAvater 2.prefab` at 10/20/30 HP respectively. Those three prefabs were previously orphaned,
unused clones of `Player.prefab` (identical `Hero`/`HeroAnimator`/`CastOrigin` etc. wiring) — this
system is what finally puts them to use. The three fights' old 40/60/80 `energyReward` values were
dropped when that field moved to `RewardPickSO` (see "Fight results" below) — place explicit
`RewardPickSO` assets in `EncounterList.asset` with equivalent values wherever a reward should still
happen. `_Prefabs/Campaign/LoadoutEncounter.prefab`/`RewardEncounter.prefab` are the two `Encounter`
prefabs `LoadoutPickSO`/`RewardPickSO` assets point their `EncounterPrefab` field at.

## Ownership split: `CampaignStateManager` vs `CampaignManager`

- **`CampaignStateManager`** owns *the run's state* (`RunState` itself — loaded/created once in
  its own `Awake()`, see "Two cross-scene-persistent managers" below) and *applying the run to the
  current scene* (loadout/energy/enemy-avatar application — see `docs/Campaign.md`).
- **`CampaignManager`** owns *where you are in the campaign* (current encounter index/asset, just
  `CampaignStateManager.Instance.CurrentRun.currentEncounterIndex` read live — no separate cached
  field) and the three navigation actions (`StartNewRun`, `ResetCurrentEncounter`,
  `AdvanceToNextEncounter`). It is the only thing that knows how to move forward — both
  `CampaignProgressTool` and (later) real gameplay UI call into it, never duplicate its logic.
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

    public EncounterSO CurrentEncounter => encounterList.encounters[
        Mathf.Clamp(CampaignStateManager.Instance.CurrentRun.currentEncounterIndex, 0, encounterList.encounters.Count - 1)];
    public bool HasNextEncounter => CampaignStateManager.Instance.CurrentRun.currentEncounterIndex < encounterList.encounters.Count - 1;

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

`SetSessionEncounterOverride`, `SetSessionEncounterIndexOverride`, and the
`ProcessAllEncountersInBattleScene` flag live in `CampaignManager.Debug.cs` instead — see
"Debug-only surface" below. `ParseExternalRunState` and the `_overrideWindowOpen` field live on
`CampaignStateManager.Debug.cs`, since they mutate/gate `RunState` directly.

## Two cross-scene-persistent managers

Every other singleton (`G`, `GameManager`, `EnergyController`, `RollStateManager`) lives on the
per-scene `Global` GameObject and is recreated on every scene load (`docs/GameLoop.md`).
`CampaignStateManager` and `CampaignManager` are the exception: both live on
`_Prefabs/Campaign/CampaignProgress.prefab` (a **root-level** GameObject — `DontDestroyOnLoad` only
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
    // ...redirect-to-MapScene check (see docs/Campaign.md)...
    ApplyLoadoutToG();
    ApplyEncounterToScene();
}

public void ApplyEncounterToScene()
{
    EnergyController.Instance.ApplyCampaignEnergy();

    if (CampaignManager.Instance.CurrentEncounter is not FightSO fight) return;
    CurrentFight = fight;
    G.EnemyView.ReplaceHeroAvatar(fight.enemyData.enemyAvatarPrefab);
}
```

`public` (not `private`) specifically so `CampaignManager.LoadCurrentEncounter()` can call
it again outside the `EnterBattleScene()` pipeline — see "Soft reload" below. No
`CampaignManager.Instance == null` guard (per CLAUDE.md rule 5) — `CampaignManager`
is placed on the same `CampaignProgress.prefab` as `CampaignStateManager` and is guaranteed present
wherever this runs; the `CurrentEncounter is not FightSO` check that remains is genuine type logic
(not every `EncounterSO` is a fight), not a defensive existence check.

## Soft reload and scene-aware navigation

`SceneManager.LoadScene` collapses/rebuilds the entire scene hierarchy — disruptive and unnecessary
when the player is already sitting in `BattleScene` and just wants to restart. But now that
`MapScene` exists (see "MapScene" below), `CampaignManager.LoadCurrentEncounter()` (the
shared tail end of `StartNewRun()`/`AdvanceToNextEncounter()`) is scene-aware:

```csharp
private void LoadCurrentEncounter()
{
    if (ProcessAllEncountersInBattleScene)
    {
        CampaignStateManager.Instance.ApplyEncounterToScene();
        GameManager.Instance.RestartBattle();
        return;
    }

    if (SceneManager.GetActiveScene().name == SceneNames.MapScene)
        MapManager.Instance.RefreshForCurrentEncounter();
    else
        SceneManager.LoadScene(SceneNames.MapScene);
}
```

Under the debug `ProcessAllEncountersInBattleScene` flag (see "Debug: single-scene automation" below)
this is exactly the old in-place soft reload — re-runs `ApplyEncounterToScene()` in place (destroys/
replaces just the enemy avatar for the new `CurrentEncounter`), then `GameManager.RestartBattle()`
for the usual in-place battle reset (`OnBattleRestart` heals both heroes, clears creatures/shield/
rolls/XP, etc., exactly like the pause menu's Restart). Safe without new staleness machinery:
`ReplaceHeroAvatar`'s `Instantiate`/`Destroy` happen synchronously before `RestartBattle()` is even
called, so `G.EnemyHero` already resolves to the new `Hero` by the time `OnBattleRestart` fires. No
`CampaignStateManager.Instance`/`GameManager.Instance` null-guards here (rule 5) — being in
`BattleScene` under this flag guarantees both exist.

Otherwise (the real, non-debug path), every transition routes through `MapScene` first — `MapManager`
decides whether the new current encounter plays in place (a pick screen) or hands off to
`BattleScene` (a fight), so this method only ever needs to refresh `MapManager` in place (already in
`MapScene`) or load `MapScene` (arriving from `BattleScene` after a fight). `BattleScene` is now only
ever entered by `MapManager` itself, or by the debug-flag branch above — see "MapScene" below.

**Reintroduced the scene-identity branch** this doc used to describe as deliberately deferred YAGNI
scaffolding — that deferral held only "until a second scene actually exists," which is what
`MapScene` now is.

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
guaranteed present. The `CurrentFight != null` check that remains is real logic, not an existence
guard: it's `null` whenever the current `EncounterSO` isn't a `FightSO`, a legitimate state once
other encounter types exist.

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

- **Victory no longer grants anything.** `ResolveVictory()` used to add `FightSO.energyReward` to
  `RunState.currentEnergy` synchronously before the delay even started; that's gone entirely — a
  reward now only happens when the campaign designer places an explicit `RewardPickSO` in the
  `EncounterListSO`, claimed via `RewardEncounter` (see "Encounter/EncounterPlayer" below). Nothing
  in `RunState` changes on victory itself anymore.
- **`CompleteCurrentEncounter()`** is the shared "advance or finish the campaign" branch, extracted
  so `EncounterPlayer` completing a `LoadoutPickSO`/`RewardPickSO` reuses the exact same logic
  instead of calling `AdvanceToNextEncounter()` directly and hitting its "already at the last
  encounter" no-op warning when a pick screen happens to be the campaign's last entry.
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

## `Encounter`/`EncounterPlayer` — playing `LoadoutPickSO`/`RewardPickSO`

Mirrors `GameState`/`GameManager.Run(GameState)` (`docs/GameLoop.md`): `EncounterPlayer` is a
generic runner that doesn't know or care what any individual `Encounter` does internally, only that
it `Play()`s and eventually fires `OnCompleted`. `Global/Campaign/Encounter.cs`:

```csharp
public abstract class Encounter : MonoBehaviour
{
    public event Action OnCompleted;
    public abstract void Play(EncounterSO data);
    protected void Complete() => OnCompleted?.Invoke();
}
```

`LoadoutEncounter`/`RewardEncounter` each own their own presentation entirely — `EncounterPlayer`
itself has no `messageText`/`claimButton` fields of its own anymore:

```csharp
public class LoadoutEncounter : Encounter
{
    [SerializeField] private TMP_Text messageText;
    public override void Play(EncounterSO data) => messageText.text = ((LoadoutPickSO)data).briefingText;
    void Update() { if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Complete(); }
}

public class RewardEncounter : Encounter
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button claimButton;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private RewardCard cardPrefab;
    private RewardPickSO _data;
    private RewardCard _selectedCard;

    void Start() => claimButton.onClick.AddListener(HandleClaimClicked);

    public override void Play(EncounterSO data)
    {
        _data = (RewardPickSO)data;
        var run = CampaignStateManager.Instance.CurrentRun;

        run.currentEnergy += _data.energyReward; // uncapped — see docs/Campaign.md's RunState shape
        messageText.text = $"You got {_data.energyReward} energy!";
        CampaignStateManager.Instance.Save();

        foreach (var reward in RewardDrawer.DrawThree(G.RewardList, run))
        {
            var card = Instantiate(cardPrefab, cardContainer);
            card.Init(reward);
            card.OnClicked += HandleCardClicked;
        }
        claimButton.interactable = false;
    }

    private void HandleCardClicked(RewardCard card)
    {
        if (_selectedCard != null) _selectedCard.SetSelected(false);
        _selectedCard = card;
        _selectedCard.SetSelected(true);
        claimButton.interactable = true;
    }

    private void HandleClaimClicked()
    {
        if (_selectedCard == null) return;
        var run = CampaignStateManager.Instance.CurrentRun;
        _selectedCard.Data.Claim(run);
        CampaignStateManager.Instance.Save();
        Complete();
    }
}
```

- **`LoadoutEncounter` completes on a mouse click anywhere** — a click, not `Keyboard.current.anyKey`
  as an earlier version had it.
- **`RewardEncounter` completes *only* via the Claim button** — deliberately not click-anywhere, so a
  stray click elsewhere on screen can't grant a reward early. The guaranteed energy reward is applied
  and saved immediately in `Play()`; the Claim button (disabled until a card is selected) finalizes
  whichever `RewardSO` card was picked via `RewardDrawer` — see `docs/Rewards.md` for the full
  card hierarchy/draw algorithm. `HandleClaimClicked` calls the selected card's `RewardSO.Claim(run)`
  and `CampaignStateManager.Instance.Save()` (a real "end of encounter" state change — see
  `docs/Campaign.md`'s Save system section) before calling `Complete()`. Reads/writes through
  `CampaignStateManager` directly specifically so this works unchanged whether it's instantiated by
  `EncounterPlayer` in `BattleScene` (under the debug flag) or by `MapManager` in `MapScene` (the
  normal path) — both `CampaignStateManager` and `CampaignManager` are cross-scene-persistent, so
  either is reachable from both scenes.

`Global/Campaign/EncounterPlayer.cs` is the generic dispatcher, fully self-driving — no other script
calls into it, and it makes no new call sites in `CampaignManager`. It stays `BattleScene`-only
today; `MapManager` is `MapManager`'s own equivalent dispatcher for `MapScene` (see "`MapEncounterPoint`
and `MapManager`" above) — the two don't share code, since their orchestration/timing needs differ
(restart-triggered refresh vs. a staged reveal sequence):

```csharp
void Start()
{
    GameManager.OnBattleRestart += RefreshForCurrentEncounter;
    RefreshForCurrentEncounter();
}

public void RefreshForCurrentEncounter()
{
    DestroyActiveEncounter();
    var encounterSO = CampaignManager.Instance.CurrentEncounter;
    if (encounterSO == null || encounterSO.EncounterPrefab == null) return; // FightSO, or an unassigned list slot

    _activeEncounter = Instantiate(encounterSO.EncounterPrefab, encounterParent);
    _activeEncounter.OnCompleted += HandleEncounterCompleted;
    _activeEncounter.Play(encounterSO);
}

private void HandleEncounterCompleted()
{
    DestroyActiveEncounter();
    CampaignManager.Instance.CompleteCurrentEncounter();
}
```

- **Reads `CurrentEncounter` in its own `Start()`** — safe purely from the universal
  Awake-before-Start guarantee, the same reasoning `Hero.GetMaxHealth()` already relies on for
  `CampaignStateManager`. No new Script Execution Order needed.
- **Re-derives itself on every `GameManager.OnBattleRestart`** — the event every soft encounter
  transition already fires via `RestartBattle()` (`docs/GameLoop.md`), so `EncounterPlayer` is just
  one more independent subscriber in the project's canonical "one broadcaster, many independent
  subscribers" pattern (CLAUDE.md rule 3/16), not a new coordinator call site.
- **`encounterSO == null` is tolerated, not thrown** — an unassigned slot in `EncounterListSO` (e.g.
  mid-edit while hand-arranging the campaign) is normal content-authoring state, not a wiring bug, so
  it's treated the same as a `FightSO`'s unset `EncounterPrefab`: nothing to play here.
- **No `Instance` null-guards** on `CampaignManager.Instance`/`CampaignStateManager.Instance`
  (rule 5) — both guaranteed present wherever `EncounterPlayer` runs, inside `BattleScene`.

**Why the round loop can't silently progress during a pick screen — `RollStateManager`'s
`FightSO` gate.** A full-screen raycast-blocking overlay alone isn't enough:
`SlotMachine.Update()` reads `Keyboard.current.spaceKey` directly to start/stop the reel,
completely bypassing UI raycasts, and `RollState.OnEnter()` unconditionally activates the player's
`SlotMachine` regardless of encounter type. So `RollStateManager.ActivateSlotMachine()` itself now
checks `CampaignManager.Instance.CurrentEncounter is not FightSO` and returns immediately
if so — since Unity never runs `Update()` on an inactive GameObject, this makes the slot machine
subtree genuinely inert (not just visually hidden) outside a fight, so `RollState` waits forever
and `GameManager`'s round loop can never move past it. See `docs/SlotMachine.md`. This is the one
change to existing non-Campaign gameplay code this system needed — `GameManager.cs` itself is
completely untouched (no gating of `RunGameLoop()`, no `Time.timeScale` tricks): it still starts
the round loop unconditionally on every encounter, it just can't go anywhere while blocked.

**Known accepted gap: `CampaignStateManager.CurrentFight`/the enemy avatar go stale during a pick
screen.** `ApplyEncounterToScene()` already early-returns for non-`FightSO` encounters, so while a
`LoadoutPickSO`/`RewardPickSO` is up, `CurrentFight` and the enemy avatar still point at the
previous real fight. Harmless — nothing meaningfully reads either during a pick screen, and both
self-correct the moment the next `FightSO` loads — but worth knowing if `CurrentFight` ever looks
"wrong" mid pick-screen in the Inspector.

**Known accepted gap: no instant-resolve path for pick screens (CLAUDE.md rule 7).** Unlike battle
resolution, `LoadoutPickSO`/`RewardPickSO` block on real user input (a key press, a button click)
with no headless equivalent — deliberately not built, since they're interstitial UI with no
simulation-relevant math to short-circuit. `MapScene`'s reveal sequence has the same gap for the
same reason. The automation consumer that does exist now uses
`CampaignManager.ProcessAllEncountersInBattleScene` instead (see "Debug: single-scene
automation" above) rather than an instant-resolve method on either `EncounterPlayer` or `MapManager`.
Verified live: this flag already fully supports non-fight encounters too — `LoadoutPickSO`/
`RewardPickSO` play in-place via `EncounterPlayer` inside `BattleScene`, never touching `MapScene`.

## Debug-only surface: `CampaignManager.Debug.cs`

Per CLAUDE.md rule 20, `SetSessionEncounterOverride`/`SetSessionEncounterIndexOverride` — called by
nothing except `CampaignProgressTool`/`CampaignDebugTool` — live in a separate
`CampaignManager.Debug.cs` partial class file instead of the main
`CampaignManager.cs`, the same partial-class split pattern
`AttacksResolver.cs`/`AttacksResolver.Mechanics.cs` already uses for a different reason. The
`ProcessAllEncountersInBattleScene` flag lives there too. The `_overrideWindowOpen` field and the
actual guarded `RunState` write instead live on `CampaignStateManager.Debug.cs` (see
`docs/Campaign.md`), since they mutate/gate `RunState` directly —
`SetSessionEncounterIndexOverride(int)` here clamps against this object's own `EncounterList`, then
calls `CampaignStateManager.Instance.ApplySessionEncounterIndexOverride(clampedIndex)` to actually
apply it.

**Why `SetSessionEncounterIndexOverride(int)` exists alongside `SetSessionEncounterOverride(EncounterSO)`**:
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
`EncounterListSO` reference to resolve an index into an `EncounterSO` the way `CampaignProgressTool`
does, so the index-based overload exists specifically for that caller too — same `_overrideWindowOpen`
gating (on `CampaignStateManager`) and `Mathf.Clamp` bounds-safety (on `CampaignManager`, against its
own `EncounterList`) as the `EncounterSO` overload (which just resolves its argument to an index and
forwards to this one).

## `CampaignProgressTool`

Editor-only, lives on `Global/CampaignProgressTool` next to `CampaignDebugTool`/`BalanceTool` in
`BattleScene.unity`. One field pair: `overrideEncounter` (bool) + `encounter` (`EncounterSO`, plain
object-reference field — same pattern `CampaignDebugTool` uses for its archer/tank/mage/nuke/spell
overrides, not a custom named dropdown). Applies in `Awake()`, calling
`CampaignManager.Instance.SetSessionEncounterOverride(encounter)`, which resolves the
picked asset to its index in `encounterList.encounters` — never persisted, mirrors
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
would silently override any real navigation: e.g. override to Battle1, click "Advance To Next
Encounter" (which correctly increments and saves index 1) — but the ensuing scene load would fire
`CampaignProgressTool.Awake()` again, snapping the index right back to Battle1's, while the *saved*
`RunState.currentEncounterIndex` kept climbing in the background. `CampaignStateManager`'s
`_overrideWindowOpen` flag closes this off: it's `true` only during the `Awake()` phase of the one
scene load where `CampaignStateManager` itself was first created (closed in its own `Start()`,
which — being `DontDestroyOnLoad` — only ever runs once per session). Any later
`SetSessionEncounterOverride` call, from a later reload's fresh `CampaignProgressTool.Awake()`, is a
no-op with a warning. In practice, since navigation is now a soft in-place reload (see above) rather
than a scene reload, `CampaignProgressTool.Awake()` mostly won't even fire again after the first
load — the window flag is defense-in-depth for the cases that do still reload the scene (arriving
from elsewhere, or the override window itself).

Its custom Editor draws three buttons — "Start New Run", "Reset Current Encounter", "Advance To
Next Encounter" — disabled outside Play mode, calling the exact same `CampaignManager`
methods real gameplay UI will use later. No separate debug-only logic exists.

## `CampaignDebugTool` — encounter override removed

`CampaignDebugTool` previously had a speculative `overrideEncounterIndex`/`currentEncounterIndex`
pair (writing `RunState.currentEncounterIndex` directly, never consumed by anything). Removed —
`CampaignProgressTool`'s encounter-asset override supersedes it with a proper `EncounterSO`-level
pick instead of a bare index, and keeping both would let two debug tools fight over the same field.
Per `CampaignDebugTool`'s own documented gotcha (see `docs/Campaign.md`), the component was removed
and re-added on its scene GameObject after the field-layout change rather than trusting old
serialized values.

## `CampaignManagerEditor` — Inspector visibility

Per CLAUDE.md rule 19 ("surface important runtime state in the Inspector"), `CampaignManager`
gets a custom Editor (`Global/Campaign/Editor/CampaignManagerEditor.cs`) showing, read-only
and live-updating during Play mode: the current encounter index, the resolved `EncounterSO` object
reference, and (if it's a `FightSO`) its `fightId` string. Selecting the `CampaignProgress`
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
  any prefab instance override. `Global/CampaignProgressTool` (`CampaignProgressTool` component)
  stays `BattleScene`-only — no equivalent tooling exists in `MapScene` yet.
- `CampaignDebugTool` (root-level GameObject, `CampaignDebugTool` component) is likewise placed in
  **both** scenes, same `DontDestroyOnLoad` duplicate-guard pattern as `CampaignProgress` — needed so
  its overrides apply regardless of which scene the session actually boots from. Unlike
  `CampaignProgress`, it is **not** a shared prefab — each placed copy's fields are independently
  serialized, so only whichever one wins the duplicate-guard race actually has its fields read;
  configure overrides on the copy in the scene you're actually about to press Play from. This was
  the source of a real, live-caught bug (CLAUDE.md rule 26): the `BattleScene` copy was once nested
  under `Global` instead of root, silently breaking its `DontDestroyOnLoad` and letting a stale
  override re-apply on later scene transitions.
- `Global/EncounterPlayer` (`EncounterPlayer` component, `encounterParent` optionally wired to a
  scene parent transform — left `None` is fine) stays in `BattleScene.unity`, used only under the
  `ProcessAllEncountersInBattleScene` debug flag (it no-ops for a `FightSO`, which is the only thing
  `CurrentEncounter` is ever allowed to be in `BattleScene` outside that flag). `MapScene.unity` gets
  its own `MapManager` GameObject (`MapManager` component, `encounterParent` similarly optional) —
  see "`MapEncounterPoint` and `MapManager`" above.
- `_Prefabs/Campaign/LoadoutEncounter.prefab` (`Canvas` + tint `Image` + `TMP_Text`,
  `LoadoutEncounter` component) and `_Prefabs/Campaign/RewardEncounter.prefab` (same, plus a Claim
  `Button`, three `cardSlots` anchors (`CardSlot1`/`CardSlot2`/`CardSlot3`, each holding a disabled
  placeholder card — CLAUDE.md's anchor+disabled-template rule) and `cardPrefab`
  (`_Prefabs/UI/Cards/RewardCard.prefab`) for the 3 drawn reward cards — see `docs/Rewards.md`,
  `RewardEncounter` component) — each is a fully self-contained `Canvas` (own `CanvasScaler`/
  `GraphicRaycaster`, `sortingOrder 5`), instantiated fresh by whichever dispatcher is active
  (`EncounterPlayer` or `MapManager`) and destroyed on completion, never left placed in a scene.
- `LoadoutPickSO`/`RewardPickSO` assets need their `EncounterPrefab` field wired to the matching
  prefab above (`FightSO` assets leave it unset).
- `_Prefabs/Map/MapEncounterPoint.prefab` needs a `MapEncounterPoint` component, its
  `futureEncounterVisual`/`currentEncounterVisual`/`completeEncounterVisual` fields wired to the
  prefab's existing named children, an `MMPositionShaker` on the root, an `ArriveFightFeedback` child
  with an `MMF_Player` (an `MMF_PositionShake` feedback targeting that `MMPositionShaker`), and an
  `ArriveRegularFeedback` child with an `MMF_Player` (an `MMF_Scale` feedback, `AnimateScaleTarget` set
  to `CurrentEncounterVisual`'s transform — its default punch-shaped curve already reads as a yoyo pop).
- `MapScene.unity`'s `MapManager` GameObject's `points` array needs one entry per placed
  `MapEncounterPoint` instance, **in the same order as `EncounterListSO.encounters`** —
  `AssignEncounters()` maps them positionally, not by any per-instance reference, so array order is the
  only thing that determines which point represents which encounter. Adding/removing/reordering
  encounters in the list means updating this array to match.
- Script Execution Order: `CampaignManager` = `-150`, `CampaignStateManager` = `-100`. No new SEO
  entries needed for `MapManager` — it only reads `CampaignManager` from its own `Start()`, never
  `Awake()`, so the universal Awake-before-Start guarantee already covers it.
- `MapScene.unity` needs its own root-level `EventSystem` GameObject (`EventSystem` +
  `InputSystemUIInputModule`, same Input Actions asset as `BattleScene`'s) — see Gotchas below.

## Gotchas

- **Every scene with clickable UGUI needs its own `EventSystem` GameObject — it doesn't carry over
  between scenes.** Caught live: `MapScene`'s `LoadoutEncounter`/`RewardEncounter` panels have a real
  `Canvas`/`GraphicRaycaster`/`Button` (see above), but with no `EventSystem` anywhere in `MapScene`,
  `EventSystem.current` was `null` and clicks were never routed to the Button at all — the panel looked
  interactive but silently ignored every click. `BattleScene` already has one; `MapScene` didn't, since
  it never needed clickable UI before pick screens moved there. Fixed by adding a root-level
  `EventSystem` GameObject to `MapScene.unity` (`EventSystem` + `InputSystemUIInputModule`, CLAUDE.md
  rule 8 — new Input System, not the legacy `StandaloneInputModule`) mirroring `BattleScene`'s.
- **`CampaignProgress` and `CampaignDebugTool` must both stay root-level GameObjects, in every scene
  they're placed in.** `DontDestroyOnLoad` only works on scene roots — parenting either under `Global`
  (where `CampaignProgressTool` correctly lives, since that one doesn't need to persist) would silently
  fail to persist it across scene reloads.
- **`CampaignManagerEditor`/`MapManagerEditor` must guard `!Application.isPlaying` before
  reading anything that touches `RunState`.** `RunState` is only populated by `CampaignStateManager`'s
  `Awake()`, which never runs outside Play mode — a custom Editor that reads
  `manager.CurrentEncounterIndex`/`CurrentEncounter` unconditionally throws a
  `NullReferenceException` on every Inspector repaint the moment `CampaignProgress` is selected in
  Edit mode. Caught live: this reproduced immediately once a second `CampaignProgress` object existed
  in `MapScene` (routinely selected during its own setup) — the exact same latent bug already existed
  for `BattleScene`'s copy, it just had never been selected in Edit mode before.
- **`AdvanceToNextEncounter()` itself still just logs a warning and no-ops if called at the last
  encounter** — but `ResolveVictory()` never calls it in that case, it calls `CompleteCampaign()`
  instead (see "Fight results" above). The no-op path only fires if something calls
  `AdvanceToNextEncounter()` directly at the last encounter (e.g. mashing
  `CampaignProgressTool`'s button) — that's an intentionally inert dead-end, not a bug.
- **`fightId`/`isTutorial` on `FightSO` are still inert** — no logic reads them yet, forward-
  looking data for a future pre-fight phase.
- **A left-over saved `RunState` can look like "the wrong default encounter loads."** Progress is
  designed to persist across Editor Play sessions (the whole point of `RunState`/the save system) — if
  `currentEncounterIndex` was previously advanced (including via the override-leak bug described
  above, before it was fixed) and never reset, a fresh Play without an override will correctly
  resume from that saved index, not restart at encounter 0. Use `CampaignProgressTool`'s "Start New
  Run" or `CampaignDebugTool`'s "Clear Saved Run" button to get back to a genuinely fresh state.

## `MapScene`

`Assets/Game/_Scenes/MapScene.unity` sits between battles — the pre-battle loadout and post-battle
reward pick screens now actually play there (still the same `LoadoutEncounter`/`RewardEncounter`
prefabs, same tint/message/Claim button — this system doesn't change their presentation, only where
they're shown), and it's the scene that visually shows campaign progress as points on a path. Campaign
navigation still isn't player-driven choice yet — `EncounterListSO` stays a flat linear list, walked
in order; `MapScene` visualizes that order rather than letting the player pick a branch.
`CampaignManager`/`CampaignStateManager` both work from it exactly as designed for (they're
cross-scene by construction — see "Two cross-scene-persistent managers" above): the same root-level
`CampaignProgress` GameObject (same components, same `encounterList`/`catalog`/`rewardList`
references) is placed in `MapScene.unity` too, so either scene can be the session's first-loaded
scene under the existing duplicate-guard singleton pattern.

### `MapEncounterPoint` and `MapManager`

- **`Global/Map/MapEncounterPoint.cs`** — one encounter's point on the map, three visual states
  (`futureEncounterVisual`/`currentEncounterVisual`/`completeEncounterVisual`, exactly one active at a
  time — `SetFuture()`/`SetCurrent()`/`SetComplete()`, visuals only, no path side effects). Path
  visibility is separate and explicit: `HidePaths()`/`ShowPathIn()`/`ShowPathOut()`, each null-checked
  (`pathIn`/`pathOut` are optional `SplineContainer` references — today just toggled active/inactive,
  no actual spline-draw-in animation yet; see the `MapManager` TODO below). Two feedbacks, both
  `MMF_Player`s per CLAUDE.md rule 15: `arriveFightFeedback` (an `MMF_PositionShake` + sibling
  `MMPositionShaker`, played via `PlayArriveFightFeedback()` when the point becomes current *and* it's
  a fight) and `arriveRegularFeedback` (an `MMF_Scale` punch, played via `PlayArriveRegularFeedback()`
  when the point becomes current and it's *not* a fight — a distinct "yoyo" pop instead of the fight's shake). Which
  `EncounterSO` a point represents (`EncounterSO` getter) is **not** wired per-instance in the
  Inspector anymore — `SetEncounter(...)` is called once by `MapManager.AssignEncounters()` (see
  below), positionally against `CampaignManager.EncounterList`. `encounterSO` itself stays
  `[SerializeField]` even though nothing authors it directly (CLAUDE.md rule 19 — load-bearing runtime
  state stays visible in the Inspector by default, not hidden behind a debugger); any value seen on it
  in Edit mode is stale and gets overwritten the instant Play mode starts.
- **`Global/Map/MapManager.cs`** — `MapScene`'s orchestrator, singleton (`Instance`). Holds a
  serialized `MapEncounterPoint[] points` — one entry per encounter in `EncounterList`, same order;
  `AssignEncounters()` (called once from `Start()`, before the first `RefreshForCurrentEncounter()`)
  walks `EncounterList.encounters` and calls `points[i].SetEncounter(encounters[i])` for each index —
  **throws** if `points.Length < encounters.Count` (a scene-setup/content-authoring mismatch that
  should surface immediately, not fail silently or wrap around). Purely driven by explicit calls
  (`Start()`, and `CampaignManager.LoadCurrentEncounter()`'s direct call when already in
  `MapScene`) — there is no `GameManager`/`OnBattleRestart` in `MapScene` to subscribe to.
  `RefreshForCurrentEncounter()` is a gradual, step-by-step reveal — every step visible, nothing jumps
  straight to its end state:
  1. Every point's paths are hidden first — a clean slate, since a point's leftover path state from a
     previous refresh isn't trustworthy (e.g. a debug override jumping the index backward).
  2. Every already-passed point (`index < CurrentEncounterIndex`) snaps to `SetComplete()` with both
     paths shown — old history, nothing new to animate about it. (This is also where the point that was
     current a moment ago — the "previous" point — visually flips to Complete; there's nothing special
     about it once its index is behind the new current one.)
  3. The current point's `PathIn` is revealed, then a pause (`revealDelaySeconds`) — "the player has
     walked up to it." If `CurrentEncounterIndex` is `0` there's no earlier point at all, so step 2 is a
     no-op and this is the only path shown.
  4. `RevealCurrentPoint()`: the current point flips `SetFuture() -> SetCurrent()`, playing
     `PlayArriveFightFeedback()` (shake) if `CurrentEncounter is FightSO`, else
     `PlayArriveRegularFeedback()` (yoyo) — then another pause (`arriveFeedbackDuration`).
  5. `StartCurrentEncounter()`: hands off to `BattleScene` for a fight
     (`SceneManager.LoadScene(SceneNames.BattleScene)`), or instantiates/plays the pick screen prefab in
     place (`LoadoutPickSO`/`RewardPickSO` — same dispatch shape as `EncounterPlayer`: instantiate
     `EncounterPrefab`, wait for `OnCompleted`, call `CampaignManager.CompleteCurrentEncounter()`).

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

### Debug: single-scene automation

`CampaignManager.ProcessAllEncountersInBattleScene` (`CampaignManager.Debug.cs`, a
plain serialized bool, CLAUDE.md rule 20) keeps the entire campaign in `BattleScene` exactly like
before `MapScene` existed — `MapScene` never loads. This is the CLAUDE.md rule 7 escape hatch:
`MapScene`'s reveal sequence has no instant-resolve equivalent (same accepted gap as the pick
screens themselves, see below), so headless balance-testing automation toggles this flag instead.
`CampaignStateManager`'s `EnterBattleScene()` also has a boot-time/every-load guard for the flag
being off: if the current scene is `BattleScene` but `CurrentEncounter` isn't a `FightSO` (e.g. a
developer pressed Play directly on `BattleScene.unity` while the saved encounter index points at a
pick screen), it immediately `SceneManager.LoadScene(SceneNames.MapScene)`s instead of applying
anything. Verified live: with the flag on, a `RewardPickSO`/`LoadoutPickSO` encounter correctly
spawns its pick screen in place inside `BattleScene` via `EncounterPlayer`, exactly like a real fight
would run the round loop in place — `MapScene` never loads for either encounter type.

### `SceneNames`

`Global/Campaign/SceneNames.cs` — `BattleScene`/`MapScene` string constants for every
`SceneManager.LoadScene` call site above.

## Related docs

- `docs/Campaign.md` — `RunState`/`CampaignStateManager`/`GameCatalog`, the data layer this system
  builds the actual sequence on top of, and the Save system section (storage abstraction, versioning,
  save trigger points).
- `docs/GameLoop.md` — `GameManager.RestartBattle()`/`OnBattleRestart`, what
  `CampaignManager.ResetCurrentEncounter()` delegates to and what `EncounterPlayer`
  subscribes to.
- `docs/SlotMachine.md` — `RollStateManager.ActivateSlotMachine()`'s `FightSO` gate, the fix that
  makes the round loop genuinely inert (not just visually hidden) during a pick screen.
- `docs/G.md` — `G.EnemyView`/`G.EnemyHero`/`G.EncounterList`, read by
  `CampaignStateManager.ApplyEncounterToScene()` and `Hero.GetMaxHealth()`.
- `docs/Rewards.md` — the reward-card pick `RewardEncounter` now offers alongside its guaranteed
  energy reward: `RewardSO` hierarchy, `RewardListSO`, `RewardDrawer`'s draw algorithm,
  `RewardBonuses`' resolver-side stat-boost hook.
