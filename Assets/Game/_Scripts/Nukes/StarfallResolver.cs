using System.Collections.Generic;
using Game._Scripts.Creatures;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Resolver for the Starfall nuke: emits one <see cref="StarfallShot"/> per alive enemy
    /// (every creature + enemy Hero), all dealing the same flat damage from
    /// <see cref="NukeSO.GetDamageForLevel"/>. No priority, no damage pool.
    /// </summary>
    public class StarfallResolver : NukeResolver
    {
        public override void Resolve(NukeSO source, Hero caster, List<Creature> enemyCreatures,
            Hero enemyHero, Shield enemyShield, int level)
        {
            Shots.Clear();

            float damage = source.GetDamageForLevel(level);

            foreach (var creature in enemyCreatures)
            {
                if (creature == null) continue;
                if (creature.Health.IsDead()) continue;
                Shots.Add(new StarfallShot { Target = creature, Damage = damage });
            }

            if (enemyHero != null && !enemyHero.Health.IsDead())
                Shots.Add(new StarfallShot { Target = enemyHero, Damage = damage });

            if (!source.IgnoresShield && enemyShield != null && !enemyShield.Health.IsDead())
                Shots.Add(new StarfallShot { Target = enemyShield, Damage = damage });
        }
    }
}
