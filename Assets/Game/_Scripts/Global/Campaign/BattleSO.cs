using System;
using UnityEngine;

/// <summary>
/// Enemy side data for one battle: which avatar to spawn and how much HP it has. Expandable
/// later (e.g. an AI tactic enum) without touching BattleSO itself. See docs/Encounters.md.
/// </summary>
[Serializable]
public struct EnemyData
{
    public GameObject enemyAvatarPrefab;
    public int hp;
}

/// <summary>
/// Data for one campaign battle: which enemy to fight and what it's worth. battleId/isTutorial/
/// energyReward aren't consumed by any logic yet — they're forward-looking data for the
/// pre-battle/post-battle phases this pass doesn't build. See docs/Encounters.md.
/// </summary>
[CreateAssetMenu(fileName = "Battle", menuName = "Game/Campaign/Battle")]
public class BattleSO : EncounterSO
{
    public string battleId;
    public bool isTutorial;
    public EnemyData enemyData;
    public int energyReward;
}
