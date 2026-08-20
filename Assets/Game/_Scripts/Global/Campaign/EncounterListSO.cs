using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// The ordered campaign sequence — indexed by RunState.currentEncounterIndex /
/// CampaignManager.CurrentEncounterIndex. See docs/Encounters.md.
/// </summary>
[CreateAssetMenu(fileName = "EncounterList", menuName = "Game/Campaign/Encounter List")]
public class EncounterListSO : ScriptableObject
{
    [FormerlySerializedAs("encounters")] public List<FightSO> fights;
}
