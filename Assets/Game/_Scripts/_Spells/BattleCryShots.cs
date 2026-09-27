using Game._Scripts.Creatures;

namespace Game._Scripts.Spells
{
    /// <summary>Applies the BattleCry attack-damage buff to one of the caster's own creatures.</summary>
    public class BattleCryBuffShot : SpellShot
    {
        public float Multiplier;

        public override void Apply()
        {
            ((Creature)Target).StatusesManager.ApplyBattleCryBuff(Multiplier);
        }
    }

    /// <summary>Applies the BattleCry attack-damage debuff to one of the enemy's creatures.</summary>
    public class BattleCryDebuffShot : SpellShot
    {
        public float Multiplier;

        public override void Apply()
        {
            ((Creature)Target).StatusesManager.ApplyBattleCryDebuff(Multiplier);
        }
    }
}
