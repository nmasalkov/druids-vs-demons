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
/// Data for one campaign fight: which enemy to face. fightId/isTutorial aren't consumed by
/// any logic yet — forward-looking data for a future pre-battle phase. The energy reward for
/// winning is no longer part of a fight itself — see RewardPickSO and docs/Encounters.md.
/// Renamed from BattleSO — campaign-layer naming only; the per-turn combat-resolution machinery
/// (BattleState, RunBattle(), AttacksResolver) keeps its own "Battle" naming, unrelated to this.
/// </summary>
[MovedFrom(true, sourceClassName: "BattleSO")]
[CreateAssetMenu(fileName = "Fight", menuName = "Game/Campaign/Fight")]
public class FightSO : EncounterSO
{
    [FormerlySerializedAs("battleId")] public string fightId;
    public bool isTutorial;
    public EnemyData enemyData;
}
