using System.Collections.Generic;
using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Resolver for the Starfall nuke: emits one <see cref="StarfallShot"/> per alive enemy
    /// (every creature + enemy Hero), all dealing the same flat damage from
    /// <see cref="NukeSO.GetDamageForLevel"/>. No priority, no damage pool.
    ///
    /// **Improved (<see cref="StarfallImprovementSO"/>, player only):** appends extra strikes
    /// (<see cref="StarfallShot.IsExtraStrike"/>), each at a random target still alive after the
    /// main volley (simulated HP), fired by the animation after a short pause.
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

            if (IsImproved(caster, out StarfallImprovementSO improvement))
                AddExtraStrikes(improvement.extraStrikes, damage);
        }

        /// <summary>
        /// Each extra strike picks a random main-volley target whose simulated HP (after every
        /// hit planned so far, extra strikes included) is still above 0. Stops early if none are left.
        /// </summary>
        private void AddExtraStrikes(int count, float damage)
        {
            var simHp = new Dictionary<Targetable, float>();
            foreach (var shot in Shots)
                simHp[shot.Target] = shot.Target.Health.CurrentHealth - damage;

            for (int i = 0; i < count; i++)
            {
                var target = PickRandomSurvivor(simHp);
                if (target == null) return;
                Shots.Add(new StarfallShot { Target = target, Damage = damage, IsExtraStrike = true });
                simHp[target] -= damage;
            }
        }

        private static Targetable PickRandomSurvivor(Dictionary<Targetable, float> simHp)
        {
            var survivors = new List<Targetable>();
            foreach (var pair in simHp)
            {
                if (pair.Value > 0f) survivors.Add(pair.Key);
            }
            return survivors.Count == 0 ? null : survivors[Random.Range(0, survivors.Count)];
        }
    }
}
