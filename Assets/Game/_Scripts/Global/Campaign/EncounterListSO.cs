using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ordered campaign sequence — indexed by RunState.currentEncounterIndex /
/// CampaignManager.CurrentEncounterIndex. See docs/Encounters.md.
/// </summary>
[CreateAssetMenu(fileName = "EncounterList", menuName = "Game/Campaign/Encounter List")]
public class EncounterListSO : ScriptableObject
{
    public List<EncounterSO> encounters;
}
