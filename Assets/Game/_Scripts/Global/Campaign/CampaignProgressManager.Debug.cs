using UnityEngine;

/// <summary>
/// Debug-only surface, called solely by CampaignProgressTool/CampaignDebugTool — kept out of
/// CampaignProgressManager.cs so that file stays pure game-navigation logic. See CLAUDE.md rule 20.
/// </summary>
public partial class CampaignProgressManager
{
    // Only open during the Awake phase of the scene load where this object itself was first
    // created (CampaignProgressManager.cs's Awake()/Start()) — closed forever after, so a debug
    // override applied at session start can never leak into later navigation. See
    // SetSessionEncounterOverride()/SetSessionEncounterIndexOverride() below.
    private bool _overrideWindowOpen;

    /// <summary>
    /// Session-only override used by CampaignProgressTool — never persisted, mirrors
    /// CampaignDebugTool's overrides never calling Save(). Only takes effect during the very first
    /// scene load of a session (see _overrideWindowOpen) — CampaignProgressTool's Awake() re-fires
    /// on every scene load while its checkbox stays checked, and without this guard that would
    /// silently override every subsequent AdvanceToNextEncounter()/StartNewRun() call back to the
    /// same encounter.
    /// </summary>
    public void SetSessionEncounterOverride(EncounterSO encounter)
    {
        var idx = encounterList.encounters.IndexOf(encounter);
        if (idx < 0)
        {
            Debug.LogWarning($"CampaignProgressManager: {(encounter != null ? encounter.name : "null")} not found in encounter list.");
            return;
        }

        SetSessionEncounterIndexOverride(idx);
    }

    /// <summary>
    /// Same session-only override as SetSessionEncounterOverride, but by raw index — used by
    /// CampaignDebugTool's "Use Debug Profile" (CampaignProfileSO.currentEncounterIndex), which has
    /// no EncounterListSO reference to resolve an EncounterSO through. Without this,
    /// CampaignDebugTool mutating CampaignManager.CurrentRun.currentEncounterIndex directly would
    /// have no effect — CampaignProgressManager bootstraps its own index from PlayerPrefs
    /// independently (see docs/Encounters.md), never from the live CurrentRun object.
    /// </summary>
    public void SetSessionEncounterIndexOverride(int index)
    {
        if (!_overrideWindowOpen)
        {
            Debug.LogWarning("CampaignProgressManager: encounter override only applies at session start; ignoring (already past startup).");
            return;
        }

        _currentEncounterIndex = Mathf.Clamp(index, 0, encounterList.encounters.Count - 1);
    }
}
