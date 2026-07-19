using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Master list of every creature/nuke/spell asset the game knows about. RunState's loadout
/// ids are resolved against this at battle start (see CampaignManager). See docs/Campaign.md.
/// </summary>
[CreateAssetMenu(fileName = "GameCatalog", menuName = "Game/Campaign/Catalog")]
public class GameCatalog : ScriptableObject
{
    public List<CreatureSO> allCreatures;
    public List<NukeSO> allNukes;
    public List<SpellSO> allSpells;

    public ArcherSO FindArcher(string id) => allCreatures.OfType<ArcherSO>().FirstOrDefault(c => c.id == id);
    public TankSO FindTank(string id) => allCreatures.OfType<TankSO>().FirstOrDefault(c => c.id == id);
    public MageSO FindMage(string id) => allCreatures.OfType<MageSO>().FirstOrDefault(c => c.id == id);
    public NukeSO FindNuke(string id) => allNukes.FirstOrDefault(n => n.id == id);
    public SpellSO FindSpell(string id) => allSpells.FirstOrDefault(s => s.id == id);
}
