using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

/// <summary>
/// Enemy side data for one fight: which avatar to spawn and how much HP it has. Expandable
/// later (e.g. an AI tactic enum) without touching FightSO itself. See docs/Encounters.md.
/// </summary>
[Serializable]
public struct EnemyData
{
    [Tooltip("Enemy HeroView prefab spawned for this fight (replaces the scene's default avatar).")]
    public GameObject enemyAvatarPrefab;
    [Tooltip("Enemy hero's max HP for this fight.")]
    public int hp;
    [Tooltip("Creature pool this fight's enemy rolls from. If left empty, falls back to " +
             "G.DefaultCreatures (the player's own resolved pool) — see CLAUDE.md rule 29.")]
    public CreaturesSO creatures;

    [Tooltip("Weight (0-100) that the AI won't go with its calculated best decision and degrades to " +
             "the next-best one instead (see AIDegrade). Decisions marked NO STUPID ignore this.")]
    [Range(0, 100)] public int stupidityChance;
    [Tooltip("Rolled only if stupidity triggers. If it also triggers, the AI picks a fully random " +
             "option instead of degrading to the next-best one.")]
    [Range(0, 100)] public int criticalFailureChance;
    [Tooltip("Total rerolls the AI can spend across the WHOLE fight (not per turn) - depletes as it " +
             "rerolls and never refills until the fight restarts. See docs/AI.md.")]
    public int rerollsAmount;
    [Tooltip("Enemy hero HP% (0-1, e.g. 0.15 = 15%) below which reroll decisions stop rolling " +
             "stupidity/critical-failure entirely and always proceed ('rolls for his life') — see " +
             "ShouldRerollDecision.AttemptReroll, docs/AI.md. Baseline is 0.15; override per fight to " +
             "make a boss more/less reckless near death.")]
    [Range(0f, 1f)] public float lowHpStupidityBypassThreshold;
}

/// <summary>
/// Data for one campaign fight: which enemy to face, and the optional pre/post phases wrapped
/// around it. fightId/isTutorial aren't consumed by any logic yet — forward-looking data for a
/// future pre-battle phase. The sole EncounterListSO entry type — LoadoutPickSO/RewardPickSO were
/// folded into hasLoadoutPick/hasReward/rewardAmount below since they never carried enough unique
/// data to justify being separate list entries. Renamed from BattleSO — campaign-layer naming only;
/// the per-turn combat-resolution machinery (BattleState, RunBattle(), AttacksResolver) keeps its
/// own "Battle" naming, unrelated to this. See docs/Encounters.md.
/// </summary>
[MovedFrom(true, sourceClassName: "BattleSO")]
[CreateAssetMenu(fileName = "Fight", menuName = "Game/Campaign/Fight")]
public class FightSO : ScriptableObject
{
    [Tooltip("Stable identifier for this fight. Not read by any logic yet — reserved for a future " +
             "pre-battle phase/save data to reference this specific fight.")]
    [FormerlySerializedAs("battleId")] public string fightId;
    [Tooltip("Marks this as the tutorial fight. Not read by any logic yet — reserved for future " +
             "tutorial-specific behavior.")]
    public bool isTutorial;
    [Tooltip("The enemy side's data for this fight — avatar, HP, creature pool, AI tuning.")]
    public EnemyData enemyData;

    [Tooltip("If true, a loadout-pick phase plays in MapScene, in place at this node, before advancing into the fight.")]
    public bool hasLoadoutPick;
    [Tooltip("If true, a reward-pick phase plays as an overlay inside BattleScene right after victory, before this encounter completes.")]
    public bool hasReward;
    [Tooltip("Guaranteed energy reward granted when the reward phase plays. Only meaningful when hasReward is true.")]
    public int rewardAmount;

    [Header("Dirty Triple Index — Base Values")]
    [Tooltip("The neutral baseline a side's Dirty Triple Index computes from before HP-based " +
             "adjustment is applied, on any turn past round 1. 100 = genuine unbiased 1-in-3 odds " +
             "a dirty triple situation (2 of 3 slots already matching) completes. Applied to " +
             "SlotMachineRigger.neutralDirtyTripleIndex at fight start. See docs/SlotMachine.md.")]
    public int neutralDirtyTripleIndex = 100;
    [Tooltip("Forced Dirty Triple Index for whichever side starts a turn while GameManager." +
             "IsFirstRound is still true (covers both sides' opening turns) — skips the HP-based " +
             "formula and neutralDirtyTripleIndex entirely for round 1. Applied to " +
             "SlotMachineRigger.firstRoundDirtyTripleIndex at fight start. See docs/SlotMachine.md.")]
    public int firstRoundDirtyTripleIndex = 50;
    [Tooltip("Dirty Triple Stabilization: subtracted from the acting side's Dirty Triple Index each " +
             "time they re-enter a bonus turn from a triple, preventing an unbounded lucky streak. " +
             "0 (default) disables it entirely. See docs/SlotMachine.md.")]
    public int dirtyTripleStabilization;

    [Header("Clean Triple Index Override")]
    [Tooltip("Per-fight base for SlotMachineRigger.PlayerCleanTripleIndex, applied once at fight " +
             "start. Chance (0-200, same curve as Dirty) a fresh roll short-circuits into an " +
             "instant, all-3-matching triple before any per-slot decision runs. 100 = neutral " +
             "33.3%, 0 = never, 200 = always. See docs/SlotMachine.md.")]
    public int playerCleanTripleIndex = 100;
    [Tooltip("Same as playerCleanTripleIndex, but for SlotMachineRigger.EnemyCleanTripleIndex.")]
    public int enemyCleanTripleIndex = 100;
    [Tooltip("Forced Clean Triple Index for whichever side starts a turn while GameManager.IsFirstRound " +
             "is still true (covers both sides' opening turns) — skips playerCleanTripleIndex/" +
             "enemyCleanTripleIndex entirely for round 1, mirroring firstRoundDirtyTripleIndex above. " +
             "Applied to SlotMachineRigger.firstRoundCleanTripleIndex at fight start. See docs/SlotMachine.md.")]
    public int firstRoundCleanTripleIndex = 50;
    [Tooltip("Player Clean Triple Stabilization: a SIGNED delta ADDED to the player's current Clean " +
             "Triple Index each time they re-enter a bonus turn from a triple (dirty or clean — " +
             "either kind grants the bonus turn). Enter a NEGATIVE number to stabilize (e.g. -50 " +
             "decreases the index by 50 per bonus-turn re-entry, preventing a lucky streak of " +
             "instant clean triples from snowballing) — unlike dirtyTripleStabilization above, " +
             "which is always a positive magnitude subtracted, this field's sign controls the " +
             "direction directly. Reset back to playerCleanTripleIndex on the player's next " +
             "genuinely new (non-bonus) turn. Independent of, and stacks separately from, " +
             "dirtyTripleStabilization above. 0 (default) disables it entirely. See docs/SlotMachine.md.")]
    public int playerCleanTripleStabilization;
    [Tooltip("Same as playerCleanTripleStabilization, but for the enemy side and enemyCleanTripleIndex.")]
    public int enemyCleanTripleStabilization;

    [Header("HP-Based Adjustment — Player")]
    [Tooltip("Turns the whole HP-based adjustment mechanism off for the player side's Dirty Triple " +
             "Index — base index just stays at neutralDirtyTripleIndex on any non-first-round turn. " +
             "Clean Triple Index has no HP-based adjustment at all (see playerCleanTripleIndex).")]
    public bool playerHpAdjustmentsEnabled = true;
    [Tooltip("Player-side HP thresholds/adjustments applied to the Dirty Triple Index only — see " +
             "each field's own tooltip inside this foldout.")]
    public HpAdjustmentSettings playerHpAdjustment = HpAdjustmentSettings.Default;

    [Header("HP-Based Adjustment — Enemy")]
    [Tooltip("Turns the whole HP-based adjustment mechanism off for the enemy side's Dirty Triple " +
             "Index — base index just stays at neutralDirtyTripleIndex on any non-first-round turn. " +
             "Clean Triple Index has no HP-based adjustment at all (see enemyCleanTripleIndex).")]
    public bool enemyHpAdjustmentsEnabled = true;
    [Tooltip("Enemy-side HP thresholds/adjustments applied to the Dirty Triple Index only — see " +
             "each field's own tooltip inside this foldout.")]
    public HpAdjustmentSettings enemyHpAdjustment = HpAdjustmentSettings.Default;

    [Header("Ludo Progress Index")]
    [Tooltip("Amount PlayerDirtyTripleIndex/EnemyDirtyTripleIndex increases after each reroll, " +
             "during a side's first (non-bonus) roll phase of its turn only — disabled for the " +
             "rest of that turn once that side gets one triple. 0 = disabled. Fallback for any " +
             "round not listed in the per-round overrides below. See docs/SlotMachine.md.")]
    public int ludoProgressIndex;
    [Tooltip("Optional per-round override, e.g. round 1 => 20 forces LudoProgressIndex to 20 " +
             "specifically during round 1's turns (both sides). A round not listed here falls " +
             "back to the common value above.")]
    public List<RoundLudoProgressOverride> perRoundLudoProgressOverrides = new();

    /// <summary>Looks up the effective LudoProgressIndex for a given 1-indexed round — the first
    /// matching per-round override, or the common fallback value if none match.</summary>
    public int GetLudoProgressIndexForRound(int round)
    {
        foreach (var entry in perRoundLudoProgressOverrides)
            if (entry.round == round) return entry.ludoProgressIndex;
        return ludoProgressIndex;
    }

    [Serializable]
    public struct RoundLudoProgressOverride
    {
        [Tooltip("The 1-indexed round (GameManager.CurrentRound) this override applies to.")]
        public int round;
        [Tooltip("LudoProgressIndex to use during this specific round, instead of the common value above.")]
        public int ludoProgressIndex;
    }
}
