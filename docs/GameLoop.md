# Game Loop

## What this system does

Drives the entire match — round order, whose turn it is, when battle happens, when the game
ends, and how a battle restarts/pauses mid-play. It's a coroutine-driven state machine:
`GameManager` owns one Unity coroutine (`RunGameLoop()`) that runs a sequence of plain C#
`GameState` objects one at a time, waiting for each to signal completion before moving to the
next. No state ever dynamically inserts another state into the sequence — `RunGameLoop()` is the
single source of truth for round order and is meant to be read top-to-bottom.

## Key files

- `Global/GameManager/GameManager.cs` — singleton MonoBehaviour; owns the round coroutine,
  `ActiveSide`, the restart/staleness machinery, and hero-death → game-over handling.
- `Global/GameManager/GameState.cs` — abstract base for every state; `OnEnter`/`OnExit`/
  `CompleteState()` contract, plus the `Generation`/`IsStale` staleness guard.
- `Global/GameManager/ActionState.cs` — abstract base for Nuke/Spell states; caster/target
  resolution helpers, the animated entry-playback loop (`PlayEntries`), and the instant-resolve
  path (`ResolveInstant`).
- Concrete states (all in `Global/GameManager/`, one file each): `GameStartState`,
  `SwitchSideState`, `RollState`, `SpawningState`, `NukeState`, `SpellState`, `BattleState`,
  `PostBattleState`, `EndOfRoundState`, `GameOverState`.
- `Units/ExperienceManager.cs`, `PlayerView/CreaturesManager.cs`, `PlayerView/HeroView.cs`,
  `Units/Hero.cs`, `Global/RollStateManager/RollStateManager.cs`, `AI/AIController.cs`,
  `Global/GameManager/EnergyController.cs` — all subscribe to the battle-restart event (see below).
- `UI/PauseMenuController.cs` — pause overlay + `Time.timeScale` freeze.
- `Utils/DoAfterDelay.cs` — the delayed-callback utility every state/animation chain runs on.

## Round flow

`GameManager.RunGameLoop()`:

```
Run(GameStartState)                          // 2s intro delay, no gameplay yet
while (!gameOver):
    PlaySide(Player, runBattleAfter: !firstRound)   // round 1: player summons, no battle after
        SwitchSideState(Player)
        TakeTurn: do { RollState → NukeState|SpellState|SpawningState } while (triple rolled)
        [BattleState → PostBattleState]      // skipped on round 1 for the player
    PlaySide(Enemy, runBattleAfter: true)     // battle always runs after the enemy's turn
        SwitchSideState(Enemy)
        TakeTurn: (same as above)
        BattleState → PostBattleState
    EndOfRoundState                           // marker; loop continues
```

- `CreateActionState()` picks `NukeState`/`SpellState`/`SpawningState` polymorphically off
  `RollStateManager.Instance.LastRollType`.
- A triple roll (`RollStateManager.TripleRolled`) causes `TakeTurn` to loop: re-roll, replay the
  action, check again.
- `GameStartState` just waits 2 seconds then completes — an intro beat, no logic.
- `RollState` activates the active side's slot machine (`RollStateManager.ActivateSlotMachine()`)
  and completes when `RollStateManager.OnRollFinished` fires.
- `SpawningState` (an `ActionState`, despite spawning creatures rather than casting) processes
  `RollStateManager.SpawnEntries`: promotes/heals an existing native-slot creature or spawns a new
  one, joins any active BattleCry buff, and heals (never promotes) charm-slot occupants of the
  same class.
- `NukeState`/`SpellState` call `PlayEntries` on `RollStateManager.Instance.NukeEntries`/
  `SpellEntries` (see `docs/ActionsAndSpells.md` for the SO→Resolver→Animation pattern itself).
- `BattleState.BeginBattle()` builds an `AttacksResolver` from both sides' creatures/hero/shield,
  calls `ExecuteAttacks()`, and waits `maxDuration + 1f` before completing (0 duration —
  everything already dead/no attackers — completes immediately).
- `PostBattleState` waits `PostBattleStateManager.CleanUpDelay`, cleans up dead bodies, clears
  BattleCry statuses, waits `GemCollectionDelay`, then resolves XP gems
  (`ExperienceManager.ResolveGems()`) before completing.
- `EndOfRoundState` is a pure marker — completes immediately, exists so the round boundary is
  visible in the state sequence and events.
- `GameOverState` **never calls `CompleteState()`** — the round loop stops here, by design. It logs
  the winner/draw, then hands off to `CampaignManager.Instance.ResolveVictory()`/
  `ResolveDefeat()`, which schedule the actual campaign transition (advance/reload/complete) after a
  delay — see `docs/Encounters.md`'s "Battle results" section. `GameManager.RestartBattle()`
  (triggered manually, or by that scheduled transition) is still the only way the round loop itself
  resumes.

## GameState / ActionState contract

- `OnStateStart()` fires the static `GameState.OnAnyStateStarted` event then calls the virtual
  `OnEnter()`. `OnStateEnd()` calls `OnExit()` then fires `OnAnyStateEnded`. Both static events are
  hooks for debug HUDs/tooling — no gameplay code depends on them today.
- A state signals it's done by calling its own `CompleteState()`, which invokes the instance
  `OnStateCompleted` event.
- `GameManager.Run(GameState state)` is the driver: sets `_currentState`, subscribes a local
  `done = true` flag to `OnStateCompleted`, calls `OnStateStart()`, then polls
  `while (!done) yield return null;` before unsubscribing and calling `OnStateEnd()`.
- `ActionState` adds caster/target resolution (`Caster`, `CasterView`, `EnemyHero`, `EnemyShield`,
  `EnemyCreatures` — all keyed off `GameManager.Instance.ActiveSide`), `BuildContext()` for
  `ActionContext`, the animated `PlayEntries`/`PlayEntry`/`ScheduleNext` chain, and the paired
  `ResolveInstant` static method used by rule-7 instant-resolve callers (`NukeState.ResolveNukesInstant()`,
  `SpellState.ResolveSpellsInstant()`).

## The Generation / IsStale staleness guard

Problem: `Utils.DoAfterDelay.Execute(action, delay)` runs on a `DontDestroyOnLoad` `CoroutineRunner`
with fire-and-forget `Action` closures and no cancellation handle. Both the round loop's state
chaining and every animation/action sequence run through it. If `GameManager.RestartBattle()`
fires while one of these delays is in flight, the stale callback firing later could call
`CompleteState()` on a dead/replaced coroutine, or read/mutate creatures and heroes that the
restart already reset or destroyed.

Fix: `GameManager.Generation` is an `int` incremented as the very first line of `RestartBattle()`.
`GameManager.IsStale(int capturedGeneration) => capturedGeneration != Instance.Generation` is the
static check.

- `GameState.Generation` is captured in a field initializer (`= GameManager.Instance.Generation`)
  at construction time — every `new XState()` snapshots the generation current when it was
  created. `GameState.IsStale` reads `GameManager.IsStale(Generation)`.
- `GameState.CompleteState()` checks `IsStale` first and no-ops if stale. This is the single choke
  point every state (`GameStartState`, `RollState`, `SpawningState`, `NukeState`, `SpellState`,
  `BattleState`, `PostBattleState`, `EndOfRoundState`, and any `ActionState`) funnels completion
  through, so this one guard covers all of them.
- `ActionState.PlayEntry` checks `IsStale` as its very first line (before touching
  `entry.Source`, `BuildContext()`, or instantiating an animation prefab) — covers the
  entry-by-entry animation chain specifically.
- `ActionState.CompleteWithCleanup()` checks `IsStale` before calling `CleanUpDeadCreatures()`
  (and again implicitly via the guarded `CompleteState()` call after it).
- Outside `GameState`, several more sites capture a local `int generation` at schedule time and
  check `GameManager.IsStale(generation)` inside the delayed closure: `AIController`'s 2-second
  `_targetMachine.StopAll()` timer (guarded with a plain null-check on `_targetMachine` instead,
  since the actual bug there is the field going null, not staleness — see below);
  `ExperienceManager.LaunchGemsToOwners()`'s per-gem flight closures (guarded twice: before
  `FlyTo`, and again before the arrival callback grants XP); and
  `CampaignManager.ResolveVictory()`/`ResolveDefeat()`'s 4-second post-battle delays (see
  `docs/Encounters.md`) — without the guard, a manual restart during that window would leave the
  stale delayed callback to fire anyway and double up on the transition.

Not guarded, deliberately: `GameStartState`'s intro delay, `BattleState`'s completion delay,
`PostBattleState`'s cleanup/gem-collection delays, `RollStateManager`'s internal button-unlock
delay — these only ever call the already-guarded `CompleteState()`, or do idempotent/harmless work
against an already-reset manager.

## Battle restart architecture

`GameManager.RestartBattle()` (public, called by `PauseMenuController.HandleRestartClicked()`):

```csharp
public void RestartBattle()
{
    Generation++;                              // invalidate every in-flight closure
    if (_mainCoroutine != null) { StopCoroutine(_mainCoroutine); _mainCoroutine = null; }
    _currentState?.OnStateEnd();
    _currentState = null;
    _gameOver = false;
    SetActiveSide(ActiveSide.Player);

    OnBattleRestart?.Invoke();                 // every subscriber resets itself

    _mainCoroutine = StartCoroutine(RunGameLoop());
}
```

`GameManager` only resets what it directly owns (coroutine, `_currentState`, `_gameOver`,
`ActiveSide`). Everything else resets itself via the static event
`GameManager.OnBattleRestart` (`Action`, no args). **This is the project's canonical event-based
reset pattern — see the CLAUDE.md rule it backs:** any script that spawns entities or holds
battle-scoped state subscribes in `Start()` and unsubscribes in `OnDestroy()`, with its own reset
method as the handler. Current subscribers (verified by grep — keep this list in sync when adding
more):

| Script | Handler | What it resets |
|---|---|---|
| `Units/Hero.cs` | `InitHealth` | Refills HP to max via `Health.Init(GetMaxHealth())`. One `Hero` instance per side subscribes independently — no need for `GameManager` to know about both sides. |
| `PlayerView/HeroView.cs` | `ClearShield` | Destroys any live `Shield` GameObject and clears the shield slot. |
| `PlayerView/CreaturesManager.cs` | `ResetAll` | Destroys every creature in every slot (native + both charm slots per class), unconditionally, no death animation. |
| `Global/RollStateManager/RollStateManager.cs` | `ResetForRestart` | Clears `SpawnEntries`/`NukeEntries`/`SpellEntries`, resets `TripleRolled`/`LastRollType`, resets both slot machines' UI and deactivates them. |
| `Units/ExperienceManager.cs` | `ClearForRestart` | Destroys any in-flight XP gem GameObjects, clears `pendingXp`/`activeGems`, resets `gemsInFlight` — discards XP rather than granting it (contrast with `ResolveGemsInstant`, which grants). |
| `AI/AIController.cs` | `ReleaseControl` | Releases AI control of a slot machine if it currently holds one (now null-guarded — restart can fire this when the AI isn't in control at all). |
| `Global/GameManager/EnergyController.cs` | `ResetForRestart` | Refills reroll energy to `CampaignStateManager.Instance.CurrentRun.currentEnergy` (read fresh, campaign-persistent — not a local baseline) and resets the reroll cost back to `baseRerollCost`. See `docs/Energy.md`. |

Because subscribers are independent (none of them read another subscriber's post-reset state),
firing order among them doesn't matter — `OnBattleRestart?.Invoke()` runs all of them
synchronously before `RestartBattle()` proceeds to restart the loop.

## Pause

`PauseMenuController` toggles pause on Escape (`Keyboard.current.escapeKey.wasPressedThisFrame`,
the project's standard new-Input-System polling pattern) and on the Restart button. Pausing sets
`Time.timeScale = 0f`; unpausing (or restarting) sets it back to `1f`.

This is a **real freeze**, not just an input-blocking overlay, because `Utils.DoAfterDelay` was
changed from `WaitForSecondsRealtime` to `WaitForSeconds` — the *only* timing mechanism in the
codebase that didn't already respect `Time.timeScale` (slot-reel movement and projectile movement
already use scaled `Time.deltaTime`; Animator and DOTween default to scaled time too). Since the
entire round/animation pipeline chains through `DoAfterDelay`, that one change made the whole game
freeze correctly under `timeScale = 0`. `PauseMenuController.OnDestroy()` also resets
`Time.timeScale = 1f` defensively.

## How to add a new GameState

1. Create a class deriving `GameState` (or `ActionState` if it needs caster/target resolution).
2. Override `OnEnter()` to do the state's work and call `CompleteState()` when done (immediately,
   or via `Utils.DoAfterDelay.Execute(CompleteState, delay)`, or via an event subscription pattern
   like `RollState`).
3. Override `OnExit()` only if you subscribed to something in `OnEnter()` that needs unsubscribing
   (see `RollState`).
4. Wire it into `GameManager.RunGameLoop()`/`PlaySide`/`TakeTurn`/`RunBattle` at the exact point in
   the sequence it belongs — this file is the single source of truth for round order; don't have
   another state conditionally insert it.
5. If the state spawns entities or mutates shared state via a delayed closure, guard against
   staleness the same way existing states do (rely on the already-guarded `CompleteState()` for
   simple cases; add an explicit `IsStale` check before the closure body for anything that
   mutates state or touches objects before calling `CompleteState()`).
6. If the mechanic is animated/delayed, add a instant-resolve counterpart per rule 7 (see
   `docs/ActionsAndSpells.md` and the existing `ResolveNukesInstant`/`ResolveSpellsInstant`/
   `ResolveBattleInstant`/`ResolveGemsInstant` as models).

## Gotchas

- **Hero death interrupts the loop out-of-band.** `Hero.OnHeroDied` is a static event; `Hero.Start()`
  wires `Health.onDeath` to fire it. `GameManager.Start()` subscribes `HandleHeroDied`, which — if
  `IsGameOver()` (**either hero is dead** — `G.PlayerHero.Health.IsDead() || G.EnemyHero.Health.IsDead()`,
  remaining creatures on either side don't matter) — sets `_gameOver = true`, stops the coroutine,
  force-ends `_currentState`, and enters `GameOverState`. This can happen mid-state, not just
  between states, since it's event-driven rather than part of the yield sequence. Each encounter's
  enemy hero is effectively the boss — killing it ends the fight immediately, it doesn't require
  clearing every creature it summoned too (a deliberate campaign-era simplification; the original
  symmetric "eliminate the whole side" rule is gone).
- **`GameOverState` never calls `CompleteState()`, but is no longer a silent dead end** — it hands
  off to `CampaignManager` (see above), which schedules a real transition. `RestartBattle()`
  itself doesn't care what state the game was frozen in and remains the only way the round loop
  resumes, whether triggered manually (pause menu) or by that scheduled transition.
- **Rule 7 (instant-resolve) relationship:** `RestartBattle()` itself doesn't need an instant-resolve
  counterpart — it's a UI-triggered meta action, already synchronous by construction, not an
  animated game mechanic whose *outcome* needs to be reproducible headlessly. The instant-resolve
  methods that do exist (`ResolveNukesInstant`, `ResolveSpellsInstant`, `ResolveBattleInstant`,
  `ResolveGemsInstant`) are unrelated to restart — they let a full round run without animation for
  balance-testing automation.
- **Round 1 skips the post-player-turn battle** (`runBattleAfter: !firstRound` in `RunGameLoop`) —
  the player just summoned units and shouldn't be attacked immediately; the enemy's turn always
  runs battle after, including on round 1.
