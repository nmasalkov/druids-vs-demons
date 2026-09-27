# Enemy AI

## What this system does

Drives the enemy's turn: whether to summon creatures or cast a nuke/spell, which specific action to
go for, and whether/how much to reroll — all computed from weights on the current fight's
`FightSO.enemyData`, with a "stupidity"/"critical failure" mechanism that lets the AI occasionally
miss its own best move. Everything is broken into small, single-purpose **decision classes**
(`Assets/Game/_Scripts/_AI/_Decisions/`), one command-pattern-style class per choice, each just
`new SomeDecision().Decide()`. Per-action scoring for nukes/spells is similarly modular — linked
directly to each action's own `ScriptableObject` via `CreateAIScorer()`, so a new nuke/spell plugs
into the AI automatically (see "How to add AI scoring to a new nuke/spell" below).

## The core architectural principle: `AIController` is a pure decision service

**`AIController` never touches `SlotMachine`/`SlotColumn` or any UI, never subscribes to their events,
has no coroutines.** Every `AIController.Decide*` method is a pure function: it reads live state
(via `G`/`CampaignStateManager`/`RollStateManager`) and returns a decision — a `bool`, a `CreatureSO`,
an `ActionSO`, an `int`, or a `RerollChoice` struct. It never calls `StartAll`/`StopAll`/`FinishRoll`/
`TriggerReroll`/`SetRollType`.

**`RollState`** (the `GameState` that already owns the roll phase) is the orchestrator: when the
active side is AI-controlled, it asks `AIController` for a decision and then executes it itself —
the exact same kind of call a player's UI click would make (`SlotMachine.SetRollType`,
`SlotMachine.StartAll`/`StopAll`, `SlotColumn.TriggerReroll`, `SlotMachine.FinishRoll`). For the
player's own turn, real UI still drives things directly, same as before this system existed —
`RollState` doesn't intercept player input, it just waits for `RollStateManager.OnRollFinished` like
it always has.

This split exists in service of the project's standing headless-testability goal (CLAUDE.md rule 7):
because a decision is expressed purely in domain types and never touches a live `SlotMachine`
reference, a future headless test path (driving a whole battle with zero visuals, for either side)
only needs to supply fake state and call these same `Decide*` methods — `AIController` itself
wouldn't need to change at all. **Actually building that full headless path is out of scope here** —
it would mean splitting `SlotMachine`/`SlotColumn` into a data-only backend plus a view, mirroring the
`RewardEncounter`/`RewardEncounterView` pattern (`docs/Rewards.md`) — and stays a documented gap,
same as it already is for the player's side (see `docs/GameLoop.md`'s "no headless path yet" Gotcha).
This system's contribution is making that future work purely additive instead of requiring
`AIController` to be reworked again.

## Key files

- `_Global/_Campaign/FightSO.cs` — `EnemyData.stupidityChance`/`criticalFailureChance` (`[Range(0,100)]`
  ints) and `rerollsAmount` (int, the fight-wide reroll pool — see "Rerolls" below).
- `_AI/AIController.cs` — the thin static façade: `RollForProbability` (also reused by
  `SlotMachineRigger` — see `docs/SlotMachine.md` — as the underlying primitive for its dirty-triple
  chance roll), the `Decide*` methods (each just `new XyzDecision().Decide()`), and the fight-wide
  reroll pool (`RerollsRemaining`/
  `SpendReroll()`) — the one piece of real state this class owns, mirroring how `EnergyController`
  owns the player's energy (not a `SlotMachine` coupling). `OnRerollsChanged(int)`/`OnRerollSpent()`
  (instance events) mirror `EnergyController`'s `OnEnergyChanged`/`OnEnergySpent` split (see
  `docs/Energy.md`'s "Two energy events"): `OnRerollsChanged` fires on every change to the pool (a
  spend or a `ResetRerollPool()` reset), `OnRerollSpent` only on an actual spend — so UI feedback
  plays for real spending only, not for a battle-restart/initial reset.
- `_AI/AIController.StateChecks.cs` — partial class (mirrors the `SlotColumn.cs`/
  `SlotColumn.Mechanics.cs` split): `PlayerHeroBelow`/`EnemyHeroBelow`/`AnyPlayerCreatureBelow`/
  `AnyEnemyCreatureBelow` (float 0..1 thresholds), `PlayerCreatureCount`/`EnemyCreatureCount`,
  `EnemyBoardFull`, `ShockedEnemyCreatures` (the AI's own stunned native creatures), and
  `PlayerFirepowerLead` (signed board-power differential, see "Rerolls" below). These are the
  primitives `_Decisions/` classes use directly, and that `_Scoring/ActionAIScorer` wraps for its own
  int-percent-based API. **`_AI` code reads board state only through these named accessors** — never
  `G`/`Health`/`CreaturesManager`/`AttacksResolver` directly (the same convention `ActionAIScorer`
  states for scorers), so a new board-state read gets a named primitive here first.
- `_AI/AIDegrade.cs` — the one shared stupidity/critical-failure resolver every stupidity-affected
  decision goes through (see "The degrade algorithm" below).
- `_AI/RerollChoice.cs` — `readonly struct { bool ShouldReroll; int SlotIndex; ShouldRerollDecision.
  RerollMode NextMode; bool GrantBonusReroll }`, the reroll decision's return type — built via
  `RerollChoice.Reroll(...)`/`RerollChoice.Finish(...)` factory methods, not a raw constructor.
  Carries the per-turn state machine's next mode and whether this call grants the one-time bonus
  reroll (see "Rerolls" below).
- `_AI/SummonChoice.cs` — `readonly struct { bool ShouldSummon; CreatureSO RepairTarget; }`, the
  summon decision's return type — see "`SummonChoice`: threading the repair target through" below.
- `_AI/_Decisions/` — one class per choice: `ShouldSummonCreaturesDecision`,
  `PreferredCreatureTypeDecision`, `PickActionDecision`, `RerollBudgetDecision`,
  `ShouldRerollDecision`.
- `_AI/_Scoring/` — `ActionAIScorer` (abstract base, the readable query surface every concrete scorer
  is built on) + `NoOpActionAIScorer` + one concrete scorer per action (`FireMagicAIScorer`,
  `ShockAIScorer`, `StarfallAIScorer`, `ShieldAIScorer`, `CharmAIScorer`, `BattleCryAIScorer`).
- `_ScriptableObjects/ActionSO.cs` — `CreateAIScorer()` (virtual, defaults to `NoOpActionAIScorer`),
  the SO→scorer factory every concrete nuke/spell SO overrides in one line, mirroring the existing
  `CreateResolver()` pattern (`docs/ActionsAndSpells.md`).
- `_Global/_GameManager/RollState.cs` — the orchestrator (see above). Owns the AI's per-turn state
  (`_desiredAction`, `_rerollBudget`, `_rerollsUsed`, `_rerollMode`, `_bonusGranted`) as plain fields
  on the `GameState` instance —
  no restart handling needed, since a fresh `RollState` is `new`'d every `TakeTurn()` iteration and
  `GameManager.RestartBattle()` already tears down the current state before firing
  `OnBattleRestart` (`docs/GameLoop.md`).
- `_Global/_GameManager/GameManager.cs` — `IsFirstRound` (public property, was a private coroutine
  local before this system), read by `ShouldSummonCreaturesDecision`'s NO-STUPID first-turn rule.
- `_Global/_RollStateManager/RollStateManager.cs` — `ActiveMachine` (computed property, tracks
  `GameManager.ActiveSide` live — rule 22, one source of truth) replaces the old
  `AIController.Instance.TakeControl`/`ReleaseControl` coupling entirely.
- `_Units/Health.cs` — `HealthPercent` (0..1 float) and `CurrentHealth` (raw float points).
  `HealthPercent` is the primitive every HP-threshold check in this system is ultimately built on;
  `CurrentHealth` is the one exception — `RerollBudgetDecision`'s formula reads it directly (via
  `AIController.EnemyHeroCurrentHealth`) and discounts it by the board-power deficit (see "Rerolls"
  below).
- `_Global/_GameManager/AttacksResolver.Mechanics.cs` — `OpponentFirepowerAdvantage`, the board-power
  differential behind `AIController.PlayerFirepowerLead`. Shared with `SlotMachineRigger`'s comeback
  ladder; see `docs/Battle.md` for the estimate's exact formula and what it deliberately excludes.

## Decisions and the degrade algorithm

### STUPIDITY_CHANCE / CRITICAL_FAILURE_CHANCE and `AIDegrade`

Both live on `FightSO.enemyData`, tunable per fight. A stupidity-affected decision builds a **ranked
list** of candidates (best → worst; ties within the same score shuffled randomly), then hands it to:

```csharp
public static T Resolve<T>(IReadOnlyList<T> rankedCandidates, int stupidityChance, int criticalFailureChance)
{
    int index = 0;
    while (true)
    {
        if (!AIController.RollForProbability(stupidityChance))
            return rankedCandidates[index];
        if (AIController.RollForProbability(criticalFailureChance))
            return rankedCandidates[Random.Range(0, rankedCandidates.Count)];
        index++;
        if (index >= rankedCandidates.Count)
            return rankedCandidates[rankedCandidates.Count - 1];
    }
}
```

Roll stupidity; if it doesn't trigger, take the current best. If it triggers, roll critical failure;
if THAT triggers, pick a fully random candidate from the whole list. If not, move one step down the
ranked list and repeat — so a decision can degrade several steps on a bad enough luck streak, capped
at the bottom of the list.

**Binary decisions** (should I summon? should I reroll?) model themselves as a 2-element list
(`[desired, opposite]`) — there's no "second-best" beyond the opposite outcome, so a stupidity trigger
without critical failure just flips it.

**Decisions marked NO STUPID** skip `AIDegrade` entirely and return their forced/top pick directly.
Two exist today, both in `ShouldSummonCreaturesDecision`: the literal first-turn rule
(`GameManager.IsFirstRound`), and the board-full short-circuit (`AIController.EnemyBoardFull` — every
native creature slot already occupied, nothing left to summon). Both are genuine impossibilities/
certainties, not "desires" stupidity should be able to override.

### `ShouldSummonCreaturesDecision`

Returns a `SummonChoice` (`ShouldSummon` + `RepairTarget`), not a bare bool — see "`SummonChoice`:
threading the repair target through" below for why. `Decide()` itself is kept deliberately narrative:
a short chain of early returns with no inline comparisons, each branch delegating its actual logic to
a small helper:

```csharp
public SummonChoice Decide()
{
    if (GameManager.Instance.IsFirstRound) return new SummonChoice(true, null);   // NO STUPID
    if (TryRepairShockedCreature(out var repair)) return repair;
    if (AIController.EnemyBoardFull) return new SummonChoice(false, null);        // NO STUPID
    return new SummonChoice(Degrade(DesiredSummon()), null);
}
```

`TryRepairShockedCreature` (the "try" pattern, mirroring `TryGetValue`-style APIs) is how the shocked-
repair path is threaded in: it's a no-op (`return false`) when nothing's shocked, so the ordinary
board-full/count-bucket flow below falls through untouched. When the AI's own native creatures are
shocked (stunned), **this takes priority over everything else, including the board-full check** —
rolling a creature's own type heals/promotes it (`SpawningState.HandleExistingCreature`) which also
cures its shock, since both promotion and healing unconditionally clear all statuses
(`StatusesManager.ClearAllStatuses`, wired to `Experience.OnPromoted` and `Health.onHealed`). So
whenever `AIController.ShockedEnemyCreatures` is non-empty, board-full is skipped entirely (repairing
needs no free slot), the repair target is the highest-current-HP shocked creature (`BestToRepair`),
and desire is a shocked-count/level-keyed probability roll instead of the normal count buckets: 3
shocked → always desired before degrade; 2 shocked → 80%; exactly 1 shocked → keyed by that
creature's level (≥4 → 80%, 3 → 50%, 1/2 → never — placeholder, spec gave no number here, retune
freely). Constants live on `ShouldSummonCreaturesDecision` (`TwoShockedRepairChance`/
`OneShockedLevel3RepairChance`/`OneShockedLevel4RepairChance`).

Only when nothing is shocked does the normal flow apply: board full → false (NO STUPID), else a
per-creature-count-bucket probability roll (0 creatures → always desired true before degrade; 1
creature and behind the player → 60%; 2 creatures and behind → 40%; otherwise →
`DefaultSummonChance` = 15, a placeholder — the spec never gave a number for "behind by count but not
by the specific 1-or-2 buckets," retune this constant freely). Either way the result is degraded via
the shared `Degrade(bool desired)` helper (the `[desired, !desired]`/`AIDegrade.Resolve` binary
pattern, factored out so both branches — and `TryRepairShockedCreature` — share one copy).

"Board full" (native slots) and "creature count" (native + charm slots) are deliberately different
checks — `EnemyBoardFull` only cares whether there's a free native slot to summon into; the count
buckets compare total board strength against the player. The shocked-repair path is a third, higher-
priority signal layered on top of both.

### `SummonChoice`: threading the repair target through

`_AI/SummonChoice.cs` — `readonly struct { bool ShouldSummon; CreatureSO RepairTarget; }`, mirroring
`RerollChoice`'s shape. `ShouldSummonCreaturesDecision` is the **only** place that picks which shocked
creature to repair (`BestToRepair` — highest current HP); it hands that choice forward as
`RepairTarget` rather than `PreferredCreatureTypeDecision` re-deriving it independently. `RollState`
reads `summon.RepairTarget` and passes it straight into
`AIController.DecidePreferredCreatureType(repairTarget, excludeCreature)` — one source of truth for
"which creature," never recomputed a second time.

### `PreferredCreatureTypeDecision`

Ranked list, top-priority first: `repairTarget` (as handed in by `ShouldSummonCreaturesDecision` via
`SummonChoice`, null on an ordinary summon) → Tank (if the AI has 0 creatures) → Archer (if any player
creature is below 30% hp) → Mage (if the player hero is below 30% hp), each of the latter three only
added if the AI doesn't already own that type; then any remaining not-yet-listed, not-owned types as a
no-signal fallback, in `CreaturesManager.AllSlots()`'s own tank→mage→archer order. Degraded via
`AIDegrade`. Only ever called when `ShouldSummonCreaturesDecision` returned a truthy `SummonChoice`, so
the ranked list is guaranteed non-empty — see the invariant comment on `BuildRankedList` for why this
still holds now that `repairTarget` (an *owned* type, unlike every other candidate here) can be the
only entry.

### `PickActionDecision`

Scores all 6 of the AI's nukes+spells together (`G.EnemyNukes.nukeA/B/C`, `G.EnemySpells.spellA/B/C`)
via each one's `CreateAIScorer().Score()`, ranks descending (ties shuffled), degrades. The caller
(`RollState`) derives `RollType` from whether the winning `ActionSO` is a `NukeSO` or `SpellSO` — this
decision doesn't need to know about roll types itself.

### The "don't repeat a just-tripled action" rule

If the AI's own roll lands a triple, the existing `TripleRolled` mechanic
(`RollStateManager`/`GameManager.TakeTurn()`, `docs/GameLoop.md`) grants a bonus replay roll. Per
spec, the AI must never go for the *same* action again on that bonus roll. `RollState.BeginAITurn()`
detects this (`RollStateManager.Instance.TripleRolled` is still true when a new `RollState` starts,
since `TakeTurn()`'s do-while loop only re-enters while it's true) and reads which action just
tripled off whichever `Entries` list matches `LastRollType`, then passes it as `excludeCreature`/
`excludeAction` into `PreferredCreatureTypeDecision.Decide()`/`PickActionDecision.Decide()`, which
filter it out of their candidate pool before scoring. For creatures this is usually redundant with
the "already owned" filter (the tripled type is promoted before the bonus roll's decision runs, via
`SpawningState` completing first in the same `TakeTurn()` iteration) — kept for symmetry and because
it's genuinely load-bearing for nukes/spells, which have no "already owned" concept at all.

### Rerolls: `RerollBudgetDecision` + `ShouldRerollDecision`

**The fight-wide pool, not a per-turn cap, is what actually limits the AI.** `EnemyData.rerollsAmount`
(default 10) is a budget that depletes by 1 every time the AI actually rerolls, persists across all
of the AI's turns for the whole fight, and only resets when the battle restarts.

**The per-turn base budget is a formula, not a fixed constant or a match-count tier.** Design
deliberately balances each fight's `EnemyData.hp` against `rerollsAmount` at roughly a 10:1 ratio (e.g.
100 hp / 10 rerolls), so `RerollBudgetDecision.Decide()` reads that ratio against how badly the AI is
actually doing — and lets it conserve its pool while it's fine, spiking the per-turn budget the more
it's losing. **"How badly it's doing" is two things, not one: HP already lost AND board power it's
behind on.**

```csharp
private const float HealthPerReroll = 10f;
private const float HealthPerFirepowerPoint = 1f;

public int Decide()
{
    int pool = AIController.RerollsRemaining;
    int desiredBudget = Mathf.Max(1, pool - HealthReserve());
    return Mathf.Min(desiredBudget, pool); // "at least 1" must never exceed what's left in the pool
}

private static int HealthReserve() => Mathf.FloorToInt(EffectiveHealth() / HealthPerReroll);

private static float EffectiveHealth() => AIController.EnemyHeroCurrentHealth - FirepowerDeficitAsHealth();

private static float FirepowerDeficitAsHealth() =>
    Mathf.Max(0f, AIController.PlayerFirepowerLead) * HealthPerFirepowerPoint;
```

`EffectiveHealth()` reads **raw current HP points** (`AIController.EnemyHeroCurrentHealth`, backed by
`Health.CurrentHealth`) rather than a percent — the one HP check in this system that isn't
percent-based, because the formula is only meaningful against the fight's own hp:rerolls ratio.
Computed once per turn, at `RollState.HandlePostRollsEnter()`.

#### The board-power deficit term

**Every 10 points the player's board out-guns the AI's counts exactly like 10 HP the AI has already
lost.** An AI at full HP staring down a board that badly out-guns its own is about to *become* hurt, so
it stops hoarding rerolls now rather than after the damage lands. Worked example: enemy at 120 hp with
14 rerolls left reserves 12 and gets a budget of **2**; with the player's board at 30 power against its
own 10, the +20 deficit reads as 100 effective hp, reserving 10 for a budget of **4**.

- "Power" is `AIController.PlayerFirepowerLead` → `AttacksResolver.OpponentFirepowerAdvantage(false)`,
  the **same** number `SlotMachineRigger` feeds `FightSO`'s comeback ladder — deliberately one shared
  measure (rule 22), so the comeback rigging and the AI's reroll spending can never disagree about who
  is ahead. Its exact formula (expected damage output per battle phase, crits at expected value,
  shocked creatures counting 0, minus the Shield in that side's way, floored at 0) and what it
  deliberately excludes (`specialDamageModifiers`, e.g. ShieldBreaker) live in `docs/Battle.md`.
- **It's one-way.** `Mathf.Max(0f, ...)` clamps the lead, so the AI being *ahead* on power never grows
  the reserve past what its HP alone dictates — a winning AI plays at its HP-derived budget, it doesn't
  get stingier than that. The accessor itself stays signed (mirroring `OpponentFirepowerAdvantage`'s own
  semantics); the one-way policy belongs to this decision, not to the primitive.
- **A player Shield inflates the lead**, because effective firepower subtracts the barrier facing each
  side: a big player Shield drives the enemy's effective firepower toward 0, widening the player's lead
  and handing the AI more rerolls. Intended — "I'm being walled, roll harder."
- **Neither end needs new clamping.** If the lead exceeds the AI's remaining HP, `EffectiveHealth()`
  goes negative and `pool - HealthReserve()` exceeds `pool` — `Decide()`'s pre-existing
  `Mathf.Min(desiredBudget, pool)` caps that at the whole remaining pool, and its `Mathf.Max(1, ...)`
  still guarantees the AI can always chase a pair.
- **Timing: it reads the board the turn *started* with.** `HandlePostRollsEnter` fires when the enemy's
  reels land, which is before `SpawningState` adds whatever this turn's roll just summoned — the same
  board state `SlotMachineRigger` read at the top of the turn. So the budget is set against the board
  the AI is actually facing right now, not one that includes its own not-yet-spawned reinforcement.
- Both weights are named `const`s on `RerollBudgetDecision` (`HealthPerReroll = 10f`,
  `HealthPerFirepowerPoint = 1f`), not per-fight `EnemyData` fields — retune them there. Making them
  per-fight would mean an asset migration pass, since a new `EnemyData` field silently backfills as 0
  on every existing `FightSO` (see the Gotchas below).

**Unlike before, the per-turn budget isn't fixed for the rest of the turn — it can grow by +1 once**,
a bonus granted the moment the AI observes it's completed the desired action's own pair while chasing
it (see below). `RollState` owns `_rerollBudget` as a genuinely mutable per-turn field for this reason.

#### The three-state per-turn reroll process

`ShouldRerollDecision` models the whole turn as a small one-way state machine,
`ShouldRerollDecision.RerollMode` (`Open` → `CommittedToPair` **or** `CommittedToDesired`, never
between the two committed modes). `RollState` owns the live value (`_rerollMode`) and passes it in on
every call — `Decide()` stays a pure function of its arguments, same reasoning as
`_rerollBudget`/`_rerollsUsed` already required:

```csharp
public RerollChoice Decide(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction,
    int rerollsUsedSoFar, int rerollBudget, RerollMode mode, bool bonusAlreadyGranted)
{
    mode = DesperateOverride(mode);
    bool grantBonus = ShouldGrantBonus(currentSlots, desiredAction, mode, bonusAlreadyGranted);
    int budget = EffectiveBudget(rerollBudget, grantBonus);

    if (OutOfRerolls(rerollsUsedSoFar, budget)) return RerollChoice.Finish(mode, grantBonus);
    if (IsOpen(mode)) return DecideOpen(currentSlots, desiredAction, budget);
    if (IsChasingPair(mode)) return DecideChasingPair(currentSlots, grantBonus);
    return DecideCommitted(currentSlots, desiredAction, grantBonus);
}
```

**Which of the two committed modes the turn ends up in is decided once, on the turn's very first
decision, and never revisited.** `Open` is the only mode that can branch into fishing for the desired
action at all — so the desired-fishing branch is entered only when (1) stupidity declined the pair
chase before any chase reroll had fired, or (2) the initial landing had no pair to chase in the first
place. Once a chase reroll has actually fired, the turn is locked into chasing that pair. Giving up on
a triple the AI had visibly started going for, and spending the remaining budget fishing instead, is
exactly what reads as stupid to a watching player.

**The bonus-eligibility check runs *before* the exhausted-budget check, not after** — `grantBonus` is
computed first and folded into `budget` before `OutOfRerolls` ever looks at it. This is deliberate:
it's what lets the bonus still fire, and its extra reroll still get used, even when the very reroll
that completes the desired pair is also the one that would otherwise have exhausted the base budget
(e.g. base budget 2, the 2nd of those 2 rerolls is the one that lands the pair — checking exhaustion
first would silently end the turn right there and the bonus would never fire).

**`RerollMode.Open`** (the default, reset at the start of every turn) — re-evaluated fresh at every
decision point:

- If **any** pair exists among the 3 slots (`TryFindPairedOddSlot`, regardless of whether it's the
  desired action), chase it: reroll the odd slot, gated by stupidity. **The instant this chase reroll
  actually fires, mode transitions to `CommittedToPair`** and the turn keeps chasing that same pair
  from then on. A declined chase attempt (stupidity blocks it) falls through to the fishing branch
  below instead.
- Otherwise (no pair, or the chase attempt above was declined by stupidity) — the guaranteed base-1
  minimum is reserved for chasing an existing pair only, never for fishing from scratch, so first check
  `rerollBudget > 1`. If that fails, finish. If it passes, attempt to fish (reroll a random
  non-desired-action slot, `NonMatchingIndices`), gated by stupidity — and **transition to
  `CommittedToDesired` the instant this fishing attempt is made**, whether or not the stupidity roll
  actually lets it through.

**`RerollMode.CommittedToPair`** (one-way, and the only mode entered by an actual pair chase) —
re-evaluated fresh at every decision point, but there is only one thing to evaluate: find the pair
again and reroll its odd slot, gated by stupidity as always, until the triple lands or the budget runs
out. It never fishes.

- Rerolling the odd slot can't break the pair, so the pair is always still there next decision — the
  "no pair found" branch only exists for the triple itself, which `SlotMachine` auto-finishes
  upstream before this class runs again.
- **The odd slot landing on the desired action does not stop the chase** — it gets rerolled away like
  anything else. A triple is worth more than a single desired slot while budget remains, and this is
  the deliberate resolution of a case that has been caught live from both directions (an earlier
  version transitioned to `CommittedToDesired` after a chase instead, which produced the opposite
  and worse artifact: with `[Tank, Tank, Archer]` and Archer desired, it fished by rerolling one of
  its own **Tanks**, visibly abandoning the triple it had just spent a reroll on).
- If the pair being chased happens to *be* the desired action's pair, the one-time bonus reroll below
  still fires, exactly as it does in `CommittedToDesired`.

**`RerollMode.CommittedToDesired`** (one-way under normal circumstances — `Open`'s "any pair" priority
never applies again this turn once entered, *except* the desperate-mode override below) — re-evaluated
fresh at every decision point:

- If the desired action specifically has a pair (2 of 3 slots), chase it exactly like `Open`'s pair
  chase (same `TryFindPairedOddSlot` target). **The first time this is observed this turn** (tracked
  via `RollState`'s `_bonusGranted` flag), this also grants the one-time +1 `_rerollBudget` bonus —
  regardless of whether this particular chase attempt itself succeeds or is declined by stupidity.
- Otherwise, **ignore any incidental non-desired pair entirely** ("considers only desired triple") and
  keep fishing, exactly like `Open`'s fishing (same `NonMatchingIndices` target selection) — this
  deliberately does not protect an incidental pair formed as a side effect of an earlier fishing
  reroll from being touched.

**Desperate-mode override — `DesperateOverride()`, checked first, before anything else in `Decide()`.**
Below `lowHpStupidityBypassThreshold` (see below), the AI stops caring specifically about its desired
action even after committing to fish for it: `Decide()` overrides `mode` back to `Open` for that
decision (and therefore for `RollState`'s persisted `_rerollMode` too, since it flows through the
returned `RerollChoice.NextMode`) whenever `mode == CommittedToDesired` and the enemy is below
threshold — so a desperate AI reroll-chases *any* pair it finds, including one that isn't its desired
action, rather than keep fishing specifically. A no-op when already `Open` (which already has this
priority), when already `CommittedToPair` (chasing a pair is what desperation wants anyway — and once
the override drops a desperate AI back into `Open`, chasing a pair from there commits it to
`CommittedToPair` for the rest of the turn like any other chase), or when not desperate. Checked
*before* `ShouldGrantBonus`, so a desperate AI chasing an
unrelated pair never accrues the desired-pair bonus meant for `CommittedToDesired`. Worked example
(desired = Tank): lands `[Tank, Archer, Mage]` (no pair, fishes, rerolls Archer) → lands
`[Tank, Mage, Mage]` (a Mage pair, not desired) → while below threshold, this rerolls the *Tank* slot
chasing the Mage pair instead of continuing to fish for Tank.

The mechanical target-selection (`TryFindPairedOddSlot` for chasing, random `NonMatchingIndices` for
fishing) is identical in every mode and lives in two shared helpers, `ChasePair()` and `Fish()` (plus
the shared `TryFindPairedOddSlot`) — only which mode applies, and the transition/bonus bookkeeping
around it, differs between `DecideOpen`/`DecideChasingPair`/`DecideCommitted`.

**HP below `FightSO.enemyData.lowHpStupidityBypassThreshold` (0-1, baseline 0.15 = 15%) bypasses
stupidity for every individual reroll attempt, in both modes** — "he rolls for his life." Per-fight
tunable (not a hardcoded constant) so a specific boss can be made more or less reckless near death;
every existing `FightSO` asset has it explicitly set to 0.15 to preserve the original baseline (see
the Gotchas entry below about new `EnemyData` fields defaulting to 0 otherwise). Every stupidity roll
in this class funnels through one helper:

```csharp
private static bool AttemptReroll()
{
    var fight = CampaignStateManager.Instance.CurrentFight.enemyData;
    if (AIController.EnemyHeroBelow(fight.lowHpStupidityBypassThreshold)) return true;
    return AIDegrade.Resolve(new[] { true, false }, fight.stupidityChance, fight.criticalFailureChance);
}
```

Below that threshold of its own max HP, the AI always proceeds with whatever reroll attempt it's
making — no `AIDegrade` roll at all. Deliberately a different (percent-based, per-fight-authored)
threshold from the raw-points budget formula above; the two aren't related and shouldn't be conflated.

**The old "exactly 1 desired match with no pair = forced no-reroll, NO STUPID" special case is gone.**
Under the new rules, a lone desired match sitting next to 2 different singles, with `rerollBudget > 1`,
actively fishes instead of banking the single match.

`AIController.SpendReroll()` is still called exactly once per actual reroll, by `RollState` right at
the point it commits to triggering one — never inside a `Decide*` method, so decisions themselves stay
free of side effects.

**UI: `EnemyRerollDisplay`** (`_UI/EnemyRerollDisplay.cs`, on `Canvas/EnemyRerollCount` in
`BattleScene.unity`, a prefab variant of `EnergyCount.prefab`) mirrors the pool onto the HUD, the same
way `EnergyDisplay` mirrors the player's energy (`docs/Energy.md`). Both now share an abstract base,
`_UI/RerollResourceDisplay.cs` — it owns the text/feedback fields and the "update text on every
change, play feedback only on an actual spend" `Start()`/`OnDestroy()` wiring; each subclass just
points `CurrentValue` and `Subscribe()`/`Unsubscribe()` at its own controller's events
(`EnergyController.OnEnergyChanged`/`OnEnergySpent` vs `AIController.OnRerollsChanged`/
`OnRerollSpent`).

## Scoring: `ActionAIScorer` and how to add AI scoring to a new nuke/spell

`ActionAIScorer` (`_AI/_Scoring/ActionAIScorer.cs`) is the shared, readable query surface every
concrete scorer is built on — the direct answer to "don't want to see data plumbing in concrete
scorers." Every raw `G`/`Health`/`CreaturesManager` read lives here exactly once, named for what it
means:

```csharp
protected static bool PlayerHpPercentageBelow(int percent);   // and PlayerHpPercentageMore, Enemy* variants
protected static bool PlayerHasShield => ...;                  // and EnemyHasShield
protected static bool PlayerHasTank/Mage/Archer => ...;        // and Enemy* variants
protected static Creature PlayerMage/PlayerArcher => ...;      // native-slot occupant, for per-unit checks
protected static int PlayerCreatureCount/EnemyCreatureCount => ...;
protected static int CountPlayerCreaturesBelow(int percent);   // and CountEnemyCreaturesBelow
protected static IReadOnlyList<Creature> PlayerCreatures => ...;
protected static bool CreatureHpBelow(Creature creature, int percent);
protected static int ClampScore(int score);                    // Mathf.Clamp(score, 0, 100)
```

A concrete scorer's `Score()` should read as a sequence of these named checks — see
`FireMagicAIScorer`/`ShockAIScorer`/etc. for worked examples. The one exception: a formula's own
*unique* logic (e.g. `ShockAIScorer` reading `creature.Experience.Level` per creature) stays inline
rather than becoming another base-class helper used nowhere else — the line between "generic data
access" (belongs on the base) and "this action's specific formula" (belongs in the concrete class) is
the guide.

The simplest case of a scorer that must agree with its resolver is `ShockAIScorer`, whose first line
is `if (PlayerHasShield) return 0;` — a standing shield blocks Shock outright at every level
(`docs/ActionsAndSpells.md` §2c), so a non-zero score there would be the AI knowingly wasting a roll.
Any change to that gameplay rule has to be mirrored here.

### A scorer that has to agree with its own resolver: `CharmAIScorer`

Most scorers read only board state. `CharmAIScorer` is the exception worth copying when a formula
needs to stay in step with the action's real odds: Charm's success chance is cut 25% per creature the
caster out-numbers the charmed side by (`docs/ActionsAndSpells.md` §3c), so an AI that kept rating
Charm on board size alone would keep casting a spell whose chance had quietly collapsed.

It therefore takes its `CharmSO` (`new CharmAIScorer(this)` from `CharmSO.CreateAIScorer()`) and
scales its score by the **same** `CharmSO.GetCreatureAdvantageMultiplier(...)` the resolver uses:

```csharp
public override int Score()
{
    if (PlayerCreatureCount == 0) return 0;                       // nothing to steal
    return ClampScore(Mathf.RoundToInt(StealValue() * AdvantagePenalty()));
}
```

`StealValue()` is the old board-size appetite (`1 => 10, 2 => 50, 3 => 95, _ => 100`);
`AdvantagePenalty()` is `_source.GetCreatureAdvantageMultiplier(EnemyCreatureCount, PlayerCreatureCount)`.
One tunable (`chancePenaltyPerExtraCreature`) now moves the odds and the AI's appetite together.

This replaced a flat `- 5 * EnemyCreatureCount` term, which penalised the AI for simply *having* a
board rather than for out-numbering the player — the thing that actually reduces the chance. Example
measured live: player board 1, AI board 3 → `StealValue(1) = 10`, advantage 2 → ×0.5 → score **5**.

Pass the SO in like this only when the scorer genuinely needs the action's own balance numbers;
board-state-only scorers stay parameterless.

**To add AI scoring to a new nuke/spell:**

1. Write a scorer class in `_AI/_Scoring/`, subclassing `ActionAIScorer`, implementing
   `public override int Score()` using the base class's named helpers (add a new helper there if you
   need board state no existing one exposes — don't reach `G`/`Health`/`CreaturesManager` directly
   from the concrete scorer).
2. Override `CreateAIScorer()` on the new nuke/spell's `NukeSO`/`SpellSO` subclass:
   `public override ActionAIScorer CreateAIScorer() => new MyNewAIScorer();` — one line, same shape
   as the existing `CreateResolver()` override (`docs/ActionsAndSpells.md`). Pass `this` if the
   scorer needs the SO's balance numbers, as `CharmSO` does.
3. That's it — `PickActionDecision` picks it up automatically the next time it scores all 6 of the
   AI's actions; no registration list to update anywhere else.

If you don't override `CreateAIScorer()`, the SO falls back to `NoOpActionAIScorer` (score 0 always)
— same placeholder-safe default shape as `NoOpNukeResolver`/`NoOpSpellResolver`.

## Gotchas

- **Existing `FightSO` assets silently default new fields to 0.** Unity backfills a newly-added
  serialized field on an already-serialized asset with the type default — `rerollsAmount = 0` makes
  the AI never reroll, `stupidityChance = 0` makes it never stupid (perfectly optimal),
  `lowHpStupidityBypassThreshold = 0` silently disables the bypass entirely (an enemy hero's HP can
  never be below 0%), neither with any compile error or console warning. Always double-check real
  values are set in the Inspector after adding a new fight asset or a new `EnemyData` field — every
  existing fight asset already has `lowHpStupidityBypassThreshold` explicitly set to 0.15 for exactly
  this reason.
- **`AIController` never calls `SlotMachine`/`SlotColumn` methods — if you're tempted to add one,
  that call belongs in `RollState` instead**, following the existing `EvaluateReroll`/`BeginAITurn`
  pattern. This is the one rule this whole system exists to enforce; breaking it re-couples decision
  logic to the live slot machine and undoes the headless-testability groundwork.
- **`DefaultSummonChance` (15) is a placeholder**, not a value from the original spec — see
  `ShouldSummonCreaturesDecision`. Same for the "exactly 1 shocked creature, level 1 or 2" case in the
  same class, which the spec left unnumbered and currently always resolves to "not desired."
- **The exclusion fix (see above) only prevents re-selecting the exact same `ActionSO`/`CreatureSO`
  that just tripled — it doesn't prevent a *different* action of the same broad category** (e.g. a
  tripled FireMagic doesn't stop the AI from choosing Starfall on the bonus roll, only FireMagic
  itself).
- **A granted +1 bonus reroll can still hit the fight-wide pool's real floor mid-turn.**
  `RerollBudgetDecision` clamps only the *starting* per-turn budget to the live pool; the one-time
  `CommittedToDesired` bonus can push `_rerollBudget` above what's actually left if earlier rerolls
  this same turn already spent the pool down close to the budget's edge. `ShouldRerollDecision`'s
  `OutOfRerolls()` guards this the same way the old hard-stop did — it checks
  `AIController.RerollsRemaining <= 0` live, not just `rerollsUsedSoFar >= rerollBudget` — so this
  degrades gracefully to "finish now" instead of ever attempting a reroll the pool can't pay for.

## Related docs

- `docs/SlotMachine.md` — `SlotMachine`/`SlotColumn` mechanics this system drives:
  `Columns`/`SetRollType`/`OnRerollResolved`/`TriggerReroll`, and how a roll becomes `WinningAction`s.
- `docs/GameLoop.md` — `GameManager.TakeTurn()`'s `TripleRolled` do-while loop (the "bonus roll"
  mechanic), the `Generation`/`IsStale` staleness guard `RollState` relies on, the restart event.
- `docs/ActionsAndSpells.md` — the `ActionSO`→Resolver→Animation pattern `CreateAIScorer()` mirrors,
  and how to add a new nuke/spell in the first place (before it needs AI scoring at all).
- `docs/Battle.md` — `AttacksResolver`'s firepower estimation trio behind
  `AIController.PlayerFirepowerLead`: the exact per-creature formula, the Shield subtraction, and what
  the estimate deliberately leaves out (so the reroll budget under-counts a ShieldBreaker roster too).
- `docs/Campaign.md` — `FightSO`/`EnemyData`, `CampaignStateManager.CurrentFight`.
