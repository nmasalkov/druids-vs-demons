using UnityEngine;

/// <summary>
/// +percentage damage and HP for every player creature of one class (archer/tank/mage),
/// whichever creature SO fills that class. Read by RewardBonuses.ApplyCreatureBonus from
/// AttacksResolver (damage) and Creature.GetMaxHealth (HP). See docs/Rewards.md.
/// </summary>
[CreateAssetMenu(fileName = "CreatureClassBoost", menuName = "Game/Campaign/Rewards/Creature Class Boost")]
public class CreatureClassBoostSO : BoostSO
{
    public enum CreatureClass { Archer, Tank, Mage }

    [Tooltip("Which creature class this boost applies to. Matches by CreatureSO subtype (ArcherSO/TankSO/MageSO), so every creature of that class is covered, Big variants included.")]
    public CreatureClass creatureClass;

    [Tooltip("Fraction added to damage and max HP of the player's creatures of this class (0.2 = +20%). Never applies to the enemy. See docs/Rewards.md.")]
    [Range(0f, 1f)] public float percentage = 0.2f;

    public bool Matches(CreatureSO data) => creatureClass switch
    {
        CreatureClass.Archer => data is ArcherSO,
        CreatureClass.Tank => data is TankSO,
        CreatureClass.Mage => data is MageSO,
        _ => false,
    };
}
