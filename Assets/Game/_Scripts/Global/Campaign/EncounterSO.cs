using UnityEngine;

/// <summary>
/// Abstract base for anything that can appear in an EncounterListSO's campaign sequence.
/// BattleSO is the only concrete subclass today; the marker exists so future encounter types
/// (shops, events, ...) can share the same list without changing CampaignProgressManager. See
/// docs/Encounters.md.
/// </summary>
public abstract class EncounterSO : ScriptableObject
{
}
