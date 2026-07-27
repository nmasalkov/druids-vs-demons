using UnityEngine;

/// <summary>
/// Abstract base for anything that can appear in an EncounterListSO's campaign sequence.
/// FightSO, LoadoutPickSO, and RewardPickSO are today's concrete subclasses; the marker exists so
/// future encounter types (shops, events, ...) can share the same list without changing
/// CampaignManager. See docs/Encounters.md.
/// </summary>
public abstract class EncounterSO : ScriptableObject
{
    /// <summary>
    /// The Encounter component to instantiate/Play when this encounter is current — see
    /// EncounterPlayer. Left unset for FightSO: a fight is driven entirely by GameManager's own
    /// round loop, not by this dispatch mechanism.
    /// </summary>
    [SerializeField] private Encounter encounterPrefab;
    public Encounter EncounterPrefab => encounterPrefab;
}
