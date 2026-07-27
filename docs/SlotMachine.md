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
- `Assets/Game/_Scripts/AI/AIController.cs` — takes/releases control of the enemy's `SlotMachine`.
- `Assets/Game/_Scripts/AI/AIRollController.cs` — the actual "AI" decision (currently a stub).

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

1. `StartSpin()` → `PickRandomWinningAction()` picks a random option from
   `SlotMachine.GetActionOptions()` (the 3 SOs for the machine's current `RollType`, sourced from
   `G.DefaultCreatures`/`G.DefaultNukes`/`G.DefaultSpells`) and stores it as `WinningAction`. State →
   `Spinning`.
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

## Reroll / finish flow

- `OnRerollClicked()` (per column, gated on `_postRollsEnabled && _state == State.Idle`) is also
  gated on `EnergyController.Instance.TrySpendReroll()` — rerolling costs energy at a per-round
  cost that doubles with every reroll and resets when the roll phase ends; see `docs/Energy.md`.
  If the player can't afford it, the click is a no-op (the button is also disabled in that case).
  Otherwise it sets `_isReroll = true`, fires `OnRerollStarted` (disables the finish button while
  any reroll is in flight), and self-schedules its own `StopSpin()` after a fixed `0.44f` via
  `Utils.DoAfterDelay` — i.e. a reroll always spins for a fixed short window, not until an external
  stop signal.
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

`RollStateManager.ActivateSlotMachine()` (called from `RollState`, not shown here) first checks that
`CampaignManager.Instance.CurrentEncounter` is actually a `FightSO` (renamed from `BattleSO`
— campaign-layer naming only, unrelated to this file's own "roll" terminology) — outside a real fight
(a `LoadoutPickSO`/`RewardPickSO` pick screen, see `docs/Encounters.md`) it does nothing and returns.
This isn't optional polish: `SlotMachine.Update()` reads `Keyboard.current.spaceKey` directly to
start/stop the reel, bypassing UI raycast blocking entirely, so a pick screen's overlay alone
couldn't stop a stray Space press from spinning reels behind it. Otherwise, it activates whichever
side's machine matches `GameManager.Instance.ActiveSide`; if it's the enemy, it also queues
`AIController.Instance.TakeControl(machine)` via a zero-delay `Utils.DoAfterDelay.Execute` (so it runs
after the current frame's activation settles).

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
3. If the enemy just rolled, releases AI control (`AIController.Instance.ReleaseControl()`).
4. Deactivates and `ResetUI()`s the machine that just finished, then fires `OnRollFinished`.

`TripleRolled` is read by `GameManager.TakeTurn()` to decide whether to immediately re-roll and
replay the action (see `docs/GameLoop.md`).

## AI control of the enemy machine

`AIController` (singleton, `Instance` set in `Awake()`) is the enemy's stand-in "player":

- `TakeControl(slotMachine)` stores `_targetMachine`, subscribes to its `OnPostRollsEnter`, calls
  `StartAll()` to begin spinning, and schedules an unconditional auto-stop after **2 seconds**
  (`Utils.DoAfterDelay.Execute(() => { if (_targetMachine != null) _targetMachine.StopAll(); }, 2f)`)
  — this is what makes the enemy "decide" to stop spinning without real input.
- `HandlePostRolls()` (fired once the machine reaches `PostRolls`) asks
  `AIRollController.Decide()` what to do. **`AIRollController.Decide()` is currently a stub that
  always returns `AIRollDecision.FinishRoll`** — the `PostRollSlot1/2/3` reroll-decision branches in
  `AIController.HandlePostRolls` are unimplemented (`// TODO: handle rerolls`), so the enemy never
  rerolls today, it always finishes immediately after its first stop.
- `ReleaseControl()` unsubscribes from `OnPostRollsEnter` and clears `_targetMachine`. Called both
  normally (`RollStateManager.HandleFinishRoll`, right after the enemy's roll is analyzed) and on
  battle restart (see below).

## Restart integration

Per the project-wide convention (see `docs/GameLoop.md` and the "Restart & pause" rule in
`CLAUDE.md`): any script that creates entities or holds battle-scoped state subscribes to the static
`GameManager.OnBattleRestart` event in its own `Start()` and unsubscribes in `OnDestroy()`, providing
its own reset method. In this system:

- `RollStateManager` subscribes `ResetForRestart` — clears all three entry lists, resets
  `TripleRolled`/`LastRollType` to defaults, and `ResetUI()` + deactivates both slot machines
  (mirrors what `HandleFinishRoll` already does per-machine after a normal roll, just applied to
  both sides unconditionally).
- `AIController` subscribes `ReleaseControl` directly (it already matches the required
  parameterless-`void` signature).

Both `ReleaseControl()` and the 2-second auto-stop closure in `TakeControl()` had to be made
null-safe for this: previously `ReleaseControl()` assumed `_targetMachine` was always non-null
(true under the old, only call site — right after `TakeControl` during the enemy's own roll), but a
battle restart can now call `ReleaseControl()` at any time, including while the AI never took control
at all this round (e.g. restart during the player's turn). Similarly the auto-stop closure now
null-checks `_targetMachine` before calling `StopAll()` on it, since a restart's `ReleaseControl()`
could null that field out before the 2-second timer fires.

Separately (not a `OnBattleRestart` subscription, but relevant to this system's timing): all of this
system's delays go through `Utils.DoAfterDelay`, which was changed this session from
`WaitForSecondsRealtime` to `WaitForSeconds` — so `AIController`'s 2-second auto-stop and
`RollStateManager.ActivateSlotMachine`'s zero-delay `TakeControl` scheduling now correctly respect
`Time.timeScale`, meaning they freeze during the game's pause feature instead of ticking through it.

## Gotchas

- **AI never rerolls.** `AIRollController.Decide()` always returns `FinishRoll`; the enemy always
  locks in its first stop. If you implement real reroll AI, wire the `PostRollSlot1/2/3` cases in
  `AIController.HandlePostRolls` to call the matching column's reroll (there's no direct
  `SlotColumn.Reroll()` public method today — `OnRerollClicked` is private and button-driven, so
  you'd need to expose a public reroll entry point on `SlotColumn` first).
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

## Related docs

- `docs/GameLoop.md` — how `RollState`/`ActionState` consume `RollStateManager`'s entries, and the
  full `OnBattleRestart` subscriber list.
- `docs/ActionsAndSpells.md` — how `NukeEntry`/`SpellEntry` (`IActionEntry`) feed into
  `ActionState.PlayEntries`.
- `docs/Energy.md` — the reroll energy/cost system that gates `SlotColumn.OnRerollClicked`.
