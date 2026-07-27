using UnityEngine;

/// <summary>
/// Debug-only surface, called solely by CampaignDebugTool/CampaignProgressTool — kept out of
/// CampaignStateManager.cs so that file stays pure data-ownership logic. See CLAUDE.md rule 20.
/// </summary>
public partial class CampaignStateManager
{
    // Only open during the Awake phase of the scene load where this object itself was first
    // created (CampaignStateManager.cs's Awake()/Start()) — closed forever after, so a debug
    // override applied at session start can never leak into later navigation. See
    // SetSessionEncounterIndexOverride() below.
    private bool _overrideWindowOpen;

    /// <summary>
    /// Session-only override of RunState.currentEncounterIndex — the data-only half of
    /// CampaignManager.SetSessionEncounterIndexOverride(), which does the clamping (it owns
    /// EncounterList) and calls this with an already-valid index. Never persisted, mirrors
    /// CampaignDebugTool's overrides never calling Save(). Only takes effect during the very first
    /// scene load of a session (see _overrideWindowOpen) — CampaignProgressTool's Awake() re-fires on
    /// every scene load while its checkbox stays checked, and without this guard that would silently
    /// override every subsequent AdvanceToNextEncounter()/StartNewRun() call back to the same
    /// encounter.
    /// </summary>
    public void ApplySessionEncounterIndexOverride(int clampedIndex)
    {
        if (!_overrideWindowOpen)
        {
            Debug.LogWarning("CampaignStateManager: encounter override only applies at session start; ignoring (already past startup).");
            return;
        }

        CurrentRun.currentEncounterIndex = clampedIndex;
    }

    /// <summary>
    /// Parses a pasted RunState JSON blob through the exact same validation real saves go through
    /// (unparseable/unsupported-version JSON returns null) — lets CampaignDebugTool's "Use External
    /// Save" exercise the real save-parsing path without writing anything to storage. See
    /// docs/Campaign.md.
    /// </summary>
    public static RunState ParseExternalRunState(string json) => ParseRunState(json);
}
