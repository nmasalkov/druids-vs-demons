using System;
using System.Collections.Generic;

/// <summary>
/// Persisted meta-progression state for the current campaign run. Plain data (no
/// ScriptableObject, no MonoBehaviour) so it round-trips through JsonUtility for storage via
/// SaveStorage.Backend (PlayerPrefs by default). See docs/Campaign.md.
/// </summary>
[Serializable]
public class RunState
{
    public const int CurrentSaveVersion = 1;

    public int saveVersion = CurrentSaveVersion;

    public int maxHp = 100;
    public int energyCapacity = 50;

    // The only RunState field a battle itself changes (via reroll spend + victory reward) —
    // persists across encounters; every other field only changes through explicit campaign
    // navigation/debug actions. See docs/Encounters.md.
    public int currentEnergy = 50;

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

    // Reward-pick tracking, see docs/Rewards.md. Ids resolved via RewardListSO.Find(). Default-
    // initialized so legacy saves lacking these keys deserialize to empty lists, not null.
    public List<string> statusRewardIds = new List<string>();
    public List<string> boostRewardIds = new List<string>();
    public List<string> gatheredCreatureIds = new List<string>();
}
