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
