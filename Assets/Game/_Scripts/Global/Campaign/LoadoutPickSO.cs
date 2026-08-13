using UnityEngine;

/// <summary>
/// Marker EncounterSO for the pre-battle loadout picker — carries no data of its own;
/// LoadoutPickEncounter derives everything from RunState/GameCatalog. Played by
/// LoadoutPickEncounter/LoadoutPickEncounterView (CLAUDE.md rule 28). See docs/Loadout.md.
/// </summary>
[CreateAssetMenu(fileName = "LoadoutPick", menuName = "Game/Campaign/Loadout Pick")]
public class LoadoutPickSO : EncounterSO
{
}
