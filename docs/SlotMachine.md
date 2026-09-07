# Slot Machine & Roll AI

## What this system does

Each side's turn starts by spinning a 3-column slot machine to roll one of three action types
(creature, nuke, spell). The machine handles spin/stop animation, triple detection and auto-reroll
prompting; `RollStateManager` turns the three landed cards into typed, deduplicated "entries" (with
match counts) that `GameManager`'s `RollState`/`ActionState` machinery consumes next.
`AIController` drives the enemy's machine autonomously, standing in for a human player.

## Key files

- `Assets/Game/_Scripts/SlotMachine/SlotMachine.cs` — top-level machine: state, buttons, roll-type
  switching, triple detection, `FinishRoll()`.
- `Assets/Game/_Scripts/SlotMachine/SlotColumn.cs` — per-column input/event surface (reroll button,
  `Start`/`Update` dispatch).
- `Assets/Game/_Scripts/SlotMachine/SlotColumn.Mechanics.cs` — same class (`partial`), the actual
  spin/stop/bounce physics and card recycling.
- `Assets/Game/_Scripts/SlotMachine/Card.cs` — trivial sprite holder for one visible card cell.
- `Assets/Game/_Scripts/Global/RollStateManager/RollStateManager.cs` — owns both `SlotMachine`
  instances (player/enemy), turns a finished roll into `SpawnEntries`/`NukeEntries`/`SpellEntries`.
  Exposes `ActiveMachine`, a computed property tracking `GameManager.ActiveSide` live.
- `Assets/Game/_Scripts/Global/GameManager/RollState.cs` — for the AI-controlled side, this is the
  orchestrator that asks `AI/AIController.cs` for decisions and executes them (`SetRollType`/
  `StartAll`/`StopAll`/`TriggerReroll`/`FinishRoll`) — see `docs/AI.md`.
- `Assets/Game/_Scripts/AI/AIController.cs` — the enemy AI's decision service. Never calls into
  `SlotMachine`/`SlotColumn` itself; see `docs/AI.md`.
- `Assets/Game/_Scripts/SlotMachine/SlotMachineRigger.cs` — the pure-data "backend" that decides
  every roll result, before any spin animation starts. See "Dirty triple terminology" and
  "SlotMachineRigger — deciding roll results" below.
- `Assets/Game/_Scripts/SlotMachine/HpAdjustmentSettings.cs` — the shared `[Serializable] struct`
  (4 threshold/adjustment pairs) for one side's HP-based Dirty Triple Index adjustment curve; used by
  both `SlotMachineRigger` (author-time defaults) and `FightSO` (per-fight override).

## Dirty triple terminology

- **Dirty triple situation**: mid-decision state where 2 of the 3 slots have already been decided to
  the same `ActionSO`, and the 3rd slot's decision is what determines whether the roll becomes a full
  triple (all 3 matching) or not.
- **Dirty Triple Index** (`SlotMachineRigger.PlayerDirtyTripleIndex`/`EnemyDirtyTripleIndex`): a live,
  per-side, Inspector-editable-at-runtime int coefficient (0–200) controlling the odds a dirty triple
  situation resolves into a full triple. `100` = neutral (a genuine unbiased 1-in-3 pick — no rigging
  at all). `0` = never completes. `200` = always completes.
- **Clean Triple Index** (`SlotMachineRigger.PlayerCleanTripleIndex`/`EnemyCleanTripleIndex`): a live,
  per-side int coefficient, same 0–200 curve as Dirty, but checked once per **fresh** roll
  (`DecideFullRoll` only — never a reroll), *before* any per-slot decision runs at all. Success
  short-circuits straight into an instant, all-3-matching triple. Failure forces the fresh roll's
  2nd decided slot to differ from the 1st, so a full triple can never happen "by accident" on a fresh
  roll — every triple is now attributable either to this index (fresh rolls) or the Dirty Triple
  Index (rerolls). Flat per-fight base, overridden once at fight start from `FightSO`
  (`playerCleanTripleIndex`/`enemyCleanTripleIndex`) — unlike Dirty, it has no HP-based scaling, but
  it DOES have a first-round override: `FightSO.firstRoundCleanTripleIndex` (default 50, mirroring
  `firstRoundDirtyTripleIndex`) forces both sides' opening turns, same as Dirty's own override.
- **Dirty Triple Stabilization** (`FightSO.dirtyTripleStabilization`, 0 = disabled): subtracted from
  the acting side's current Dirty Triple Index each time they earn and re-enter a bonus turn from a
  triple, so a lucky streak can't snowball indefinitely. Reset to a freshly computed HP-adjusted base
  the next time that side starts a genuinely new (non-bonus) turn.
- **Clean Triple Stabilization** (`FightSO.playerCleanTripleStabilization`/
  `enemyCleanTripleStabilization` — separate per-side fields, unlike Dirty Triple Stabilization's
  single shared one; 0 = disabled): a **signed** delta **added** to the acting side's own current
  Clean Triple Index on the same bonus-turn re-entry event Dirty Triple Stabilization reacts to —
  note the different convention from Dirty: Dirty's field is always a positive magnitude that gets
  *subtracted*, Clean's field is a signed value that gets *added*, so a negative value (the intended
  "stabilizing" usage) decreases the index and a positive value would increase it. An independent
  lever on its own track, reset back to the fight's flat `playerCleanTripleIndex`/`enemyCleanTripleIndex`
  base (no HP formula, see below) on that side's next genuinely new turn. Triggered by the exact same
  bonus-turn re-entry event as Dirty Triple Stabilization — either kind of triple (clean short-circuit
  or dirty completion) grants the bonus turn that both stabilizations react to, and both apply
  independently to that same re-entry.
- **Ludo Progress Index** (`FightSO.ludoProgressIndex` + `perRoundLudoProgressOverrides`, read live —
  not cached on the Rigger): added to the acting side's Dirty Triple Index after **each reroll**,
  during that side's first (non-bonus) roll phase of a turn only. Undone the instant that roll phase
  ends (triple or normal Finish) via the running `_ludoBumpApplied` total, and **disabled** for the
  rest of that turn once the side has already earned one triple (Dirty Triple Stabilization handles
  bonus turns in the opposite direction instead). `perRoundLudoProgressOverrides` is an addable
  `(round, value)` list on `FightSO` — `FightSO.GetLudoProgressIndexForRound(round)` returns the
  first matching round's value, else falls back to the common `ludoProgressIndex`. Keyed by
  `GameManager.CurrentRound` (new 1-indexed round counter — one round is the player's turn *and* the
  enemy's turn; a triple's bonus-turn re-entry does not advance it).
- **Actor**: whichever side (`GameManager.ActiveSide`) is currently rolling.

## `RollType` / `MachineState`

Both are nested in `SlotMachine` (project convention — small enums live on their owning class):

- `RollType { Creature, Nuke, Spell }` — which action pool the columns roll from. Selectable via
  `creatureRollButton`/`nukeRollButton`/`spellRollButton`, **only while `MachineState.FirstRoll`**
  (`SwitchRollType` early-returns otherwise). Switching type calls `Reset()` (full reshuffle).
- `MachineState { FirstRoll, Rolling, PostRolls }`:
  - `FirstRoll` — idle, spin button visible, roll-type buttons visible.
  - `Rolling` — columns spinning/stopping in sequence; roll-type buttons hidden.
  - `PostRolls` — all columns landed, not a triple; reroll buttons active per column, finish button
    enabled.

## Column spin/stop mechanics

Each `SlotColumn` is its own tiny state machine (`SlotColumn.State { Idle, Spinning, WaitingToStop,
Stopping, Bouncing }`, private, in `SlotColumn.Mechanics.cs`):

1. `StartSpin()` is now purely visual — state → `Spinning`. It no longer decides anything: by the
   time it's called, `WinningAction` has already been assigned externally (`AssignWinningAction`) by
   whichever caller decided this roll — `SlotMachine.StartAll()` for a fresh 3-column roll, or this
   column's own `StartReroll()` for a single-column reroll — both of which get the actual decision
   from `SlotMachineRigger`. See "SlotMachineRigger — deciding roll results" below for the full
   backend flow; `SlotMachine.GetActionOptions()` (the 3 SOs for the machine's current `RollType`,
   sourced from `G.DefaultCreatures`/`G.DefaultNukes`/`G.DefaultSpells` if this instance's
   `isPlayerMachine` is true, else `G.EnemyCreatures`/`G.EnemyNukes`/`G.EnemySpells` — see
   `docs/G.md`'s Gotchas for why the two sides don't share a pool) is still the source of the 3
   candidate options handed to the rigger.
2. While `Spinning`, `UpdateSpinning()` scrolls the column via `Spin(speed)`, which moves
   `columnContainer` down by `speed * Time.deltaTime` and recycles the bottom card to the top every
   `CellHeight` (150px) of travel — an infinite scroll illusion over a fixed pool of `CardCount = 30`
   cards.
3. `StopSpin()` (called by `SlotMachine.StopAll()` → the `OnSlotMachineStop` event, or a column's own
   reroll flow) transitions `Spinning → WaitingToStop`; the column keeps spinning until it crosses a
   cell boundary (so it doesn't snap mid-cell), then `SnapToAligned()` + `BeginStopping()` computes a
   deterministic stop distance (`_totalStopDistance`, at least 3 full recycles) and swaps the sprite
   of the card that will land in the center slot to `WinningAction.cardSprite` — this is the actual
   "roll result" being written in, visually, right before it's revealed.
4. `Stopping` eases out over `_actualStopDuration` (ease-out quadratic), then `Bouncing` plays a short
   sine overshoot/settle (`bounceDuration`/`bounceHeight`), and finally fires `OnColumnStopped`.
5. `stopDuration` differs for a normal stop (`stopDuration`, default 5s) vs. a reroll
   (`rerollStopDuration`, default 0.7s, faster since it's a single-column re-spin).
6. Each column gets a staggered stop via `SlotMachine.Awake()`'s `col.AddStopDelay(i *
   stopDepDelayBetweenColumns)` (added once, at wiring time, to `stopDuration`), so columns don't all
   land in perfect unison.

`SlotMachine.HandleColumnStopped()` counts stopped columns:
- During `Rolling`, once all columns report stopped: state → `PostRolls`, spin button hidden. If
  `IsTriple()` (all 3 `WinningAction`s equal) the roll auto-finishes immediately (`FinishRoll()`) —
  no reroll opportunity for a triple. Otherwise the finish button and reroll buttons appear
  (`OnPostRollsEnter` fires, which `SlotColumn.OnPostRollsEnter()` uses to show its `rerollButton`).
- During `PostRolls`, it instead counts down `_rerollingCount` (incremented per reroll click in
  `HandleRerollStarted`); when all in-flight rerolls finish, it re-checks `IsTriple()` (a reroll can
  turn a near-miss into a triple) before re-enabling the finish button.

## SlotMachineRigger — deciding roll results (the backend)

`SlotMachineRigger` (singleton `MonoBehaviour`, lives on `Global` next to `G`/`AIController`, reachable
via `G.Rigger`) is the 100% data-only, headlessly-callable "backend" that decides every roll's
results before any spin animation starts. Before this existed, each `SlotColumn` decided its own
result independently and inline (`PickRandomWinningAction`, mid-visual-state-machine) with zero
awareness of the other columns — this is the concrete backend/frontend split for the roll-decision
path that replaces that:

- **Fresh 3-column roll** — `SlotMachine.StartAll()` calls `G.Rigger.DecideFullRoll(GetActionOptions(),
  isPlayerMachine)` and assigns the 3 results to `columns[i].AssignWinningAction(...)` *before* firing
  `OnSlotMachineStart` (which now only kicks off each column's purely-visual `StartSpin()`).
  `DecideFullRoll` first checks the Clean Triple Index (see terminology above) — success returns an
  instant all-3-matching triple with no further decisions made. Otherwise it shuffles which physical
  column gets decided 1st/2nd/3rd (Fisher-Yates over `{0,1,2}`) — so no single visual column is ever
  consistently "the one that completes triples," which is what makes the rigging not look suspicious
  — decides the 1st (shuffled-order) slot with a plain uniform pick, forces the 2nd to differ from
  the 1st (`PickOtherThan`), and the 3rd goes through the dirty-triple check below (which, given the
  2nd is now guaranteed to differ from the 1st, always takes its plain-uniform-pick branch here — its
  dirty-triple branch is only ever live via the reroll path below).
- **Single-column reroll** — `SlotColumn.StartReroll()` calls `G.Rigger.DecideRerollSlot(options,
  otherA, otherB, isPlayerMachine)` (the other 2 columns' current results) before its own
  `StartSpin()`. Same dirty-triple check as the 3rd slot above — a reroll that leaves the other 2
  columns matching is exactly a dirty triple situation for this column too.
- **The dirty-triple check** (shared by both paths): if the other 2 results don't match, the slot is
  a plain uniform pick (no bias possible — a triple can't happen regardless of this slot). If they do
  match, the acting side's Dirty Triple Index is converted to a percent via a piecewise-linear curve
  (`index/3` for 0–100, then ramping to 100% at 200 — the only shape that hits all 3 required anchor
  points: 0→0%, 100→33.3%, 200→100%) and rolled through `AIController.RollForProbability` — success
  completes the triple, failure picks uniformly from the other 2 (non-matching) options.
- **Turn-boundary bookkeeping** — `SlotMachineRigger` subscribes to `GameState.OnAnyStateEnded`
  (`SwitchSideState` → recompute that side's base index: `GameManager.IsFirstRound` forces it to a
  configurable `firstRoundDirtyTripleIndex` (default 50, authored per-fight via
  `FightSO.firstRoundDirtyTripleIndex`), covering both sides' opening turns; otherwise an HP-based
  formula starting from `neutralDirtyTripleIndex` (default 100, authored per-fight via
  `FightSO.neutralDirtyTripleIndex`) — most-severe-tier-wins, not cumulative, per-side
  `HpAdjustmentSettings` (`playerHpAdjustment`/`enemyHpAdjustment`), each independently toggleable via
  `playerHpAdjustmentsEnabled`/`enemyHpAdjustmentsEnabled` — adjusts that neutral baseline up or down;
  disabling a side's toggle just returns the neutral baseline unadjusted; **HP-based adjustment only
  ever touches the Dirty Triple Index** — the Clean Triple Index has no HP formula at all, see below.
  **A POSITIVE HP adjustment is a one-time boost, not a whole-turn one** — `RecomputeBaseForActiveSide`
  earmarks it (`_playerPendingHpBoost`/`_enemyPendingHpBoost`, only for a positive value; a negative
  adjustment/penalty is never earmarked) and `RevertPendingHpBoostForActiveSide` strips it back out the
  instant that side re-enters its first bonus turn, before `dirtyTripleStabilization` is even applied —
  so a large near-death boost helps land the turn's *first* triple, then the index drops back to
  `neutralDirtyTripleIndex` (further eroded by stabilization as normal) for any further chasing within
  that same turn, instead of staying boosted across a whole lucky streak. Caught live: a
  `nearDeathHpAdjustment` of +75 with `dirtyTripleStabilization` at 0 produced 3 triples in a row, since
  nothing ever brought the boosted index back down between bonus turns.)
  and `GameState.OnAnyStateStarted` (`RollState` re-entries that *aren't* a fresh `SwitchSideState`
  turn, i.e. a bonus turn from `GameManager.TakeTurn()`'s triple-driven do-while loop, first revert any
  pending HP boost as above, then apply both `FightSO.dirtyTripleStabilization` to the Dirty Triple
  Index *and* `FightSO.playerCleanTripleStabilization`/`enemyCleanTripleStabilization` (per-side fields,
  unlike the single shared `dirtyTripleStabilization`) to the Clean Triple Index, independently; a
  genuinely fresh entry instead resets the Clean Triple Index back to its flat
  `FightSO.playerCleanTripleIndex`/`enemyCleanTripleIndex` base — or `firstRoundCleanTripleIndex`
  during round 1, mirroring the Dirty base's own first-round override — and marks the turn's first
  roll phase eligible for Ludo Progress). Resets both Dirty indices to neutral (and clears any pending HP boost) on
  `GameManager.OnBattleRestart` (rule 16 — this is battle-scoped state, not `DontDestroyOnLoad`),
  which also re-applies `FightSO`'s overrides (see below) — including the Clean Triple Index base —
  since a restart doesn't change the fight.
- **Ludo Progress reroll hook** — rather than adding any new hook into `SlotColumn`/`SlotMachine`,
  `SlotMachineRigger` subscribes to the *existing* `SlotColumn.OnRerollStarted` event on every column
  of every machine (found once in `Start()` via `FindObjectsByType<SlotMachine>(FindObjectsInactive.
  Include, ...)` — **must** include inactive objects: both machine GameObjects are still inactive at
  `Start()` time, only the active side's gets enabled later by `RollStateManager`, and the
  active-only overload silently finds neither, permanently skipping this hook for the whole session
  — a real bug caught live while implementing this). On each reroll, if the active side's roll phase
  is still Ludo-Progress-eligible (a fresh, non-bonus first roll phase — see above), it adds
  `FightSO.GetLudoProgressIndexForRound(GameManager.CurrentRound)` to that side's Dirty Triple Index
  and tracks the running total in `_ludoBumpApplied`, subtracted back off (and zeroed) the moment
  `RollState` ends (piggybacking the same `OnAnyStateEnded` subscription above) — whether that's from
  a triple or a normal Finish.
- `SlotMachineRigger.Start()` also calls a private `ApplyFightOverrides()` (and `ResetForRestart()`
  re-calls it) that copies `FightSO.neutralDirtyTripleIndex`, `firstRoundDirtyTripleIndex`,
  `playerCleanTripleIndex`/`enemyCleanTripleIndex`, `firstRoundCleanTripleIndex`, and the per-side
  HP-adjustment blocks/toggles
  onto the Rigger's own live fields (which stay public and Inspector-visible as author-time/fallback
  defaults for testing outside a real fight) — same "read `CampaignStateManager.Instance.CurrentFight`
  once at scene start, no null-check" pattern `AIController.ResetRerollPool()` already used first.
  `FightSO.dirtyTripleStabilization`, `playerCleanTripleStabilization`/`enemyCleanTripleStabilization`,
  and `GetLudoProgressIndexForRound` are instead read live off `CampaignStateManager.Instance.
  CurrentFight` at the point they're needed, never cached onto the Rigger — see rule 22 (one source
  of truth). The pending-HP-boost fields (`_playerPendingHpBoost`/`_enemyPendingHpBoost`) are the one
  exception that *is* Rigger-local, battle-scoped runtime state (not authored on `FightSO`) — same
  category as `_ludoBumpApplied`.
- All of the above is pure C#/no `MonoBehaviour` visual coupling — trivially unit-testable headlessly
  (rule 7), same as the rest of this system's AI decision logic.

## Reroll / finish flow

- `OnRerollClicked()` (per column, gated on `_postRollsEnabled && _state == State.Idle`) is also
  gated on `EnergyController.Instance.TrySpendReroll()` — rerolling costs energy at a per-round
  cost that doubles with every reroll and resets when the roll phase ends; see `docs/Energy.md`.
  If the player can't afford it, the click is a no-op (the button is also disabled in that case).
  Otherwise it sets `_isReroll = true`, fires `OnRerollStarted` (disables the finish button while
  any reroll is in flight), asks `SlotMachineRigger` to decide this column's result (see above), and
  self-schedules its own `StopSpin()` after a fixed `0.44f` via `Utils.DoAfterDelay` — i.e. a reroll
  always spins for a fixed short window, not until an external stop signal.
- `FinishRoll()` reads every column's `WinningAction` into a `List<ActionSO>` and fires
  `OnFinishRollCompleted(rolledActions, CurrentRollType)` — this is the single hand-off point to
  `RollStateManager.HandleFinishRoll`.
- `Reset()` (full reshuffle — destroys and reinstantiates all 30 cards per column via
  `SlotColumn.ResetColumn()`) is used when switching `RollType`. `ResetUI()` (lighter — clears
  `MachineState`/reroll counters and column `State` via `SlotColumn.ResetState()`, but does **not**
  reshuffle cards) is used after every normal roll completes and during battle restart — see below.
  `SlotColumn.ResetState()` also snaps `columnContainer.anchoredPosition` back to the column's resting
  Y (via the existing `SnapToAligned()` helper) — needed because a battle restart can happen while a
  column is mid-spin/mid-stop/mid-bounce (e.g. paused there), and without this the reel would stay
  visually offset wherever it was frozen instead of resetting to idle.

## `RollStateManager` — turning a roll into entries

`RollStateManager.ActivateSlotMachine()` (called from `RollState`, not shown here) just activates
`ActiveMachine` (a computed property tracking `GameManager.Instance.ActiveSide` live) — driving the
AI's turn from there on is `RollState`'s job, not `RollStateManager`'s (see "AI control of the enemy
machine" below and `docs/AI.md`). It used to first check that
`CampaignManager.Instance.CurrentEncounter` was actually a `FightSO` (renamed from `BattleSO` —
campaign-layer naming only, unrelated to this file's own "roll" terminology), since
`SlotMachine.Update()` reads `Keyboard.current.spaceKey` directly to start/stop the reel — bypassing
UI raycast blocking entirely, so a pick screen's overlay alone couldn't stop a stray Space press from
spinning reels behind it. That gate was removed once `FightSO` became `EncounterListSO`'s only entry
type (`docs/Encounters.md`): the loadout-pick phase now fully resolves in `MapScene` before
`BattleScene` ever loads, and the reward-pick phase only ever shows strictly after `GameOverState`,
once the round loop has already permanently halted — so `BattleScene`'s round loop is now always
running the actual fight it's supposed to, and the gate's premise can no longer occur.

`HandleFinishRoll(actions, rollType)` (subscribed to both machines' `OnFinishRollCompleted`):
1. Stores `LastRollType`.
2. `AnalyzeRoll` groups the 3 landed `ActionSO`s with LINQ `GroupBy`, and for whichever `rollType` was
   rolled, populates exactly one of `SpawnEntries`/`NukeEntries`/`SpellEntries` with one entry per
   distinct `ActionSO`, `Level`/`Count` = how many of the 3 slots matched it (1, 2, or 3 — 3 means
   `TripleRolled` is also set true, since `groups.Any(g => g.Count() >= 3)`).
   - `SpawnEntry { Creature, Level }` — target creature level to spawn/promote to.
   - `NukeEntry`/`SpellEntry` both implement `IActionEntry` (`Source => Nuke/Spell`, `Level =>
     Count`) — this is the shared shape `ActionState.PlayEntries<TEntry>` consumes; see
     `docs/ActionsAndSpells.md`.
3. Deactivates and `ResetUI()`s the machine that just finished (`ActiveMachine`, same computed
   property), then fires `OnRollFinished`.

`TripleRolled` is read by `GameManager.TakeTurn()` to decide whether to immediately re-roll and
replay the action (see `docs/GameLoop.md`).

## AI control of the enemy machine

`RollState` (`Global/GameManager/RollState.cs`) is the orchestrator for the AI-controlled side —
`AIController` itself never touches `SlotMachine`/`SlotColumn` (see `docs/AI.md` for the full
architectural principle). When the active side is `Enemy`, `RollState.BeginAITurn()`:

1. Asks `AIController` for a roll-type + desired-action decision, calls the machine's
   `SetRollType(type)` (a public entry point mirroring the player's button-driven `SwitchRollType`,
   minus the button-debounce lock) and `StartAll()`.
2. Schedules an unconditional auto-stop after **2 seconds** (`AIThinkDelay`) — same fixed spin
   duration as before, just now owned by `RollState` instead of `AIController`.
3. Subscribes to the machine's `OnPostRollsEnter` and `OnRerollResolved` (see below), both driving
   the same `EvaluateReroll()` method: ask `AIController.DecideReroll(...)` for a `RerollChoice`, and
   either call `Columns[choice.SlotIndex].TriggerReroll()` or `FinishRoll()`.

`SlotMachine.Columns` (`IReadOnlyList<SlotColumn>`) and `SlotColumn.TriggerReroll()` (a public
reroll entry point mirroring the player's `OnRerollClicked`, minus the energy gate — the AI spends
its own fight-wide reroll pool instead, see `docs/AI.md`) are the two additions that make this
possible; both exist solely for `RollState` to call, never `AIController`.

`SlotMachine.OnRerollResolved` is new too: fired in `HandleColumnStopped()`'s `PostRolls` branch once
`_rerollingCount` reaches 0 (i.e. every in-flight reroll has settled) and the result isn't a triple —
previously nothing fired there at all, so nothing could react to "a reroll just finished, decide
again." This is what lets the AI's reroll loop continue past the first reroll.

## Restart integration

Per the project-wide convention (see `docs/GameLoop.md` and the "Restart & pause" rule in
`CLAUDE.md`): any script that creates entities or holds battle-scoped state subscribes to the static
`GameManager.OnBattleRestart` event in its own `Start()` and unsubscribes in `OnDestroy()`, providing
its own reset method. In this system:

- `RollStateManager` subscribes `ResetForRestart` — clears all three entry lists, resets
  `TripleRolled`/`LastRollType` to defaults, and `ResetUI()` + deactivates both slot machines
  (mirrors what `HandleFinishRoll` already does per-machine after a normal roll, just applied to
  both sides unconditionally).
- `AIController` subscribes its private `ResetRerollPool` — re-seeds the fight-wide reroll pool from
  `CampaignStateManager.Instance.CurrentFight.enemyData.rerollsAmount` (see `docs/AI.md`). No
  `SlotMachine` reference to null out anymore — `RollState`'s own per-turn fields need no restart
  handling at all, since a fresh `RollState` is `new`'d every turn and `GameManager.RestartBattle()`
  already tears down the current state (`OnStateEnd()` → `RollState.OnExit()`, unsubscribing from
  whichever machine it was driving) before firing `OnBattleRestart`.
- `SlotMachineRigger` subscribes its private `ResetForRestart` — resets both `PlayerDirtyTripleIndex`/
  `EnemyDirtyTripleIndex` back to the neutral baseline and clears Ludo Progress bookkeeping
  (battle-scoped state, same as the above), then re-calls `ApplyFightOverrides()` so Clean Triple
  Index and the HP-adjustment blocks/toggles come back from `FightSO` rather than whatever they last
  happened to be.

Separately (not a `OnBattleRestart` subscription, but relevant to this system's timing): all of this
system's delays go through `Utils.DoAfterDelay`, which was changed from `WaitForSecondsRealtime` to
`WaitForSeconds` — so `RollState`'s 2-second AI auto-stop and its zero-delay `BeginAITurn` scheduling
now correctly respect `Time.timeScale`, meaning they freeze during the game's pause feature instead
of ticking through it.

## Gotchas

- **Player and enemy roll from separate pools, via `SlotMachine`'s own `isPlayerMachine` flag** — not
  derived from `GameManager.ActiveSide`. See `docs/G.md`'s Gotchas for the live-caught bug this fixes
  (a campaign loadout pick used to leak into the enemy's roster) and the Editor-setup expectation for
  any new `SlotMachine` instance.
- **The AI rerolls via a fight-wide budget, not per-turn energy** — see `docs/AI.md` for the full
  reroll-budget/decision flow. `SlotColumn.TriggerReroll()` (public, ungated by
  `EnergyController`) is the AI-only entry point; `OnRerollClicked` (private, player-only) still goes
  through the energy gate as before — both funnel into the same private `StartReroll()`.
- **Triple always short-circuits rerolling**, both on the initial stop and after any reroll settles —
  `IsTriple()` is checked in both branches of `HandleColumnStopped`, and a resulting `FinishRoll()`
  bypasses `PostRolls` entirely (no reroll buttons ever appear for a triple that's true from the
  first stop).
- **`Reset()` vs `ResetUI()` are not interchangeable.** `Reset()` destroys and reinstantiates every
  card (reshuffles) — only appropriate when switching `RollType`. `ResetUI()` is the cheap path used
  after every roll and by `RollStateManager.ResetForRestart()`; it deliberately does not touch the
  card pool.
- **Space bar is a legacy-feeling debug/convenience control**: `SlotMachine.Update()` reads
  `Keyboard.current.spaceKey.wasPressedThisFrame` directly (no null-guard on `Keyboard.current`,
  unlike the `G.cs`/`PauseMenuController.cs` convention of guarding it) to start/stop the machine —
  this fires for whichever machine's GameObject is active, i.e. effectively "whichever side is
  currently rolling," which is fine given only one machine is ever active at a time but is worth
  knowing if you add a second simultaneously-active machine.
- **`SlotColumn` split across two files is one `partial class`,** not a base/derived pair —
  `SlotColumn.cs` holds events/lifecycle/public control methods, `SlotColumn.Mechanics.cs` holds the
  actual movement math. Treat them as one class when reasoning about state.
- **Both `SlotMachine` GameObjects are inactive when scene-scoped singletons run their own `Start()`**
  — only the active side's machine gets enabled later, by `RollStateManager`. Any `Start()`-time
  `FindObjectsByType<SlotMachine>(...)` needs the `FindObjectsInactive.Include` overload or it
  silently finds zero machines (verified live: `SlotMachineRigger`'s Ludo Progress reroll-hook
  subscription used the active-only overload at first and found nothing, for the whole session, with
  no error anywhere — only caught by explicitly checking the subscription count at runtime).

## Related docs

- `docs/GameLoop.md` — how `RollState`/`ActionState` consume `RollStateManager`'s entries, and the
  full `OnBattleRestart` subscriber list.
- `docs/ActionsAndSpells.md` — how `NukeEntry`/`SpellEntry` (`IActionEntry`) feed into
  `ActionState.PlayEntries`.
- `docs/Energy.md` — the reroll energy/cost system that gates `SlotColumn.OnRerollClicked`.
