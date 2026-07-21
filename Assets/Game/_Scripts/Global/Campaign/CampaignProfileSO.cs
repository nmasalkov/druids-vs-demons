using UnityEngine;

/// <summary>
/// A reusable, Editor-authorable snapshot of a RunState — same fields, but direct SO references
/// instead of id strings (like CampaignDebugTool's existing per-field overrides), so a whole test
/// scenario can be saved as one asset and dragged into CampaignDebugTool's "Use Debug Profile"
/// slot instead of re-checking a dozen boxes every session. See docs/Campaign.md.
///
/// Reminder (CLAUDE.md rule 21): any new RunState field needs a matching field here too.
/// </summary>
[CreateAssetMenu(fileName = "CampaignProfile", menuName = "Game/Campaign/Campaign Profile")]
public class CampaignProfileSO : ScriptableObject
{
    [Header("Stats")]
    public int maxHp = 100;
    public int energyCapacity = 50;
    public int currentEnergy = 50;

    [Header("Creatures")]
    public ArcherSO archer;
    public TankSO tank;
    public MageSO mage;

    [Header("Nukes")]
    public NukeSO nukeA;
    public NukeSO nukeB;
    public NukeSO nukeC;

    [Header("Spells")]
    public SpellSO spellA;
    public SpellSO spellB;
    public SpellSO spellC;

    [Header("Progress")]
    public int currentEncounterIndex = 0;
}
