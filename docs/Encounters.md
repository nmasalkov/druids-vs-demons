# Encounters & Campaign Progress

## What this system does

Builds the actual **battle sequence** on top of `docs/Campaign.md`'s data layer:
`EncounterListSO` is the ordered campaign (currently three `BattleSO` battles), each battle carries
its own enemy avatar/HP/reward, and `CampaignProgressManager` is the real API — not debug-only
logic — for starting a run, restarting the current encounter, and advancing to the next one. Every
battle's own pre-battle/post-battle phases and reward consumption are still unbuilt follow-ups; this
only wires up the sequencing skeleton so those can land later without re-architecting.

## Key files

- `Global/Campaign/EncounterSO.cs` — abstract marker base for anything that can appear in an
  `EncounterListSO`. No fields; exists purely so future encounter types (shops, events, ...) can
  share the same list without changing `CampaignProgressManager`.
- `Global/Campaign/BattleSO.cs` — concrete `EncounterSO`. `battleId` (string), `isTutorial` (bool),
  `enemyData` (`EnemyData`), `energyReward` (int). `battleId`/`isTutorial`/`energyReward` aren't
  consumed by any logic yet — forward-looking data for the pre/post-battle phases this pass doesn't
  build, same as `RunState.currentEncounterIndex` was before this system existed.
- `EnemyData` (nested `[Serializable] struct` in `BattleSO.cs`) — `enemyAvatarPrefab` (`GameObject`),
  `hp` (int). Expandable later (e.g. an AI tactic enum) without touching `BattleSO` itself.
- `Global/Campaign/EncounterListSO.cs` — `List<EncounterSO> encounters`, the ordered campaign
  sequence, indexed by `CampaignProgressManager.CurrentEncounterIndex`.
- `Global/Campaign/CampaignProgressManager.cs` — the navigation API (below), pure game logic.
- `Global/Campaign/CampaignProgressManager.Debug.cs` — `partial class` split of the same type
  holding only the debug-only `SetSessionEncounterOverride` surface (CLAUDE.md rule 20).
- `Global/Campaign/Editor/CampaignProgressManagerEditor.cs` — read-only Inspector view of the
  current encounter index/asset (CLAUDE.md rule 19).
- `Global/Campaign/CampaignProgressTool.cs` + `Global/Campaign/Editor/CampaignProgressToolEditor.cs`
  — Editor-only debug tool mirroring `CampaignDebugTool`'s pattern.
- `Global/Campaign/SceneNames.cs` — `static class` holding scene name constants (currently just
  `BattleScene`), so scene-name strings aren't inlined at each call site.
- `PlayerView/HeroView.cs` — gained `ReplaceHeroAvatar(GameObject avatarPrefab)`.
- `Global/Campaign/CampaignManager.cs` — gained `CurrentBattle`, `ResetRun()`, the static
  `LoadOrCreateRunState()` helper, and `ApplyEncounterToScene()` (called from `Start()`).
- `Units/Hero.cs` — `GetMaxHealth()` gained a symmetric enemy-side branch.
- `UI/PauseMenuController.cs` — `HandleRestartClicked()` now calls
  `CampaignProgressManager.Instance.ResetCurrentEncounter()` instead of calling
  `GameManager.RestartBattle()` directly.

Assets: `_ScriptableObjects/Campaign/Encounters/EncounterList.asset` +
`Battle1.asset`/`Battle2.asset`/`Battle3.asset`, referencing
`_Prefabs/Characters/Units/Avatars/Enemies/EnemyAvater.prefab` / `EnemyAvater 1.prefab` /
`EnemyAvater 2.prefab` at 10/20/30 HP and 40/60/80 energy reward respectively. Those three prefabs
were previously orphaned, unused clones of `Player.prefab` (identical `Hero`/`HeroAnimator`/
`CastOrigin` etc. wiring) — this system is what finally puts them to use.

## Ownership split: `CampaignProgressManager` vs `CampaignManager`

- **`CampaignProgressManager`** owns *where you are in the campaign* (current encounter index/
  asset) and the three navigation actions (`StartNewRun`, `ResetCurrentEncounter`,
  `AdvanceToNextEncounter`). It is the only thing that knows how to move forward — both
  `CampaignProgressTool` and (later) real gameplay UI call into it, never duplicate its logic. This
  is what makes it a real API rather than debug-only tooling.
- **`CampaignManager`** stays owner of *the run's state* (`RunState`) and *applying it to the
  current scene*, now including the current encounter's enemy data on top of what it already
  applies for player loadout/energy (see `docs/Campaign.md`).
- `AdvanceToNextEncounter()`/`StartNewRun()` write the new index through to
  `CampaignManager.CurrentRun` + `Save()`, then call the shared `LoadCurrentEncounter()` (see "Soft
  reload vs. scene load" below) instead of unconditionally reloading the scene.
  `ResetCurrentEncounter()` never touches the index at all — the current encounter's enemy avatar/
  HP are already correct, so it just delegates to the existing in-place
  `GameManager.RestartBattle()` machinery (`OnBattleRestart` → `Hero.InitHealth()`, which now reads
  the encounter-aware `GetMaxHealth()`).

```csharp
// CampaignProgressManager.cs — pure game logic, no defensive Instance null-checks (CLAUDE.md
// rule 5: CampaignManager/GameManager are guaranteed to co-exist wherever this runs).
public partial class CampaignProgressManager : MonoBehaviour
{
    public static CampaignProgressManager Instance { get; private set; }
    [SerializeField] private EncounterListSO encounterList;
    private int _currentEncounterIndex;

    public EncounterSO CurrentEncounter => encounterList.encounters[
        Mathf.Clamp(_currentEncounterIndex, 0, encounterList.encounters.Count - 1)];
    public bool HasNextEncounter => _currentEncounterIndex < encounterList.encounters.Count - 1;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _currentEncounterIndex = CampaignManager.LoadOrCreateRunState().currentEncounterIndex;
        _overrideWindowOpen = true; // field lives in CampaignProgressManager.Debug.cs
    }

    void Start() => _overrideWindowOpen = false;

    public void StartNewRun() { /* index = 0, CampaignManager.Instance.ResetRun(), LoadCurrentEncounter() */ }
    public void ResetCurrentEncounter() { GameManager.Instance.RestartBattle(); }
    public void AdvanceToNextEncounter() { /* index++, save through CampaignManager.Instance, LoadCurrentEncounter() */ }

    private void LoadCurrentEncounter() { /* soft reload if already in BattleScene, else SceneManager.LoadScene */ }
}
```

`SetSessionEncounterOverride` and the `_overrideWindowOpen` field it uses live in
`CampaignProgressManager.Debug.cs` instead — see "Debug-only surface" below.

## The first cross-scene-persistent object in the codebase

Every other singleton (`G`, `GameManager`, `CampaignManager`, `EnergyController`,
`RollStateManager`) lives on the per-scene `Global` GameObject and is recreated on every scene load
— `docs/Campaign.md` explicitly flagged this as a gap ("Not `DontDestroyOnLoad`... revisit once a
second scene exists"). `CampaignProgressManager` is that revisit: it's placed once as a **root-level**
GameObject named `CampaignProgress` in `BattleScene.unity` (must stay root-level —
`DontDestroyOnLoad` only works on scene roots) and uses the standard Unity duplicate-guard singleton
pattern:

```csharp
void Awake()
{
    if (Instance != null) { Destroy(gameObject); return; }
    Instance = this;
    DontDestroyOnLoad(gameObject);
    ...
}
```

On the first scene load its `Awake()`/`Start()` run once and it survives forever after (within the
session). On every later `BattleScene` reload (triggered by `AdvanceToNextEncounter`/`StartNewRun`),
the scene file's own placed copy of `CampaignProgress` is instantiated fresh, sees `Instance != null`,
and self-destructs immediately — the original, persistent instance keeps its in-memory
`_currentEncounterIndex` untouched by the reload.

**Why it bootstraps its own index from PlayerPrefs instead of trusting `CampaignManager` live**:
because its own `Start()` only ever runs once per session, it can't rely on querying
`CampaignManager.Instance` fresh at arbitrary later points — and per the planned future map scene
(see below), `CampaignManager` (battle-scoped) may not even be loaded when `CampaignProgressManager`
needs to answer "what's the current encounter." Bootstrapping via the same static
`CampaignManager.LoadOrCreateRunState()` helper `CampaignManager.Awake()` itself uses keeps it
self-contained the same way `CampaignManager.Awake()` already is.

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

Called from `CampaignManager.ApplyEncounterToScene()` — see below — which runs from
`CampaignManager.Start()` (Script Execution Order `-100`, the earliest `Start()` in the scene).
**Ordering is safe without any new staleness machinery**: `Destroy()` on the old avatar happens
before its own `Start()` would otherwise fire (Unity skips `Start()` for objects destroyed earlier
in the same Awake/Start pass), so the old `Hero` never runs `InitHealth()`/subscribes to
`OnBattleRestart`. The newly instantiated `Hero`'s `Start()` runs later in the same frame, by which
point `CampaignManager.CurrentBattle` is already set, so its first `GetMaxHealth()` read is correct.

## `CampaignManager.ApplyEncounterToScene()`

```csharp
public BattleSO CurrentBattle { get; private set; }

void Start()
{
    ApplyLoadoutToG();
    EnergyController.Instance.ApplyCampaignEnergyCapacity(CurrentRun.energyCapacity);
    ApplyEncounterToScene();
}

public void ApplyEncounterToScene()
{
    if (CampaignProgressManager.Instance.CurrentEncounter is not BattleSO battle) return;
    CurrentBattle = battle;
    G.EnemyView.ReplaceHeroAvatar(battle.enemyData.enemyAvatarPrefab);
}
```

`public` (not `private`) specifically so `CampaignProgressManager.LoadCurrentEncounter()` can call
it again outside the Awake/Start pipeline — see "Soft reload vs. scene load" below. No
`CampaignProgressManager.Instance == null` guard (per CLAUDE.md rule 5) — `CampaignProgressManager`
is placed in `BattleScene.unity` alongside `CampaignManager` and is guaranteed present wherever this
runs; the `CurrentEncounter is not BattleSO` check that remains is genuine type logic (not every
`EncounterSO` is a battle), not a defensive existence check.

## Soft reload vs. scene load

`SceneManager.LoadScene` collapses/rebuilds the entire scene hierarchy — disruptive and unnecessary
when the player is already sitting in `BattleScene` and just wants to restart or move to the next
battle. `CampaignProgressManager.LoadCurrentEncounter()` (the shared tail end of `StartNewRun()`/
`AdvanceToNextEncounter()`) branches on where we currently are:

```csharp
private void LoadCurrentEncounter()
{
    if (SceneManager.GetActiveScene().name == SceneNames.BattleScene)
    {
        CampaignManager.Instance.ApplyEncounterToScene();
        GameManager.Instance.RestartBattle();
    }
    else
    {
        SceneManager.LoadScene(SceneNames.BattleScene);
    }
}
```

`SceneNames.BattleScene` is `Scene.name` terminology, matching Unity's own `SceneManager` API
(`GetActiveScene().name` / `LoadScene(string sceneName)`) — not an arbitrary id we invented. Kept in
a small dedicated `SceneNames` static class rather than inline in `CampaignProgressManager` so a
future `MapScene` constant has an obvious home and nothing needs to hunt through this class to find
scene-name strings.

- **Already in `BattleScene`** (the only case that exists today): re-run `ApplyEncounterToScene()`
  in place — destroys/replaces just the enemy avatar for the new `CurrentEncounter` — then
  `GameManager.RestartBattle()` for the usual in-place battle reset (`OnBattleRestart` heals both
  heroes, clears creatures/shield/rolls/XP, etc., exactly like the pause menu's Restart). No scene
  reload, no hierarchy collapse. Safe without new staleness machinery: `ReplaceHeroAvatar`'s
  `Instantiate`/`Destroy` happen synchronously before `RestartBattle()` is even called, so
  `G.EnemyHero` already resolves to the new `Hero` by the time `OnBattleRestart` fires; the new
  `Hero`'s own `Start()` (which calls `InitHealth()`) runs later that same frame, well before
  `GameStartState`'s 2-second intro delay would let any gameplay code read its HP. No
  `CampaignManager.Instance`/`GameManager.Instance` null-guards here (rule 5) — being in
  `BattleScene` at all guarantees both exist.
- **Arriving from elsewhere** (e.g. a future `MapScene`): falls back to
  `SceneManager.LoadScene(SceneNames.BattleScene)`, which runs the normal Awake/Start pipeline
  (`CampaignManager.Start()` → `ApplyEncounterToScene()`) from scratch.

## `Hero.GetMaxHealth()` — symmetric enemy branch

```csharp
protected override float GetMaxHealth()
{
    if (this == G.PlayerHero)
        return CampaignManager.Instance.CurrentRun.maxHp;
    if (this == G.EnemyHero && CampaignManager.Instance.CurrentBattle != null)
        return CampaignManager.Instance.CurrentBattle.enemyData.hp;
    return Data.health;
}
```

Keeps `Hero.cs`'s dependency surface unchanged — still only `CampaignManager`, no new direct
dependency on `CampaignProgressManager`. No `CampaignManager.Instance != null` guard (rule 5) — any
`Hero` only ever exists inside `BattleScene`, where `CampaignManager` is guaranteed present. The
`CurrentBattle != null` check that remains is real logic, not an existence guard: it's `null`
whenever the current `EncounterSO` isn't a `BattleSO`, a legitimate state once other encounter
types exist.

## Debug-only surface: `CampaignProgressManager.Debug.cs`

Per CLAUDE.md rule 20, `SetSessionEncounterOverride` — called by nothing except
`CampaignProgressTool` — lives in a separate `CampaignProgressManager.Debug.cs` partial class file
instead of the main `CampaignProgressManager.cs`, the same partial-class split pattern
`AttacksResolver.cs`/`AttacksResolver.Mechanics.cs` already uses for a different reason. The
`_overrideWindowOpen` field lives there too, since it exists solely in service of the override
mechanism. `CampaignProgressManager.cs`'s own `Awake()`/`Start()` still set it directly (partial
class members are shared across all the type's files) — the split only separates *what's debug-only*
from *what's real navigation logic*, not lifecycle ownership.

## `CampaignProgressTool`

Editor-only, lives on `Global/CampaignProgressTool` next to `CampaignDebugTool`/`BalanceTool` in
`BattleScene.unity`. One field pair: `overrideEncounter` (bool) + `encounter` (`EncounterSO`, plain
object-reference field — same pattern `CampaignDebugTool` uses for its archer/tank/mage/nuke/spell
overrides, not a custom named dropdown). Applies in `Awake()`, calling
`CampaignProgressManager.Instance.SetSessionEncounterOverride(encounter)`, which resolves the
picked asset to its index in `encounterList.encounters` — never persisted, mirrors
`CampaignDebugTool`'s overrides never calling `Save()`.

**Requires Script Execution Order `CampaignProgressManager` (`-150`) before `CampaignProgressTool`
(default order)** — the same class of Awake-vs-Awake ordering issue `docs/Campaign.md` already
documents between `CampaignManager` and `CampaignDebugTool` (two same-phase callbacks with no
default ordering guarantee). No SEO relationship is needed between `CampaignProgressTool` and
`CampaignManager` itself — `CampaignManager.Start()` reading the override is safe purely from the
universal Awake-phase-precedes-Start-phase guarantee, the same reasoning `Hero.Start()` already
relies on for `CampaignManager`.

**The override only takes effect once per session, even though `CampaignProgressTool.Awake()` fires
on every `BattleScene` load.** `CampaignProgressTool` is a plain scene-local component — its
serialized `overrideEncounter` checkbox stays checked across reloads, so its `Awake()` calls
`SetSessionEncounterOverride(encounter)` again every time the scene loads. Without a guard, that
would silently override any real navigation: e.g. override to Battle1, click "Advance To Next
Encounter" (which correctly increments and saves index 1) — but the ensuing scene load would fire
`CampaignProgressTool.Awake()` again, snapping the index right back to Battle1's, while the *saved*
`RunState.currentEncounterIndex` kept climbing in the background. `CampaignProgressManager`'s
`_overrideWindowOpen` flag closes this off: it's `true` only during the `Awake()` phase of the one
scene load where `CampaignProgressManager` itself was first created (closed in its own `Start()`,
which — being `DontDestroyOnLoad` — only ever runs once per session). Any later
`SetSessionEncounterOverride` call, from a later reload's fresh `CampaignProgressTool.Awake()`, is a
no-op with a warning. In practice, since navigation is now a soft in-place reload (see above) rather
than a scene reload, `CampaignProgressTool.Awake()` mostly won't even fire again after the first
load — the window flag is defense-in-depth for the cases that do still reload the scene (arriving
from elsewhere, or the override window itself).

Its custom Editor draws three buttons — "Start New Run", "Reset Current Encounter", "Advance To
Next Encounter" — disabled outside Play mode, calling the exact same `CampaignProgressManager`
methods real gameplay UI will use later. No separate debug-only logic exists.

## `CampaignDebugTool` — encounter override removed

`CampaignDebugTool` previously had a speculative `overrideEncounterIndex`/`currentEncounterIndex`
pair (writing `RunState.currentEncounterIndex` directly, never consumed by anything). Removed —
`CampaignProgressTool`'s encounter-asset override supersedes it with a proper `EncounterSO`-level
pick instead of a bare index, and keeping both would let two debug tools fight over the same field.
Per `CampaignDebugTool`'s own documented gotcha (see `docs/Campaign.md`), the component was removed
and re-added on its scene GameObject after the field-layout change rather than trusting old
serialized values.

## `CampaignProgressManagerEditor` — Inspector visibility

Per CLAUDE.md rule 19 ("surface important runtime state in the Inspector"), `CampaignProgressManager`
gets a custom Editor (`Global/Campaign/Editor/CampaignProgressManagerEditor.cs`) showing, read-only
and live-updating during Play mode: the current encounter index, the resolved `EncounterSO` object
reference, and (if it's a `BattleSO`) its `battleId` string. Selecting the `CampaignProgress`
GameObject during Play always shows what encounter you're actually on — no debugger needed. Guards
against an unassigned/empty `encounterList` with a help box instead of throwing.

## Editor setup

- `Assets/Game/_Scenes/BattleScene.unity` is registered in Build Settings' Scenes In Build —
  required for `SceneManager.LoadScene(string)` to work at runtime, including in-Editor Play mode.
- `CampaignProgress` (root-level GameObject, `CampaignProgressManager` component, `encounterList`
  wired to `EncounterList.asset`) and `Global/CampaignProgressTool` (`CampaignProgressTool`
  component) both live in `BattleScene.unity`.
- Script Execution Order: `CampaignProgressManager` = `-150`.

## Gotchas

- **`CampaignProgress` must stay a root-level GameObject.** `DontDestroyOnLoad` only works on scene
  roots — parenting it under `Global` (where `CampaignProgressTool` correctly lives, since that one
  doesn't need to persist) would silently fail to persist it across scene reloads.
- **`AdvanceToNextEncounter()` doesn't wrap or stop cleanly at the end of the list** — it just logs
  a warning and no-ops if already on the last encounter. There's no "campaign complete" state yet;
  that's part of the pre/post-battle phase work this pass doesn't build.
- **`energyReward`/`battleId`/`isTutorial` on `BattleSO` are inert** — no logic reads them yet,
  same as `RunState.currentEncounterIndex` was before this system existed. They exist for the
  pre-battle/post-battle phases to consume once built.
- **A left-over saved `RunState` can look like "the wrong default encounter loads."** Progress is
  designed to persist across Editor Play sessions (the whole point of `RunState`/PlayerPrefs) — if
  `currentEncounterIndex` was previously advanced (including via the override-leak bug described
  above, before it was fixed) and never reset, a fresh Play without an override will correctly
  resume from that saved index, not restart at encounter 0. Use `CampaignProgressTool`'s "Start New
  Run" or `CampaignDebugTool`'s "Clear Saved Run" button to get back to a genuinely fresh state.

## Future: `MapScene`

The game will eventually gain a `MapScene` that sits between battles — where the pre-battle loadout
and post-battle reward phases actually live, and where the player picks/confirms the next encounter
instead of it just being "whatever's next in the list." `CampaignProgressManager` is built to
already work from it once it exists: it's cross-scene by design specifically for this, and none of
its API assumes `BattleScene` is the only scene that will ever call it. `CampaignManager` (battle-
scoped, recreated every `BattleScene` load) does not need to exist in `MapScene` at all —
`CampaignProgressManager`'s bootstrap read is self-contained for exactly this reason.

## Related docs

- `docs/Campaign.md` — `RunState`/`CampaignManager`/`GameCatalog`, the data layer this system builds
  the actual sequence on top of.
- `docs/GameLoop.md` — `GameManager.RestartBattle()`/`OnBattleRestart`, what
  `CampaignProgressManager.ResetCurrentEncounter()` delegates to.
- `docs/G.md` — `G.EnemyView`/`G.EnemyHero`, read by `CampaignManager.ApplyEncounterToScene()` and
  `Hero.GetMaxHealth()`.
