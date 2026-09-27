using System.Collections.Generic;
using UnityEngine;

public class ShouldRerollDecision
{
    /// <summary>The per-turn reroll state machine (see docs/AI.md "Rerolls"). RollState owns the
    /// actual field (_rerollMode) and passes it in every call — Decide() itself stays pure.</summary>
    public enum RerollMode
    {
        /// <summary>Default at the start of every turn, and the ONLY mode that can ever branch into
        /// fishing for the desired action. Chases any existing pair (regardless of action) first;
        /// falls back to fishing only if no pair exists, or if the chase attempt is declined by
        /// stupidity — i.e. only ever on the turn's very first decision.</summary>
        Open,
        /// <summary>Entered the instant a pair chase actually fires from Open, one-way: the AI keeps
        /// rerolling that pair's odd slot for the rest of the turn while budget lasts, and never
        /// derails into fishing for the desired action. A triple is worth more than a single desired
        /// slot, so even the desired action landing in the odd slot gets rerolled away.</summary>
        CommittedToPair,
        /// <summary>Entered the instant a fishing attempt is made from Open, one-way. Only the
        /// desired action's own pair is ever chased from here on; any other incidental pair is
        /// ignored.</summary>
        CommittedToDesired,
    }

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

    // ---- top-level gates ----

    // Below the fight's low-HP threshold, the AI stops caring specifically about its desired action
    // and falls back to "any triple will do" — re-entering Open's any-pair priority even if it had
    // already committed to fishing for something specific. A no-op when already Open (which already
    // has this priority), when already chasing a pair (that IS what desperation wants), or when the
    // enemy isn't desperate. Checked before ShouldGrantBonus below, so a desperate AI chasing an
    // unrelated pair never accrues the desired-pair bonus.
    private static RerollMode DesperateOverride(RerollMode mode)
    {
        if (mode != RerollMode.CommittedToDesired) return mode;
        var fight = CampaignStateManager.Instance.CurrentFight.enemyData;
        return AIController.EnemyHeroBelow(fight.lowHpStupidityBypassThreshold) ? RerollMode.Open : mode;
    }

    // Detected purely from "is the desired pair present right now," checked BEFORE the
    // exhausted-budget gate below — this is what lets the bonus still fire (and its extra reroll
    // still get used) even on what would otherwise be the very last reroll of an exhausted base
    // budget. See docs/AI.md's worked example. Fires in either committed mode: a chased pair that
    // happens to BE the desired action earns the bonus exactly like a fished-up one does.
    private static bool ShouldGrantBonus(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction,
        RerollMode mode, bool bonusAlreadyGranted)
    {
        if (bonusAlreadyGranted) return false;
        if (IsOpen(mode)) return false; // nothing committed to yet — a desperate AI lands here too
        return DesiredPairExists(currentSlots, desiredAction, out _);
    }

    private static int EffectiveBudget(int rerollBudget, bool grantBonus) =>
        grantBonus ? rerollBudget + 1 : rerollBudget;

    private static bool OutOfRerolls(int rerollsUsedSoFar, int rerollBudget) =>
        AIController.RerollsRemaining <= 0 || rerollsUsedSoFar >= rerollBudget;

    private static bool IsOpen(RerollMode mode) => mode == RerollMode.Open;

    private static bool IsChasingPair(RerollMode mode) => mode == RerollMode.CommittedToPair;

    // ---- RerollMode.Open: chase any pair first, else fish for the desired action ----

    private static RerollChoice DecideOpen(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction,
        int rerollBudget)
    {
        if (TryChasePair(currentSlots, out var choice)) return choice;
        if (!CanFishFromScratch(rerollBudget)) return RerollChoice.Finish(RerollMode.Open);
        return Fish(currentSlots, desiredAction, RerollMode.CommittedToDesired);
    }

    private static bool TryChasePair(IReadOnlyList<ActionSO> currentSlots, out RerollChoice choice)
    {
        choice = default;
        if (!TryFindPairedOddSlot(currentSlots, out int oddSlot)) return false;
        if (!AttemptReroll()) return false; // declined — falls through to the fishing gate above
        // Committing here locks the whole rest of the turn into chasing THIS pair
        // (DecideChasingPair): once the AI has visibly started going for a triple, switching to
        // fishing for its desired action mid-turn reads as giving up and then wasting rerolls.
        choice = RerollChoice.Reroll(oddSlot, RerollMode.CommittedToPair);
        return true;
    }

    // The guaranteed base-1 minimum is reserved for chasing an existing pair only — never for
    // fishing from scratch, whether that's a fresh no-pair landing or a declined pair-chase.
    private static bool CanFishFromScratch(int rerollBudget) => rerollBudget > 1;

    // ---- RerollMode.CommittedToPair: keep chasing the committed pair, nothing else ----

    private static RerollChoice DecideChasingPair(IReadOnlyList<ActionSO> currentSlots, bool grantBonus)
    {
        // Rerolling the odd slot can never break the pair, so one is always still here — except on
        // the triple itself, which SlotMachine auto-finishes upstream before this runs again.
        if (!TryFindPairedOddSlot(currentSlots, out int oddSlot))
            return RerollChoice.Finish(RerollMode.CommittedToPair, grantBonus);

        return ChasePair(oddSlot, RerollMode.CommittedToPair, grantBonus);
    }

    // ---- RerollMode.CommittedToDesired: only the desired action's own pair matters from here on ----

    private static RerollChoice DecideCommitted(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction,
        bool grantBonus)
    {
        if (DesiredPairExists(currentSlots, desiredAction, out int oddSlot))
            return ChasePair(oddSlot, RerollMode.CommittedToDesired, grantBonus);

        return Fish(currentSlots, desiredAction, RerollMode.CommittedToDesired); // ignores any incidental non-desired pair
    }

    private static RerollChoice ChasePair(int oddSlot, RerollMode nextMode, bool grantBonus) =>
        AttemptReroll()
            ? RerollChoice.Reroll(oddSlot, nextMode, grantBonus)
            : RerollChoice.Finish(nextMode, grantBonus);

    private static bool DesiredPairExists(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction,
        out int oddSlotIndex)
    {
        if (!TryFindPairedOddSlot(currentSlots, out oddSlotIndex)) return false;
        int pairedSlot = (oddSlotIndex + 1) % currentSlots.Count;
        return currentSlots[pairedSlot] == desiredAction;
    }

    // ---- shared fishing target-selection + stupidity gating ----

    private static RerollChoice Fish(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction, RerollMode nextMode)
    {
        var candidates = NonMatchingIndices(currentSlots, desiredAction);
        if (candidates.Count == 0) return RerollChoice.Finish(nextMode); // already a full desired triple — unreachable in practice, SlotMachine auto-finishes upstream
        int slotIndex = candidates[Random.Range(0, candidates.Count)];
        return AttemptReroll() ? RerollChoice.Reroll(slotIndex, nextMode) : RerollChoice.Finish(nextMode);
    }

    // "He rolls for his life" — below FightSO.enemyData.lowHpStupidityBypassThreshold of its own max
    // HP, every reroll attempt this gates just proceeds, bypassing AIDegrade entirely. Percent-based,
    // unlike RerollBudgetDecision's raw-points formula — the two thresholds serve different purposes
    // and aren't related. Per-fight tunable so a boss can be made more/less reckless near death.
    private static bool AttemptReroll()
    {
        var fight = CampaignStateManager.Instance.CurrentFight.enemyData;
        if (AIController.EnemyHeroBelow(fight.lowHpStupidityBypassThreshold)) return true;
        return AIDegrade.Resolve(new[] { true, false }, fight.stupidityChance, fight.criticalFailureChance);
    }

    private static List<int> NonMatchingIndices(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction)
    {
        var indices = new List<int>();
        for (int i = 0; i < currentSlots.Count; i++)
            if (currentSlots[i] != desiredAction) indices.Add(i);
        return indices;
    }

    // True when exactly 2 of the 3 slots share the same ActionSO — oddSlotIndex is the one that
    // doesn't. Slot count is always 3 in practice (SlotMachine has 3 columns); written generically
    // to match NonMatchingIndices' own IReadOnlyList<ActionSO> shape. Reused unchanged from before.
    private static bool TryFindPairedOddSlot(IReadOnlyList<ActionSO> currentSlots, out int oddSlotIndex)
    {
        for (int odd = 0; odd < currentSlots.Count; odd++)
        {
            int a = (odd + 1) % currentSlots.Count;
            int b = (odd + 2) % currentSlots.Count;
            if (currentSlots[a] == currentSlots[b] && currentSlots[a] != currentSlots[odd])
            {
                oddSlotIndex = odd;
                return true;
            }
        }
        oddSlotIndex = -1;
        return false;
    }
}
