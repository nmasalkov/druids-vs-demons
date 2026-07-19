using System;

/// <summary>
/// Persisted meta-progression state for the current campaign run. Plain data (no
/// ScriptableObject, no MonoBehaviour) so it round-trips through JsonUtility for
/// PlayerPrefs storage. See docs/Campaign.md.
/// </summary>
[Serializable]
public class RunState
{
    public int saveVersion = 1;

    public int maxHp = 100;
    public int energyCapacity = 50;

    // Defaults mirror today's _DefaultCreatures.asset/DefaultNukes.asset/DefaultSpells.asset
    // assignments exactly, so a fresh/no-save run resolves to the same loadout the game already
    // ships with. See the id assignments in docs/Campaign.md's Editor setup checklist.
    public string archerId = "archer";
    public string tankId = "tank";
    public string mageId = "mage";

    public string nukeAId = "firemagic";
    public string nukeBId = "starfall";
    public string nukeCId = "shock";

    public string spellAId = "battlecry";
    public string spellBId = "charm";
    public string spellCId = "shield";

    public int currentEncounterIndex = 0;
}
