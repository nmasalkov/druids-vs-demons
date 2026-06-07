using System.Collections.Generic;
using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Resolver for the "laser" nuke: distributes a single damage pool across targets in
    /// priority order (Mage → Tank → Archer → enemy Hero). Each target absorbs only up to its
    /// current HP, so a single tanky target may consume the entire pool. Produces one
    /// <see cref="NukeShot"/> per target that actually receives damage.
    /// </summary>
    public class FireMagicResolver : NukeResolver
    {
        public override void Resolve(NukeSO source, Hero caster, List<Creature> enemyCreatures,
            Hero enemyHero, int level)
        {
            Shots.Clear();

            var targets = BuildPriorityTargets(enemyCreatures, enemyHero);
            float remaining = source.GetDamageForLevel(level);

            foreach (var target in targets)
            {
                if (remaining <= 0f) break;

                float currentHp = target.Health.CurrentHealth;
                if (currentHp <= 0f) continue;

                float dealt = Mathf.Min(remaining, currentHp);
                Shots.Add(new NukeShot { Target = target, Damage = dealt });
                remaining -= dealt;
            }
        }
    }
}

