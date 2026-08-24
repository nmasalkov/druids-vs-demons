# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

"Druids vs Demons" is a Unity 6 (6000.1.6f1, URP 17.1) 2D auto-battler. Two `HeroView`s (player and
enemy) take turns spinning a slot machine to roll creatures, nukes, or spells, then an automated
battle phase resolves combat between the two sides' summoned creatures.

Game code lives entirely under `Assets/Game/_Scripts` (and `Assets/Game/_ScriptableObjects` for data
assets). Everything else under `Assets/` (Epic Toon FX, PixPlays, Feel/MoreMountains, Spine, All In 1
Sprite Shader, etc.) is vendored third-party/asset-store content — treat it as read-only unless a task
specifically requires changing how it's wired into `Assets/Game`.

**Development direction:** the game is moving from a single self-contained battle toward a
**campaign** — a series of battles against different enemies, bracketed by a pre-battle loadout phase
and a post-battle reward phase, with player-improvable run stats (max HP, reroll energy capacity) that
persist across the run. See [`docs/Campaign.md`](docs/Campaign.md) for the run-state data layer this
is built on so far, and [`docs/Encounters.md`](docs/Encounters.md) for the battle sequence built on
top of it. A future `MapScene` will sit between battles — home for the pre-battle/post-battle phases
and encounter navigation — once it exists; `CampaignManager` is already built to work from it.

There is no custom `.asmdef` for `Assets/Game` — it compiles into the default `Assembly-CSharp`
assembly.

### Robotek terminology mapping

The user often describes mechanics in terms of the game Robotek. Translate as follows:

| Robotek term | This project |
| ------------ | ------------ |
| hack         | Charm (the `CharmSO` spell) |
| robots       | creatures |
| mainframe    | hero |
| droid        | mage |
| drone        | archer |
| tank         | tank |

## Working with this repo

This is a Unity project, not a CLI-buildable one — there are no npm/make/CLI build, lint, or test
commands. Development happens in the Unity Editor (6000.1.6f1):

- Open the project in Unity Editor, enter Play mode on the main scene under `Assets/Game/_Scenes` to
  run the game.
- **⚠️ GIANT RULE, do not skip: always sync a scene's saved-on-disk state with the Editor's live
  in-memory state before entering Play mode (`play_game`) or leaving that scene (`open_scene` on a
  different path).** Play mode always runs whatever is currently loaded in memory — not what's on
  disk. A live edit made through a dedicated Coplay tool (`set_property`, `add_component`, ...) that
  was never `save_scene`'d is not safe: opening a *different* scene silently discards it, no warning.
  A direct file edit (`Edit`/`Write` on the `.unity` file itself) only lands on disk — the Editor's
  in-memory copy doesn't know about it until you `open_scene` that same path again to force a reload;
  pressing Play or saving before that reload silently runs/overwrites the stale in-memory version,
  discarding the edit. Concretely: live Coplay-tool edit → `save_scene` before switching scenes or
  playing. Direct file edit → `open_scene` on that same path before doing anything else. This has
  caused real, live-caught data loss in this project already (see `docs/Encounters.md` Gotchas — a
  scene switch without saving first silently dropped wiring work). Full detail in the Coplay MCP
  section below.
- `com.unity.test-framework` is installed as a package dependency, but no EditMode/PlayMode test
  assemblies currently exist in the repo — there is no automated test suite to run.
- After changing any MonoBehaviour/ScriptableObject serialized fields, the change must be verified/
  wired up in the Editor (see rule 6 below) since there's no way to check this outside Unity.

## Plan mode

The user is not an experienced game developer, so in plan mode be a proactive collaborator on the
**new** parts of a plan, not just a transcriber of what they asked for. This applies to genuinely new
architecture/design decisions only — don't relitigate existing code or patterns already established as
the desired approach in this file or `docs/*.md`; those are settled, not up for review.

For the new pieces a plan introduces:

- Think through basic performance implications (allocations/GC per frame or per roll, `Update()` work,
  instantiate/destroy churn) for anything that runs every frame or every battle.
- Think through ease of future expansion — is this a one-off special case, or does it set a pattern
  that will need to repeat (new creature type, new nuke, new campaign stage, etc.)? Prefer the shape
  that generalizes.
- If you see a materially better alternative (simpler, cheaper at runtime, easier to extend later),
  say so and offer it as a variant alongside the user's original ask, with a one-line tradeoff — don't
  silently substitute your own approach.

## Documentation map

CLAUDE.md is a glossary and rule book — deep per-system detail lives in `docs/*.md` so this file stays
short and scannable. Read the matching doc before altering that system, and (per rule 17 below) check
for a matching `docs/*.md` file whenever you're collecting context about a system, even one not listed
here — this list is added to over time and can lag behind the actual `docs/` folder contents.

| System | Doc | Read it when touching... |
| ------ | --- | ------------------------- |
| Round/turn state machine, restart, pause | [`docs/GameLoop.md`](docs/GameLoop.md) | `GameManager`, `GameState`/`ActionState`, the restart/pause feature, the `Generation`/`IsStale` staleness guard |
| Combat resolution, units, health/shield | [`docs/Battle.md`](docs/Battle.md) | `BattleState`, `Health`, `Shield`, `Targetable`/`Unit`/`Creature`/`Hero`, `StatusesManager` |
| Nuke/Spell action pattern | [`docs/ActionsAndSpells.md`](docs/ActionsAndSpells.md) | adding/changing a nuke or spell, `ActionSO`/`ActionResolver`/`ActionAnimation`/`ActionState` |
| Slot machine + AI roller | [`docs/SlotMachine.md`](docs/SlotMachine.md) | `SlotMachine`/`SlotColumn`, `RollStateManager`, `AIController` |
| XP/leveling, gem pickups | [`docs/Experience.md`](docs/Experience.md) | `ExperienceManager`, `Experience`, `ExpirienceGem` |
| Reroll energy/cost | [`docs/Energy.md`](docs/Energy.md) | `EnergyController`, `EnergyDisplay`, `SlotColumn`'s reroll cost gate |
| Campaign/meta progression, run-state save data | [`docs/Campaign.md`](docs/Campaign.md) | `RunState`, `GameCatalog`, `CampaignStateManager`, `CampaignDebugTool`, `ActionSO.id` |
| Encounters, campaign progress/navigation | [`docs/Encounters.md`](docs/Encounters.md) | `EncounterSO`/`FightSO`/`EncounterListSO`, `Encounter`/`EncounterPlayer`, `CampaignManager`, `CampaignProgressTool`, `HeroView.ReplaceHeroAvatar`, `LoadoutPickEncounter` |
| Reward cards, boost rewards | [`docs/Rewards.md`](docs/Rewards.md) | `RewardSO`/`RewardListSO`, `RewardDrawer`, `RewardBonuses`, `RewardCard`/`RewardEncounter`/`RewardEncounterView` |
| Pre-battle loadout picker | [`docs/Loadout.md`](docs/Loadout.md) | `LoadoutPickEncounter`, `LoadoutPickEncounterView`, `MiniCard`, `SlotKind`/`SlotRef` |
| Enemy AI decision-making | [`docs/AI.md`](docs/AI.md) | `AIController`, `AI/Decisions/*`, `AI/Scoring/*`, `AIDegrade`, the fight-wide reroll pool, `RollState`'s AI orchestration |
| Global service locator | [`docs/G.md`](docs/G.md) | `G`, `G.ApplyCampaignLoadout`, adding a new static accessor |

**Keep these docs up to date** (rule 18 below): when a change alters how a documented system works
(new states, new events, changed resolution order, new restart participants, etc.), update the
relevant `docs/*.md` file in the same change instead of letting it go stale.

## Core architecture

### Game loop: `GameManager` + `GameState`

`GameManager` (`Global/GameManager/GameManager.cs`) is a singleton that drives the entire match as a
single coroutine, `RunGameLoop()` — the **single source of truth for round order** (read it
top-to-bottom; no state ever dynamically inserts another state into the sequence). Hero death
interrupts the loop out-of-band via `Hero.OnHeroDied` → `GameOverState`.

`GameManager.RestartBattle()` restarts the battle in place (no scene reload) by firing a static
`OnBattleRestart` event — every script that owns entities or battle-scoped state subscribes to it
independently and resets itself (the canonical example of the event-based architecture in rule 3). A
`Generation`/`IsStale` counter guards against delayed callbacks (`Utils.DoAfterDelay`) that were
scheduled before a restart. Full detail: [`docs/GameLoop.md`](docs/GameLoop.md).

### `G` — global service locator

`G.cs` (`Global/G.cs`) is a singleton most gameplay code reaches other systems through (both sides'
`HeroView`/`CreaturesManager`/`Hero`, the shared creature/nuke/spell pool) rather than holding direct
references. Full detail: [`docs/G.md`](docs/G.md).

### Action pattern: SO (data) → Resolver (logic) → Animation (view)

Nukes and spells share one pattern, split strictly along the data/logic/view boundary (mirrors rule
13): **`ActionSO`** (data/balance + `AnimationPrefabBase`) → **`ActionResolver`** (pure logic,
`ApplyInstant()`) → **`ActionAnimation`** (view only, `Execute(...)`). `ActionState.PlayEntries` drives
this per rolled entry; `ActionState.ResolveInstant` is the non-animated equivalent (rule 7). Full
detail, worked example, and how to add a new nuke/spell:
[`docs/ActionsAndSpells.md`](docs/ActionsAndSpells.md).

### Slot machine

`SlotMachine.cs` drives a multi-column reel (`SlotColumn`) across three roll types
(`RollType.Creature/Nuke/Spell`). `RollStateManager` consumes the finished roll
(`OnFinishRollCompleted`) into typed entries (`IActionEntry`) that `ActionState` plays. For the
AI-controlled side, `RollState` (not `RollStateManager`) drives the machine — asking `AIController`
for decisions and executing them itself; `AIController` never touches `SlotMachine` directly (see
[`docs/AI.md`](docs/AI.md)). Full detail: [`docs/SlotMachine.md`](docs/SlotMachine.md).

### Units

`Targetable` → `Unit` → `Creature`/`Hero`; `Shield` is a `Targetable` directly (not a `Unit`), highest-
priority target for most nukes/melee, no XP on kill. `Health` (`TakeDamage`/`Heal`/`IsDead`,
`PostponeDeath`) is a standalone component. Creature stats/balance live on `CreatureSO` indexed by
`Experience.Level`, per the data/view separation rule. Combat resolution detail:
[`docs/Battle.md`](docs/Battle.md). XP/leveling detail: [`docs/Experience.md`](docs/Experience.md).

## Project coding rules

These are load-bearing conventions for this codebase (from `.github/copilot-instructions.md`) — follow
them for any new/modified game code under `Assets/Game`:

1. **No null-checks on serialized `[SerializeField]` fields.** Let missing references throw so the bug
   is immediately visible.
2. **Use `Utils.DoAfterDelay.Execute(action, delay)`** for one-off delayed calls instead of writing a
   custom coroutine.
3. **Prefer events over direct references** between components for loose coupling. The canonical
   example is the battle-restart architecture: `GameManager` fires one static
   `GameManager.OnBattleRestart` event, and every script that needs to reset itself (heroes, creature
   managers, roll state, XP, AI control, ...) subscribes independently in its own `Start()`
   (unsubscribing in `OnDestroy()`) instead of a coordinator holding direct references to every
   dependent system and calling each of them by hand. Reach for this "one broadcaster, many
   independent subscribers" shape whenever a single fan-out action (restart, game-over, round-start,
   etc.) needs several unrelated systems to each do their own thing.
4. **No cross-script logic in `Awake()`.** `Awake()` is self-initialization only (caching own
   components, singleton `Instance` assignment). Anything depending on other MonoBehaviours or
   singletons goes in `Start()` or later.
5. **No null-checks on mandatory references** (e.g. `Creature.Slot`) — let them throw. Only guard
   values that are genuinely optional. This extends to singleton/manager `Instance` accessors that
   are guaranteed to co-exist wherever the calling code runs — e.g. `CampaignStateManager.Instance`,
   `CampaignManager.Instance`, and `GameManager.Instance` are all guaranteed present in `BattleScene`
   (the first two are cross-scene-persistent singletons on the `CampaignProgress` prefab, not
   `BattleScene`-local, but still always alive by the time anything in `BattleScene` runs — see
   `docs/Campaign.md`), so code that only ever runs inside that scene (or inside another singleton
   also only guaranteed there) shouldn't defensively null-check one from the other. If one is ever
   genuinely missing, that's a scene-setup bug you want surfaced immediately as a
   `NullReferenceException` in the console, not silently swallowed by an
   `if (x.Instance == null) return;`. This doesn't cover guards against genuinely fragile *ordering*
   (e.g. an `Awake()`-vs-`Awake()` dependency between two specific scripts pending on Script
   Execution Order, like `CampaignDebugTool`'s existing `CampaignStateManager.Instance == null`
   check) — those are a different, documented risk and can keep their guard.
6. **After complex changes / new serialized fields**, always give a checklist of what needs manual
   Editor setup (components to add, fields to assign, SO assets to update).
7. **Every animated/delayed game mechanic needs an instant-resolve counterpart** that skips animation,
   so tests/automation can run without waiting (see `NukeState.ResolveNukesInstant()`,
   `ActionState.ResolveInstant`, `BattleState.ResolveBattleInstant()`,
   `ExperienceManager.ResolveGemsInstant()`). The end goal: the **whole game cycle** must be able to
   switch into instant mode and run hundreds of automated test fights headlessly (no visual playback)
   to gather balance data — so an outcome must never depend on its animation. Keep all data mutation
   in resolvers/shots (`Apply()`), never inside animation callbacks, so both paths reach the exact
   same end state.
8. **Use the new Input System** (`UnityEngine.InputSystem`, e.g. `Keyboard.current.jKey...`), never the
   legacy `Input` class.
9. **Cache `[RequireComponent]` sibling references in `Awake()`** and reuse the cached field — never
   repeat `GetComponent` calls at runtime.
10. **Use `[RequireComponent]`** for components a GameObject will essentially always need (e.g. `Health`
    on a unit).
11. **Avoid nested `if`s, especially in loops** — extract methods, use guard clauses/early returns.
12. **Nest small, single-owner enums inside their owning class** (e.g. `SlotMachine.RollType`,
    `SlotMachine.MachineState`); only promote to top-level if multiple unrelated classes need it.
13. **Data/view separation.** Balance numbers (damage, cooldowns, costs, durations, prefab lookup
    tables) belong on `ScriptableObject`s, never on view/animation MonoBehaviours, which hold only
    presentation data (timings, VFX prefabs, audio, transform offsets). Prefer an SO asset over a
    scene MonoBehaviour for any data not tied to a live scene object.
14. **Base-class-owned helper components**, cached once in the base's `Awake()`:
    - Sibling on the same GameObject → `[RequireComponent]` + `GetComponent<T>()`.
    - Dedicated child GameObject by convention → no `[RequireComponent]`; `GetComponentInChildren<T>()`
      and let it throw if missing (rule 5). Example: `HitFeedback` lives on a child GO named
      `HitFeedback`, cached via `Unit.Awake()`.
15. **Play particles through Feel feedbacks.** Gameplay code never calls `ParticleSystem.Play()`
    directly — every particle effect is wired to an `MMF_Player` (with an `MMF_Particles` feedback
    bound to the system) and triggered via `PlayFeedbacks()` / `StopFeedbacks()` (plus the
    `Stop(true, StopEmittingAndClear)` residue fix when force-stopping looping effects). See
    `StatusesManager` for the pattern — it owns the serialized `MMF_Player` refs for every unit
    status/attempt effect. Also: particle systems on units must
    use main-module **Scaling Mode = Local**, not Hierarchy — the enemy side is mirrored via
    `localScale.x = -1`, and Hierarchy-scaled Billboard/Mesh particles inherit the negative scale
    and render invisible (verified live: identical simulation, nothing drawn). A world-space UI
    `Canvas` nested under a unit (e.g. `HpbarCanvas.prefab`'s health bar/text) has the opposite
    problem — it renders fine but mirrored (fill direction reversed, text backwards) — fixed via a
    small self-correcting `MirrorCorrector` component (`Assets/Game/_Scripts/UI/MirrorCorrector.cs`)
    on the Canvas root: corrects once in `Awake()` (covers every normal spawn — the parent slot's
    scale is already final by then) and again on `CreatureAnimator.OnRunToSlotArrived`
    (`GetComponentInParent<CreatureAnimator>()`, null-guarded — Heroes have no `CreatureAnimator`
    and are never reparented after spawn, so they only ever need the `Awake()` correction). That
    event is the one point a unit's ancestor scale can change *after* spawn without the Canvas's own
    immediate parent ever changing — `CharmShot` reparents an existing (never destroyed/reinstantiated)
    creature into a slot on the opposite side, and `CreatureAnimator.RunToCurrentSlot`/
    `FinishRunToSlot` (the visual run-in for that move) settles the creature root's own scale right
    before firing this event. No per-frame polling — Unity has no "an ancestor's scale changed"
    event in general, but this game has exactly one call path that ever changes it post-spawn, so
    hooking that specific event beats polling for it. Reach for the same `MirrorCorrector` component
    on any future world-space UI nested under a unit; if a future mechanic ever reparents a unit
    across sides through some *other* path, wire that path's own settle point into `Correct()` too
    rather than falling back to polling.
16. **Entities/state-holding scripts must support battle restart.** Any script that spawns entities
    (creatures, shields, gems, projectiles, ...) or holds battle-scoped state (pending rolls, pending
    XP, AI control, ...) must subscribe to the static `GameManager.OnBattleRestart` event in its own
    `Start()` (unsubscribe in `OnDestroy()`) and provide its own reset method for the handler to call.
    See `docs/GameLoop.md` for the current subscriber list and `GameManager.RestartBattle()` for how
    the event fires. Don't add restart-handling logic to `GameManager` itself beyond its own fields
    (`_gameOver`, `ActiveSide`, coroutine state) — every other system resets itself.
17. **Check for a matching `docs/*.md` file whenever collecting context about a system**, before
    reading source top-to-bottom from scratch — see the Documentation map above. If a doc exists for
    the system you're touching, read it first; it's cheaper and more complete than re-deriving the
    same understanding from code every session.
18. **Keep `docs/*.md` up to date.** When a change alters how a documented system works (new states,
    new events, changed resolution order, new restart participants, new SO subclasses, etc.), update
    the relevant `docs/*.md` file as part of that same change instead of letting it drift from the
    code.
19. **Surface important runtime state in the Inspector.** A script that owns load-bearing runtime
    state — what phase/state is active, what level something is at, what occupies a slot, which
    encounter/asset is currently resolved, etc. — should show it directly in a custom Editor, not
    leave it buried in private fields only visible via a debugger. The bar is "can someone glance at
    the Inspector while the game is running and know what's actually going on." If there's enough
    state that showing it all flatly gets noisy, group related values and collapse them behind a
    foldout — but any single piece of state that's load-bearing for understanding current behavior
    stays visible by default, not hidden a click away. See `CampaignManagerEditor` (shows
    the resolved current encounter index/id/asset live) for the pattern.
20. **Keep debug-only surface out of the main class file.** When a script that's core game logic
    (not itself a debug tool) exposes a method/field that only a debug tool ever calls — e.g.
    `CampaignManager.SetSessionEncounterOverride`, called solely by `CampaignProgressTool`
    — split that surface into a `<ClassName>.Debug.cs` partial class file instead of mixing it into
    the main one. Same partial-class split already used for non-debug reasons elsewhere (e.g.
    `AttacksResolver.cs`/`AttacksResolver.Mechanics.cs`) — apply it here so the main file stays
    scannable as pure gameplay logic, and anything living in `.Debug.cs` is self-evidently
    debug-only without having to read doc comments to tell.
21. **Adding a new persisted `RunState` field means updating three more places, not just
    `RunState`.** `CampaignProfileSO` needs the matching field (SO reference or plain value,
    mirroring `RunState`'s shape — see `docs/Campaign.md`), `CampaignDebugTool`'s granular
    override section needs the matching `override<Field>`/`<field>` pair and its
    `ApplyDebugProfile` case, and `docs/Campaign.md` needs the field documented. Easy to forget
    one of the three since none of them fail to compile if you do — `CampaignProfileSO` and
    `CampaignDebugTool` are both plain data/Editor-only, so a missed field just silently doesn't
    override, no error anywhere. Applies equally to list-shaped fields (see `docs/Rewards.md`'s
    `statusRewardIds`/`boostRewardIds`/`gatheredCreatureIds`, the first precedent) —
    `CampaignProfileSO` gets a matching `List<T>` of direct SO refs, and `CampaignDebugTool`'s
    override pair is a toggle + `List<T>` drawn via `SerializedProperty` in its Editor, since the
    existing `ref`-based scalar override helpers don't fit a list.
22. **One source of truth per piece of state — never cache a copy that has to be kept in sync by
    hand.** If two pieces of code both need the same value (current HP, current energy, whose turn
    it is, ...), exactly one of them owns it; everything else either reads it live (a computed
    property/method, not a field) or reacts to an event the owner fires when it changes. Don't
    give a second script its own field that gets manually reassigned every time the source
    changes — that's a copy that *can* drift, and eventually will, usually silently. Example:
    `EnergyController.CurrentEnergy` is `=> CampaignStateManager.Instance.CurrentRun.currentEnergy`,
    not a locally cached field kept in step by every call site that spends/restores energy — see
    `docs/Energy.md`. This was a real, live-caught bug: an earlier version gave `EnergyController`
    its own `CurrentEnergy` field that `TrySpendReroll()` updated but nothing wrote back to
    `RunState`, so every encounter transition silently discarded whatever the player had just
    spent. Applies equally to a transient in-memory snapshot as to persisted data — e.g.
    `EnergyController`'s own `_encounterStartEnergy` (the value a same-encounter restart reverts
    to) is fine as a local field precisely because *it*, not `CurrentEnergy`, is the one place
    that value lives.
23. **Most `ScriptableObject`s that represent one of several instances of a kind (creatures,
    nukes, spells, rewards, ...) should carry a stable string `id` field**, used to resolve the
    asset from campaign save data. `RunState` (and any other persisted data) stores these ids,
    never a direct SO reference — `ScriptableObject` references don't survive a `JsonUtility`
    round-trip through `SaveStorage`. Resolve an id back to its asset through a small catalog SO
    with a flat `List<T>` + `Find(id)` (or per-subtype `Find*(id)`), never a per-call linear scan
    written ad hoc at each call site. `ActionSO.id`/`GameCatalog` (`docs/Campaign.md`) is the
    original example; `RewardSO.id`/`RewardListSO` (`docs/Rewards.md`) is the second.
24. **For a fixed, small number of runtime-spawned slots (not a truly open-ended/dynamic list),
    place one explicit, named anchor `Transform` per slot in the prefab/scene, each holding a
    disabled instance of the thing that spawns there** (e.g. `CardSlot1`/`CardSlot2`/`CardSlot3`
    each containing a disabled `RewardCard`, in `RewardEncounter.prefab`), instead of one generic
    container relying purely on a `LayoutGroup` plus code-only `Instantiate` calls. Two wins over
    the generic-container approach: (1) it's trivially debuggable — enable a slot's placeholder
    child in the Inspector (Play mode or not) to see exactly what that slot will look like, with
    no script run needed; (2) spawning is still simple — at runtime, destroy whatever's currently
    parented under the anchor (the disabled placeholder, or a previous spawn) and instantiate the
    real instance as its child, so each slot independently ends up with exactly one live child.
    Reach for this whenever the slot count is fixed by design (a reward draw is always 3 cards, a
    loadout is always N fixed ability slots, etc.) — for a genuinely unbounded/variable-length
    list (arbitrary creature count in a battle, arbitrary log entries, ...) a single dynamic
    container is still correct; don't force this pattern there.
25. **Debug/test tools that override normal game behavior must hijack the flow at the earliest,
    most localized point possible** — the single call site that produces the value being tested,
    not a broader system further upstream or downstream. Example: `DebugRewards` (paired with
    `CampaignManager`/`CampaignStateManager` on the `CampaignProgress` GameObject, same duplicate-guard pattern —
    see rule below on why it must be root-level) overrides a reward draw by short-circuiting
    exactly the `RewardDrawer.DrawThree(...)` call inside `RewardEncounter.SpawnCards()` — one
    `if` at the site that would otherwise produce the random draw — rather than, say, patching
    `RewardDrawer` itself, wrapping `RewardEncounter.Play()`, or requiring a fake `RunState`. This
    keeps the override provably equivalent to the real path (same code runs afterward — spawning,
    claiming, saving — only the *source* of the drawn rewards differs) and keeps the blast radius
    of the debug tool to a single line at a single call site. A one-shot override (a specific "next
    roll" or "next reward" rather than a standing mode) should also clear itself once consumed —
    `DebugRewards.rollOnNextReward` flips back to `false` inside the same method that reads it —
    so it can never silently keep firing after the tester forgets it's checked.
26. **Any singleton `MonoBehaviour` that calls `DontDestroyOnLoad(gameObject)` must be a root
    GameObject in every scene it's placed in** — `DontDestroyOnLoad` silently no-ops on a
    non-root object (logs a one-line console warning, doesn't throw), so a nested copy quietly
    fails to persist across a scene load while its doc comments/class intent claim otherwise. This
    was a real, live-caught bug: `CampaignDebugTool` was nested under `Global` in
    `BattleScene.unity` (but root in `MapScene.unity`), so if a session ever booted from
    `BattleScene`, its `CampaignDebugTool` copy was silently destroyed on the next scene load
    instead of surviving — and because the destroyed object still satisfied Unity's `!=` "fake-
    null" check against the stale `Instance` field, the *next* scene's copy skipped its own
    duplicate-guard and re-ran `Awake()`'s override-application logic again, on a second object,
    using whatever values were authored in *that* scene's copy. Any debug-override checkbox left
    checked in one scene's authored values would then silently re-stomp `RunState` on every
    subsequent transition through that direction, instead of applying once at session boot as
    intended. When adding a new cross-scene singleton (mirroring `CampaignManager`/
    `CampaignStateManager`/`CampaignDebugTool`/`DebugRewards`), always place it directly under the scene root in every
    scene it's duplicated into — verify via the Inspector (no parent shown) or by checking
    `m_Father: {fileID: 0}` in the `.unity` file's `Transform` block.
27. **For pure transform tweening (scale/position/rotation punches, moves, settles), use DOTween
    via a dedicated `<Thing>Animator` component, not `MMF_Player`'s `MMF_Scale`/`MMF_Position`
    feedbacks** — `TankAnimator`/`CreatureAnimator`/`ShieldAnimator`/`RewardCardAnimator` are the
    pattern: a component owning `DOScale`/`DOMove`/`DOAnchorPos` calls, tracking the active
    `Tween`(s) in a field, killing them at the start of every `Play*` call before starting a new
    one, and always animating toward an absolute fixed target rather than a value relative to
    wherever the target currently is — see `ShieldAnimator.KillActive()` for the shape. This keeps
    rapid re-triggering (fast select/deselect, repeated hits) provably safe: no compounding, no
    stuck mid-animation values, no two tweens racing on the same property. `MMF_Player`'s
    `ToDestination` mode is a real trap here — it still runs the curve through
    `RemapCurveZero`/`RemapCurveOne` on top of the already-lerped value, and those default to `1`/
    `2` (meant for `Absolute`/`Additive` mode, not `ToDestination`) unless *explicitly* set to
    `0`/`1` — a real, live-caught bug: `RewardCard`'s select/deselect feedback used
    `MMF_Scale`/`ToDestination` without overriding those defaults, so every play landed on a wrong
    intermediate scale that the *next* play then used as its own starting point, compounding across
    repeated select/deselect into the card visibly growing far past its intended size before
    snapping back at the end. Rule 15 (particles through Feel feedbacks) is unaffected — this rule
    is specifically about transform tweens, where DOTween's explicit `Kill`+absolute-target
    semantics are both simpler and safer than getting `MMF_Scale`'s remap settings right.
28. **Encounter backend/view split for headless-testable pick screens.** Any `Encounter` subclass
    that mutates `RunState` and has more than a trivial (click-anywhere) completion condition splits
    into two components on the same prefab GameObject: the `Encounter` subclass itself is the
    **backend** (owns all state — drawn/pending values, `RunState` reads/writes/`Save()` calls — and
    exposes it only via public events and methods, never a UI reference), and a plain
    `[RequireComponent(typeof(<Backend>))]` `MonoBehaviour` (`<Backend>View`, e.g.
    `RewardEncounterView`) is the **view** (owns every `[SerializeField]` UI reference, subscribes to
    the backend's events, calls the backend's public methods from clicks — never mutates `RunState`
    or decides completion itself). See `RewardEncounter`/`RewardEncounterView` (`docs/Rewards.md`).
    - `Encounter` exposes `public bool Headless { get; set; }` — a plain runtime property, **never**
      `[SerializeField]`, so it can never be left accidentally checked on a real prefab; only test
      code sets it, before calling `Play()` on a bare, view-less instance — and
      `public void CompletePresentation() => Complete();`, a public wrapper so the View (a sibling
      component, not a subclass) can trigger completion despite `Complete()` staying `protected`.
      The backend's own completion-trigger method (`Confirm()` or equivalent) always performs its
      real `RunState` mutation unconditionally, then only self-completes via
      `if (Headless) CompletePresentation();` — in visual mode (`Headless` false, the default) the
      View owns exactly when `CompletePresentation()` fires (e.g. after a discard animation
      finishes), so a test harness gets full call-methods-directly control with zero UI, while real
      gameplay's animation timing is untouched.
    - **The View subscribes to the backend's events in its own `Awake()`, not `Start()`.**
      `EncounterPlayer`/`MapManager` both call `Instantiate()` then `Play()` synchronously in the
      same method. Unity runs `Awake()` synchronously as part of `Instantiate()`, but defers
      `Start()` to later that frame — so a `Start()`-based subscription would miss whatever event
      `Play()` fires inline. This is a narrow, deliberate exception to rule 4 — it's event
      registration on a guaranteed `[RequireComponent]` sibling, cached via `GetComponent` the same
      way rule 9 already caches sibling references in `Awake()`, not logic depending on the
      sibling's own `Awake()`-time state. Ordinary UI button listeners (real clicks can't happen
      before `Start()`) stay wired in `Start()`/unwired in `OnDestroy()` as before.
    - The View never keeps its own copy of backend-owned state (rule 22) — reads it live off the
      backend when needed.
    - Simple `Encounter`s with no real state or animated completion (a click-anywhere dismiss, e.g.
      the current placeholder `LoadoutEncounter`) don't need this split.
    - **Name an event listener for what it actually does, not `Handle<EventName>`.**
      `_backend.OnSwapped += HandleSwapped` says nothing a reader doesn't already know from the
      event's own name. Prefer `OnPoolChanged += RebuildAvailablePool`, `OnConfirmed +=
      ClosePresentation` — the method name is the documentation. Same for a component's own internal
      listener wiring (e.g. `button.onClick.AddListener(NotifyClicked)`, not `HandleClicked`).
    - **If a View needs several pieces of backend state together to redraw, expose one snapshot
      getter instead of several piecemeal reads spread across handlers.** A nested `readonly struct
      State` (rule 12) with a single `GetState()` method beats a scatter of properties/getters each
      queried from a different event handler — it turns "5 events, 5 handlers, 5 different backend
      calls" into "4 events, a couple of `Spawn*`/`Rebuild*` (structural) handlers, and every content
      update running through one shared `Redraw()` that fetches one snapshot and hands it to a few
      `Draw<Region>(state)` functions." This isn't a cache the View holds onto (rule 22 still applies
      to the backend's own fields) — `State` is refetched fresh on every redraw, never stored and
      mutated between frames. See `LoadoutPickEncounter.State`/`LoadoutPickEncounterView.Redraw()`
      (`docs/Loadout.md`) for the worked example; reach for this shape on the next Encounter view
      that ends up with more than 2-3 events.
29. **Player and enemy creature/nuke/spell pools are two separate resolution chains that must never
    cross — this has already caused a real, live-caught bug twice.** Exact data flow, follow it
    precisely whenever touching either side:
    - **Player pool**: `RunState` (`archerId`/`tankId`/`mageId`/...) is the sole source of truth —
      populated from a real save (`SaveStorage`), a pasted "Use External Save" JSON, a "Use Debug
      Profile" `CampaignProfileSO`, or `CampaignDebugTool`'s granular overrides (precedence: External
      Save > Debug Profile > granular overrides, see `CampaignDebugTool.cs`), and mutated going
      forward by the loadout-pick screen (`LoadoutPickEncounter.Confirm()`, `docs/Loadout.md`). Every
      `BattleScene` load, `CampaignStateManager.ApplyLoadoutToG()` resolves those ids through
      `GameCatalog` and writes the result to `G.defaultCreatures`/`defaultNukes`/`defaultSpells` via
      `G.ApplyCampaignLoadout(...)` — falling back to `G.DefaultCreatures.*`'s *own current value*
      (the Inspector-wired default, since this fallback read happens before this same call overwrites
      it) only if a catalog lookup for that id fails.
    - **Enemy pool**: `FightSO.enemyData.creatures` (a direct per-fight SO reference, author-set in
      the Inspector, not an id/catalog lookup) if non-null, **else `G.DefaultCreatures`** — resolved
      by `CampaignStateManager.ResolveEnemyCreatures(fight)` and written to `G.enemyCreatures` via
      `G.ApplyCampaignEnemyCreatures(...)`, called every `BattleScene` load right after
      `ApplyLoadoutToG()` (so `G.DefaultCreatures` is already this encounter's freshly-resolved player
      pool by the time the enemy fallback reads it — never a stale value). `enemyNukes`/`enemySpells`
      have no per-fight data yet and stay fixed Inspector defaults.
    - **The enemy fallback must target `G.DefaultCreatures`, never `G.EnemyCreatures` itself.**
      `G.enemyCreatures` is a plain mutable field with no reset between encounters — falling back to
      "whatever it currently holds" means a fight with no roster of its own silently inherits
      whatever the *previous* fight last wrote there, forever, with nothing in that fight's own data
      to blame. `G.DefaultCreatures` is safe to fall back to only because it's unconditionally
      recomputed from `RunState` on every single encounter, never a leftover.
    - **`SlotMachine.isPlayerMachine`** (a plain per-instance Inspector bool, `true` on the player
      machine / `false` on the enemy machine) is the *only* thing that decides which pool a given
      `SlotMachine` reads — `true` → `G.DefaultCreatures`/`DefaultNukes`/`DefaultSpells`, `false` →
      `G.EnemyCreatures`/`EnemyNukes`/`EnemySpells`. Never derive this from `GameManager.ActiveSide`
      (whose *turn* it is) or anything else — a third `SlotMachine` instance must have this flag set
      explicitly (it defaults to `true`).
    - **`CampaignDebugTool`'s granular creature/nuke/spell overrides touch `RunState` — the player
      side — only.** They have no path to the enemy pool and must never grow one; if enemy-side
      debug overrides are ever needed, they get their own dedicated fields, not a repurposing of
      these. Conversely, because they're real committed/saved scene state, a checked override left
      on after testing silently keeps re-applying on every subsequent Play session — always leave
      them unchecked (bool `0` **and** the referenced asset cleared to `{fileID: 0}`, matching every
      currently-unused override already sitting that way in `CampaignDebugTool`) once done. Live
      incident: `BattleScene.unity`'s `CampaignDebugTool` had `overrideArcher`/`overrideTank`/
      `overrideMage` left checked (pointing at the same demon-themed creatures used to build one
      fight's enemy roster) from earlier testing — invisible in normal play (`MapScene`'s clean copy
      always wins the cross-scene singleton race there), but the instant `BattleScene` was played
      directly for isolated testing, this copy won instead and force-set the player's ids to match
      the enemy's before `ApplyLoadoutToG()` ever ran — both sides ended up rolling the same roster,
      which looked exactly like a bug in whichever creature-pool code had just been touched, when the
      actual cause was this unrelated leftover checkbox. See `docs/G.md` and `docs/Encounters.md` for
      the full writeup.
30. **Keep a class's entry-point method (`Decide()`, `Resolve()`, `Apply()`, and similar — the method
    a reader opens first to understand what the class does) narrative-thin: as few inline comparisons/
    boolean operators as possible.** It should read as a short sequence of guard-clause-style early
    returns, each line naming *what* is being decided, with the actual comparison, RNG roll, or
    multi-branch logic pushed into a small, well-named private helper the entry method just calls.
    Concretely: no bare `&&`/`||` at that level, no arithmetic/bucket comparisons, no direct
    `Random`/`RollForProbability` calls — if a branch needs one of those, it belongs in a helper the
    entry method calls by name instead. It's fine — expected — to end up with several small private
    helpers below it; that's the trade this rule is making (readability of the one method everyone
    opens first, over a minimal method count). See `ShouldSummonCreaturesDecision.Decide()`
    (`AI/Decisions/`, `docs/AI.md`) for the shape:
    ```csharp
    public SummonChoice Decide()
    {
        if (GameManager.Instance.IsFirstRound) return new SummonChoice(true, null);   // NO STUPID
        if (TryRepairShockedCreature(out var repair)) return repair;
        if (AIController.EnemyBoardFull) return new SummonChoice(false, null);        // NO STUPID
        return new SummonChoice(Degrade(DesiredSummon()), null);
    }
    ```
    Every other `AI/Decisions/*.cs` class already follows this shape — treat it as the canonical
    reference whenever writing or reviewing a new decision-style class, not just AI code specifically.
    **When one decision needs to hand a specific choice (not just true/false) forward to whatever acts
    on it, return a small `readonly struct` pairing the bool with that choice** (`RerollChoice`,
    `SummonChoice`) rather than a bare `bool` plus a second, independent piece of code re-deriving the
    same choice from live state elsewhere — one call site should own picking *which* thing, not two
    call sites separately agreeing on it by coincidence.

## Editor / IDE MCP integrations

Setup status in this environment (last confirmed 2026-07-07): **Coplay MCP is connected and verified
live** — registered at user scope (`claude mcp get coplay-mcp` → Connected), `com.coplaydev.coplay` in
`Packages/manifest.json`, and `mcp__coplay-mcp__get_unity_editor_state` returns real live editor state.
**JetBrains MCP is still not set up** — installed Rider is 2024.3.10 (build 243.28141.39); a Rider
update was applied but did not cross the 2025.2 line the built-in MCP Server plugin requires, and no
MCP-related plugin is present under `%APPDATA%\JetBrains\Rider2024.3\plugins`. Re-check the installed
version (`product-info.json` under the Rider install dir, e.g. `C:\Program Files\JetBrains\JetBrains
Rider 2024.3\product-info.json`) after any future Rider update before assuming `mcp__jetbrains__*` is
usable.

### Coplay MCP (Unity Editor Access)

The Coplay MCP server (`mcp__coplay-mcp__*`) gives direct access to the running Unity Editor. Use it
whenever a task would otherwise require the user to manually run something in Unity. Prefer it over
guessing at editor state.

**RULE: MCP tools are mandatory. `execute_script` is the absolute last resort.**
Before every editor action, scan the tool list below. If a dedicated tool exists, you MUST use it — no
exceptions, no "the dedicated tool might be slower / more verbose / has a known quirk".
`execute_script` is only allowed when no dedicated tool covers the case (e.g. reading editor-only
state not exposed elsewhere, batch ops across many objects with no per-object tool, calling APIs with
no MCP wrapper). A known quirk in a dedicated tool does not promote `execute_script` to default — use
the dedicated tool first and only fall back if it actually fails for that call. Don't announce tool
choice ("using execute_script per the gotcha…") — just use the right tool.

**⚠️ GIANT RULE: sync the scene before entering Play mode or switching scenes — never assume Play mode
sees your latest edit.** `play_game` always runs whatever the Editor currently has loaded **in
memory** — not the `.unity` file on disk. Two failure directions, both real and both already bitten
this project:
1. *Live edit, never saved.* You mutate the scene through a dedicated Coplay tool (`set_property`,
   `add_component`, `create_game_object`, ...). That edit lives in the Editor's memory. If you then
   call `open_scene` on a **different** scene (or otherwise let that scene close) before calling
   `save_scene`, the edit is silently discarded — no error, no warning, just gone.
2. *Disk edit, never reloaded.* You edit a `.unity` file directly (`Edit`/`Write` tool, bypassing the
   Editor entirely). That change is now on disk, but the Editor's in-memory copy of that scene — if it
   was already open — doesn't know anything happened. Pressing Play or calling `save_scene` before
   reopening it runs or overwrites the **stale in-memory version**, silently ignoring your file edit.

The fix is mechanical: after a live Coplay-tool edit, call `save_scene` (full asset path — see
Gotchas) before switching scenes or pressing Play. After a direct file edit, call `open_scene` on that
exact path before doing anything else with it. Never chain "edit → immediately do something in another
scene/Play mode" without one of these in between. This is not theoretical: a scene switch without
saving first already silently dropped real wiring work in this project (`docs/Encounters.md` Gotchas
has the writeup) — treat every scene edit as unsafe until it's either saved or the Editor has reloaded
it from disk.

- Compile / errors / logs: `check_compile_errors`, `get_unity_logs`, `get_unity_editor_state`
- Scene + hierarchy: `list_game_objects_in_hierarchy`, `get_game_object_info`, `open_scene`,
  `save_scene` (⚠️ pass full asset path as `scene_name` — see Gotchas), `create_scene`
- GameObjects: `create_game_object`, `duplicate_game_object`, `delete_game_object`,
  `parent_game_object`, `rename_game_object`, `set_transform`, `set_layer`, `set_tag`, `set_property`
- Components: `add_component`, `remove_component`, `set_property` (use for `SerializedField` values)
- Prefabs / assets: `create_prefab`, `create_prefab_variant`, `add_nested_object_to_prefab`,
  `place_asset_in_scene`, `duplicate_asset`, `rename_asset`, `list_all_prefabs_with_bounding_boxes`
- UI Toolkit / UGUI: `create_ui_element`, `set_ui_layout`, `set_ui_text`, `set_rect_transform`,
  `create_panel_settings_asset`, `capture_ui_canvas`
- Materials / shaders / sprites: `create_material`, `assign_material`, `assign_material_to_fbx`,
  `assign_shader_to_material`
- Animation: `create_animation_clip`, `create_animator_controller`, `modify_animator_controller`,
  `create_blend_tree_state`, `set_animation_curves`, `set_animation_clip_settings`,
  `apply_animation_to_rigged_model`, `auto_rig_3d_model`, `list_model_animation_clips`,
  `search_animation_library`
- Input System: `create_input_action_asset`, `add_action_map`, `add_action`, `add_bindings`,
  `add_composite_binding`, `add_control_scheme`, `generate_input_action_wrapper_code`, plus matching
  `remove_*` / `rename_*`
- Generation (AI assets): `generate_3d_model_from_text`, `generate_3d_model_from_image`,
  `generate_3d_model_texture`, `generate_or_edit_images`, `generate_music`, `generate_sfx`,
  `generate_tts`
- Files / search inside project: `read_file`, `list_files`, `search_files`,
  `list_code_definition_names`
- Packages: `list_packages`, `search_installed_packages`, `search_all_packages`,
  `install_unity_package`, `install_git_package`, `remove_unity_package`
- Scene view / capture: `capture_scene_object`, `scene_view_functions`
- Profiling: `get_worst_cpu_frames`, `get_worst_gc_frames`, `list_objects_with_high_polygon_count`
- Play mode: `play_game`, `stop_game`
- Scripts (escape hatch): `execute_script` runs arbitrary C# in the editor — use for anything not
  covered by a dedicated tool
- Project root: `list_unity_project_roots`, `set_unity_project_root` (call once if Coplay points at a
  different project)

**When to use:**
- Verifying compile state after edits → `check_compile_errors` (avoid asking the user to "check the
  console")
- Inspecting hierarchy / component values → `list_game_objects_in_hierarchy` +
  `get_game_object_info`
- Wiring up scenes, prefabs, components → use the dedicated tools instead of writing setup
  instructions for the user
- Saving the active scene → `save_scene` with full asset path (see Gotchas)
- `execute_script` → only when no dedicated tool fits (e.g., reading editor-only state, batch
  operations across many objects, calling APIs not exposed as MCP tools)

**Gotchas:**
- `save_scene` quirk: `scene_name` requires a full asset path, not a bare name. Call `save_scene`
  with the full path first. Only fall back to `execute_script` + `EditorSceneManager.SaveScene` if
  that specific call fails.
- Tools mutate the live editor. Treat them with the same care as editing files: confirm destructive
  operations (deleting GameObjects, removing components, overwriting assets) when intent is unclear.
- `set_rect_transform` can silently no-op (reports success, RectTransform on disk is unchanged) —
  verify anchor/position changes actually landed (re-read the file/`get_game_object_info`) before
  trusting the reported success message; fall back to `execute_script` +
  `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset` if a retry doesn't fix it.
- No dedicated tool simulates a UI click — `capture_ui_canvas` only screenshots, it never proves an
  element is actually clickable (raycast-routing bugs render correctly and still don't respond to
  clicks). To verify click behavior, not just appearance, use `execute_script` with a real
  `EventSystem.RaycastAll` + `ExecuteEvents.GetEventHandler`/`Execute` simulation — see
  `docs/Loadout.md`'s "Testing UI clicks live via Coplay MCP" section for the full recipe and gotchas
  (it's what caught a real click-routing bug that a screenshot alone had already missed).

### JetBrains MCP (Rider / IDE Access)

The JetBrains MCP server (`mcp__jetbrains__*`) connects to the running Rider/IntelliJ IDE. Use it for
code reading, search, and refactoring instead of raw grep/Read/Edit whenever a JetBrains tool fits —
it's indexed, language-aware, and updates references correctly.

**RULE: Prefer JetBrains MCP tools for code work over generic file tools.**
- Renaming a symbol → `rename_refactoring` (updates all references project-wide). Never do a manual
  find-replace on a class/method/field name.
- Searching code → `search_in_files_by_text` / `search_in_files_by_regex` (indexed, much faster than
  shell grep).
- Finding files → `find_files_by_name_keyword` (indexed, very fast) or `find_files_by_glob` for
  patterns.
- Reading a file you already know → `get_file_text_by_path` is fine, but `Read` is equivalent — pick
  either.
- Understanding a symbol at a position → `get_symbol_info` (Quick Documentation: type, signature,
  declaration).
- Editing a known string → `replace_text_in_file` (auto-saves the file; good for surgical edits).
  Plain `Edit` also works.
- Checking errors on a file → `get_file_problems` (IntelliJ inspections, errors + warnings).
- Building / validating after edits → `build_project` (for non-Unity-managed code; for Unity scripts
  use Coplay's `check_compile_errors`).
- Project shape → `list_directory_tree`, `get_project_modules`, `get_all_open_file_paths`.

**When NOT to use JetBrains MCP:**
- Anything touching the live Unity Editor state (scenes, GameObjects, components, assets) → use
  Coplay MCP instead. JetBrains only sees the file system / solution.
- Compile-checking Unity scripts → Coplay's `check_compile_errors` reflects the actual Unity domain
  reload; JetBrains' `build_project` doesn't run Unity's compile pipeline.

**Quick decision:** editing/searching code on disk → JetBrains MCP. Anything inside the running Unity
Editor → Coplay MCP. Shell / non-project files → Bash + Read/Edit.
