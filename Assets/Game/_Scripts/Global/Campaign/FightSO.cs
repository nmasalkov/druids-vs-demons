using System;
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
    public GameObject enemyAvatarPrefab;
    public int hp;

    [Tooltip("Weight (0-100) that the AI won't go with its calculated best decision and degrades to " +
             "the next-best one instead (see AIDegrade). Decisions marked NO STUPID ignore this.")]
    [Range(0, 100)] public int stupidityChance;
    [Tooltip("Rolled only if stupidity triggers. If it also triggers, the AI picks a fully random " +
             "option instead of degrading to the next-best one.")]
    [Range(0, 100)] public int criticalFailureChance;
    [Tooltip("Total rerolls the AI can spend across the WHOLE fight (not per turn) - depletes as it " +
             "rerolls and never refills until the fight restarts. See docs/AI.md.")]
    public int rerollsAmount;
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
    [FormerlySerializedAs("battleId")] public string fightId;
    public bool isTutorial;
    public EnemyData enemyData;

    [Tooltip("If true, a loadout-pick phase plays in MapScene, in place at this node, before advancing into the fight.")]
    public bool hasLoadoutPick;
    [Tooltip("If true, a reward-pick phase plays as an overlay inside BattleScene right after victory, before this encounter completes.")]
    public bool hasReward;
    [Tooltip("Guaranteed energy reward granted when the reward phase plays. Only meaningful when hasReward is true.")]
    public int rewardAmount;
}
