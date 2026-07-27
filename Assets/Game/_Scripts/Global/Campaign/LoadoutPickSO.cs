using UnityEngine;

/// <summary>
/// Placeholder pre-battle encounter: a full-screen tint + briefing text, dismissed on any key
/// press. Played by EncounterPlayer. See docs/Encounters.md.
/// </summary>
[CreateAssetMenu(fileName = "LoadoutPick", menuName = "Game/Campaign/Loadout Pick")]
public class LoadoutPickSO : EncounterSO
{
    [TextArea(2, 5)]
    public string briefingText = "This will be a battle!";
}
