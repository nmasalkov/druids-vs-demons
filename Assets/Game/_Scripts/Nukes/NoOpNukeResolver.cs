using System.Collections.Generic;
using Game._Scripts.Creatures;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Default resolver for plain <see cref="NukeSO"/> placeholder assets that have no gameplay
    /// implementation yet. Produces zero shots — the animated path will complete instantly and
    /// the instant-resolve path will apply nothing.
    /// </summary>
    public class NoOpNukeResolver : NukeResolver
    {
        public override void Resolve(NukeSO source, Hero caster, List<Creature> enemyCreatures,
            Hero enemyHero, Shield enemyShield, int level)
        {
            Shots.Clear();
        }
    }
}

