using System.Collections.Generic;
using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;

namespace Game._Scripts.Spells
{
    /// <summary>
    /// Plans BattleCry: buffs every creature on the caster's own board and debuffs every
    /// creature on the enemy's board, both by a flat attack-damage multiplier for the level.
    /// </summary>
    public class BattleCryResolver : SpellResolver
    {
        public override void Resolve(SpellSO source, Hero caster, HeroView casterView,
            List<Creature> enemyCreatures, int level)
        {
            Shots.Clear();

            var battleCrySO = (BattleCrySO)source;
            float buffMultiplier = BoostedBonusPart(battleCrySO, caster, battleCrySO.GetBuffMultiplierForLevel(level));
            float debuffMultiplier = battleCrySO.GetDebuffMultiplierForLevel(level);

            foreach (var own in casterView.CreaturesManager.GetAllCreatures())
                Shots.Add(new BattleCryBuffShot { Target = own, Multiplier = buffMultiplier });

            foreach (var enemy in enemyCreatures)
                Shots.Add(new BattleCryDebuffShot { Target = enemy, Multiplier = debuffMultiplier });
        }

        /// <summary>
        /// A Battle Cry stat boost (ActionStatBoostSO, player only) scales only the bonus part of the
        /// buff: ×1.24 with a 0.6 boost → 1 + 0.24 × 1.6 = ×1.384. See docs/Rewards.md.
        /// </summary>
        private static float BoostedBonusPart(BattleCrySO battleCrySO, Hero caster, float multiplier)
            => 1f + (multiplier - 1f) * (1f + RewardBonuses.ActionBonusFraction(battleCrySO, IsPlayer(caster)));
    }
}
