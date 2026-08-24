# Enemy AI

## What this system does

Drives the enemy's turn: whether to summon creatures or cast a nuke/spell, which specific action to
go for, and whether/how much to reroll — all computed from weights on the current fight's
`FightSO.enemyData`, with a "stupidity"/"critical failure" mechanism that lets the AI occasionally
miss its own best move. Everything is broken into small, single-purpose **decision classes**
(`Assets/Game/_Scripts/AI/Decisions/`), one command-pattern-style class per choice, each just
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

- `Global/Campaign/FightSO.cs` — `EnemyData.stupidityChance`/`criticalFailureChance` (`[Range(0,100)]`
  ints) and `rerollsAmount` (int, the fight-wide reroll pool — see "Rerolls" below).
- `AI/AIController.cs` — the thin static façade: `RollForProbability`, the `Decide*` methods (each
  just `new XyzDecision().Decide()`), and the fight-wide reroll pool (`RerollsRemaining`/
  `SpendReroll()`) — the one piece of real state this class owns, mirroring how `EnergyController`
  owns the player's energy (not a `SlotMachine` coupling). `OnRerollsChanged(int)`/`OnRerollSpent()`
  (instance events) mirror `EnergyController`'s `OnEnergyChanged`/`OnEnergySpent` split (see
  `docs/Energy.md`'s "Two energy events"): `OnRerollsChanged` fires on every change to the pool (a
  spend or a `ResetRerollPool()` reset), `OnRerollSpent` only on an actual spend — so UI feedback
  plays for real spending only, not for a battle-restart/initial reset.
- `AI/AIController.StateChecks.cs` — partial class (mirrors the `SlotColumn.cs`/
  `SlotColumn.Mechanics.cs` split): `PlayerHeroBelow`/`EnemyHeroBelow`/`AnyPlayerCreatureBelow`/
  `AnyEnemyCreatureBelow` (float 0..1 thresholds), `PlayerCreatureCount`/`EnemyCreatureCount`,
  `EnemyBoardFull`, `ShockedEnemyCreatures` (the AI's own stunned native creatures). These are the
  primitives `Decisions/` classes use directly, and that `Scoring/ActionAIScorer` wraps for its own
  int-percent-based API.
- `AI/AIDegrade.cs` — the one shared stupidity/critical-failure resolver every stupidity-affected
  decision goes through (see "The degrade algorithm" below).
- `AI/RerollChoice.cs` — `readonly struct { bool ShouldReroll; int SlotIndex; }`, the reroll
  decision's return type.
- `AI/SummonChoice.cs` — `readonly struct { bool ShouldSummon; CreatureSO RepairTarget; }`, the
  summon decision's return type — see "`SummonChoice`: threading the repair target through" below.
- `AI/Decisions/` — one class per choice: `ShouldSummonCreaturesDecision`,
  `PreferredCreatureTypeDecision`, `PickActionDecision`, `RerollBudgetDecision`,
  `ShouldRerollDecision`.
- `AI/Scoring/` — `ActionAIScorer` (abstract base, the readable query surface every concrete scorer
  is built on) + `NoOpActionAIScorer` + one concrete scorer per action (`FireMagicAIScorer`,
  `ShockAIScorer`, `StarfallAIScorer`, `ShieldAIScorer`, `CharmAIScorer`, `BattleCryAIScorer`).
- `ScriptableObjects/ActionSO.cs` — `CreateAIScorer()` (virtual, defaults to `NoOpActionAIScorer`),
  the SO→scorer factory every concrete nuke/spell SO overrides in one line, mirroring the existing
  `CreateResolver()` pattern (`docs/ActionsAndSpells.md`).
- `Global/GameManager/RollState.cs` — the orchestrator (see above). Owns the AI's per-turn state
  (`_desiredAction`, `_rerollBudget`, `_rerollsUsed`) as plain fields on the `GameState` instance —
  no restart handling needed, since a fresh `RollState` is `new`'d every `TakeTurn()` iteration and
  `GameManager.RestartBattle()` already tears down the current state before firing
  `OnBattleRestart` (`docs/GameLoop.md`).
- `Global/GameManager/GameManager.cs` — `IsFirstRound` (public property, was a private coroutine
  local before this system), read by `ShouldSummonCreaturesDecision`'s NO-STUPID first-turn rule.
- `Global/RollStateManager/RollStateManager.cs` — `ActiveMachine` (computed property, tracks
  `GameManager.ActiveSide` live — rule 22, one source of truth) replaces the old
  `AIController.Instance.TakeControl`/`ReleaseControl` coupling entirely.
- `Units/Health.cs` — `HealthPercent` (0..1 float), the primitive every HP-threshold check in this
  system is ultimately built on.

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

`AI/SummonChoice.cs` — `readonly struct { bool ShouldSummon; CreatureSO RepairTarget; }`, mirroring
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
of the AI's turns for the whole fight, and only resets when the battle restarts. Once it hits 0, the
AI simply can't reroll anymore — `RerollBudgetDecision` always returns 0 from that point on, and
`ShouldRerollDecision` hard-stops (no degrade — nothing left to spend, not a "desire").

The **per-turn base cap** is a fixed constant, `RerollBudgetDecision.BaseMaxRerollsPerTurn = 2`
(the spec's literal "2" — no longer a per-fight-tunable field). Computed once, off the *initial*
landed slots, based on how many of the 3 match the desired action: 0 matches → up to the base cap;
exactly 1 → 1; a pair (2) → up to the base cap again (aggressively chasing the triple, since it
grants a bonus turn). A low-hp bonus can push the total **above** the base cap (and even above what's
normally expected), right up until the pool itself runs out: either side below 15% hp → +2; below
25% (not also below 15%) → +1; tiers, not additive.

`ShouldRerollDecision` is called once after the initial landing and again after every reroll settles.
**Exactly 1 landed slot matching the desired action is a forced no-reroll (NO STUPID)** — the AI
already has what it wants and just takes it, no degrade involved. Only the 0-match (nothing desired
yet) and 2-match (chasing the triple) cases go through the normal stupidity/critical-failure treatment
— a `[true, false]` binary degrade — so the AI can occasionally finish when it should've rerolled, or
vice versa on critical failure (a coin-flip), in those two cases only.

`AIController.SpendReroll()` is called exactly once per actual reroll, by `RollState` right at the
point it commits to triggering one — never inside a `Decide*` method, so decisions themselves stay
free of side effects.

**UI: `EnemyRerollDisplay`** (`UI/EnemyRerollDisplay.cs`, on `Canvas/EnemyRerollCount` in
`BattleScene.unity`, a prefab variant of `EnergyCount.prefab`) mirrors the pool onto the HUD, the same
way `EnergyDisplay` mirrors the player's energy (`docs/Energy.md`). Both now share an abstract base,
`UI/RerollResourceDisplay.cs` — it owns the text/feedback fields and the "update text on every
change, play feedback only on an actual spend" `Start()`/`OnDestroy()` wiring; each subclass just
points `CurrentValue` and `Subscribe()`/`Unsubscribe()` at its own controller's events
(`EnergyController.OnEnergyChanged`/`OnEnergySpent` vs `AIController.OnRerollsChanged`/
`OnRerollSpent`).

## Scoring: `ActionAIScorer` and how to add AI scoring to a new nuke/spell

`ActionAIScorer` (`AI/Scoring/ActionAIScorer.cs`) is the shared, readable query surface every
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

**To add AI scoring to a new nuke/spell:**

1. Write a scorer class in `AI/Scoring/`, subclassing `ActionAIScorer`, implementing
   `public override int Score()` using the base class's named helpers (add a new helper there if you
   need board state no existing one exposes — don't reach `G`/`Health`/`CreaturesManager` directly
   from the concrete scorer).
2. Override `CreateAIScorer()` on the new nuke/spell's `NukeSO`/`SpellSO` subclass:
   `public override ActionAIScorer CreateAIScorer() => new MyNewAIScorer();` — one line, same shape
   as the existing `CreateResolver()` override (`docs/ActionsAndSpells.md`).
3. That's it — `PickActionDecision` picks it up automatically the next time it scores all 6 of the
   AI's actions; no registration list to update anywhere else.

If you don't override `CreateAIScorer()`, the SO falls back to `NoOpActionAIScorer` (score 0 always)
— same placeholder-safe default shape as `NoOpNukeResolver`/`NoOpSpellResolver`.

## Gotchas

- **Existing `FightSO` assets silently default new fields to 0.** Unity backfills a newly-added
  serialized field on an already-serialized asset with the type default — `rerollsAmount = 0` makes
  the AI never reroll, `stupidityChance = 0` makes it never stupid (perfectly optimal), neither with
  any compile error or console warning. Always double-check real values are set in the Inspector
  after adding a new fight asset or a new `EnemyData` field.
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

## Related docs

- `docs/SlotMachine.md` — `SlotMachine`/`SlotColumn` mechanics this system drives:
  `Columns`/`SetRollType`/`OnRerollResolved`/`TriggerReroll`, and how a roll becomes `WinningAction`s.
- `docs/GameLoop.md` — `GameManager.TakeTurn()`'s `TripleRolled` do-while loop (the "bonus roll"
  mechanic), the `Generation`/`IsStale` staleness guard `RollState` relies on, the restart event.
- `docs/ActionsAndSpells.md` — the `ActionSO`→Resolver→Animation pattern `CreateAIScorer()` mirrors,
  and how to add a new nuke/spell in the first place (before it needs AI scoring at all).
- `docs/Campaign.md` — `FightSO`/`EnemyData`, `CampaignStateManager.CurrentFight`.
