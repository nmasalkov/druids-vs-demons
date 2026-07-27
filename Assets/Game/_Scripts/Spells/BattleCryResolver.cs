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
            float buffMultiplier = RewardBonuses.ApplyBonuses(battleCrySO, battleCrySO.GetBuffMultiplierForLevel(level));
            float debuffMultiplier = battleCrySO.GetDebuffMultiplierForLevel(level);

            foreach (var own in casterView.CreaturesManager.GetAllCreatures())
                Shots.Add(new BattleCryBuffShot { Target = own, Multiplier = buffMultiplier });

            foreach (var enemy in enemyCreatures)
                Shots.Add(new BattleCryDebuffShot { Target = enemy, Multiplier = debuffMultiplier });
        }
    }
}
