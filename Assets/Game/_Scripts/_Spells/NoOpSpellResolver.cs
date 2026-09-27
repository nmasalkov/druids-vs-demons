using System.Collections.Generic;
using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;

namespace Game._Scripts.Spells
{
    /// <summary>
    /// Default resolver for placeholder <see cref="SpellSO"/> assets — produces zero shots.
    /// </summary>
    public class NoOpSpellResolver : SpellResolver
    {
        public override void Resolve(SpellSO source, Hero caster, HeroView casterView,
            List<Creature> enemyCreatures, int level)
        {
            Shots.Clear();
        }
    }
}

