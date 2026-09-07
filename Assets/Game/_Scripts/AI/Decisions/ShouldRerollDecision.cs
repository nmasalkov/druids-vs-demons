using System.Collections.Generic;
using UnityEngine;

public class ShouldRerollDecision
{
    /// <summary>The per-turn reroll state machine (see docs/AI.md "Rerolls"). RollState owns the
    /// actual field (_rerollMode) and passes it in every call — Decide() itself stays pure.</summary>
    public enum RerollMode
    {
        /// <summary>Default at the start of every turn. Chases any existing pair (regardless of
        /// action) first — but only ONCE: the instant a chase reroll actually fires, the next
        /// decision moves to CommittedToDesired regardless of whether the chase completed a triple
        /// (see TryChasePair). Falls back to fishing for the desired action from scratch immediately
        /// if no pair exists or the chase attempt is declined by stupidity.</summary>
        Open,
        /// <summary>One-way — entered the instant a fishing attempt is made from Open, never
        /// reverts. Only the desired action's own pair is ever chased from here on; any other
        /// incidental pair is ignored.</summary>
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
        return DecideCommitted(currentSlots, desiredAction, grantBonus);
    }

    // ---- top-level gates ----

    // Below the fight's low-HP threshold, the AI stops caring specifically about its desired action
    // and falls back to "any triple will do" — re-entering Open's any-pair priority even if it had
    // already committed to fishing for something specific. A no-op when already Open (which already
    // has this priority) or when the enemy isn't desperate. Checked before ShouldGrantBonus below, so
    // a desperate AI chasing an unrelated pair never accrues the desired-pair bonus meant for
    // CommittedToDesired.
    private static RerollMode DesperateOverride(RerollMode mode)
    {
        if (mode != RerollMode.CommittedToDesired) return mode;
        var fight = CampaignStateManager.Instance.CurrentFight.enemyData;
        return AIController.EnemyHeroBelow(fight.lowHpStupidityBypassThreshold) ? RerollMode.Open : mode;
    }

    // Detected purely from "is the desired pair present right now," checked BEFORE the
    // exhausted-budget gate below — this is what lets the bonus still fire (and its extra reroll
    // still get used) even on what would otherwise be the very last reroll of an exhausted base
    // budget. See docs/AI.md's worked example.
    private static bool ShouldGrantBonus(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction,
        RerollMode mode, bool bonusAlreadyGranted)
    {
        if (mode != RerollMode.CommittedToDesired || bonusAlreadyGranted) return false;
        return DesiredPairExists(currentSlots, desiredAction, out _);
    }

    private static int EffectiveBudget(int rerollBudget, bool grantBonus) =>
        grantBonus ? rerollBudget + 1 : rerollBudget;

    private static bool OutOfRerolls(int rerollsUsedSoFar, int rerollBudget) =>
        AIController.RerollsRemaining <= 0 || rerollsUsedSoFar >= rerollBudget;

    private static bool IsOpen(RerollMode mode) => mode == RerollMode.Open;

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
        // One-shot: chasing a pair is only ever attempted once per turn. Committing here — even
        // though the chase might not land the triple — prevents next decision from re-chasing the
        // SAME pair again, which would otherwise keep rerolling away an already-obtained desired
        // action sitting as the odd slot out (a real, live-caught case: chasing a Tank pair kept
        // rerolling the enemy's own just-landed desired Mage instead of banking it).
        choice = RerollChoice.Reroll(oddSlot, RerollMode.CommittedToDesired);
        return true;
    }

    // The guaranteed base-1 minimum is reserved for chasing an existing pair only — never for
    // fishing from scratch, whether that's a fresh no-pair landing or a declined pair-chase.
    private static bool CanFishFromScratch(int rerollBudget) => rerollBudget > 1;

    // ---- RerollMode.CommittedToDesired: only the desired action's own pair matters from here on ----

    private static RerollChoice DecideCommitted(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction,
        bool grantBonus)
    {
        if (DesiredPairExists(currentSlots, desiredAction, out int oddSlot))
            return ChaseDesiredPair(oddSlot, grantBonus);

        return Fish(currentSlots, desiredAction, RerollMode.CommittedToDesired); // ignores any incidental non-desired pair
    }

    private static RerollChoice ChaseDesiredPair(int oddSlot, bool grantBonus) =>
        AttemptReroll()
            ? RerollChoice.Reroll(oddSlot, RerollMode.CommittedToDesired, grantBonus)
            : RerollChoice.Finish(RerollMode.CommittedToDesired, grantBonus);

    private static bool DesiredPairExists(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction,
        out int oddSlotIndex)
    {
        if (!TryFindPairedOddSlot(currentSlots, out oddSlotIndex)) return false;
        int pairedSlot = (oddSlotIndex + 1) % currentSlots.Count;
        return currentSlots[pairedSlot] == desiredAction;
    }

    // ---- shared fishing target-selection + stupidity gating (both modes) ----

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
