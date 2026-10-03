using System.Collections.Generic;
using Game._Scripts.Creatures;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Resolver for the Shock nuke: emits up to <c>level</c> shots, one per priority target
    /// (Mage → Tank → Archer → enemy Hero). Each shot applies the Shocked status (Hero shots
    /// are no-ops, see <see cref="ShockShot"/>). No damage is dealt. Unshocked creatures are
    /// always picked before already-shocked ones (see <see cref="UnshockedFirst"/>).
    ///
    /// **A standing enemy shield blocks Shock completely** — at any level, 1/2/3 alike. Nothing
    /// behind the shield is shocked while it's up; every bolt is spent on the shield itself and
    /// does nothing there. That's the whole reason Shock resolves last of the nukes
    /// (<see cref="NukeSO.ResolutionOrder"/> 30): a damage nuke in the same roll gets the chance
    /// to break the shield first, and only then does Shock reach the creatures.
    ///
    /// **Improved (<see cref="ShockImprovementSO"/>, player only):** bolts damage the shield
    /// instead; once the simulated shield HP hits 0, the remaining bolts carry on to the normal
    /// priority targets behind it.
    /// </summary>
    public class ShockResolver : NukeResolver
    {
        public override void Resolve(NukeSO source, Hero caster, List<Creature> enemyCreatures,
            Hero enemyHero, Shield enemyShield, int level)
        {
            Shots.Clear();

            if (!IsAlive(enemyShield))
            {
                AddShockShots(BuildPriorityTargets(enemyCreatures, enemyHero, enemyShield, source.IgnoresShield), level);
                return;
            }

            if (IsImproved(caster, out ShockImprovementSO improvement))
            {
                AddShieldBreakingShots(enemyShield, improvement.shieldDamagePerBolt, enemyCreatures, enemyHero, level);
                return;
            }

            AddBlockedShots(enemyShield, level);
        }

        private void AddShockShots(List<Targetable> priorityTargets, int count)
        {
            var targets = UnshockedFirst(priorityTargets);
            int shots = count < targets.Count ? count : targets.Count;
            for (int i = 0; i < shots; i++)
                Shots.Add(new ShockShot { Target = targets[i] });
        }

        /// <summary>
        /// Stable reorder: unshocked creatures first, then everything else (already-shocked
        /// creatures, then the Hero) in their original priority order. Shocked is a plain on/off
        /// flag, so a bolt into an already-shocked creature would be wasted.
        /// </summary>
        private static List<Targetable> UnshockedFirst(List<Targetable> targets)
        {
            var ordered = new List<Targetable>(targets.Count);
            foreach (var t in targets)
                if (IsFreshShockTarget(t)) ordered.Add(t);
            foreach (var t in targets)
                if (!IsFreshShockTarget(t)) ordered.Add(t);
            return ordered;
        }

        private static bool IsFreshShockTarget(Targetable target)
            => target is Creature creature && !creature.StatusesManager.IsShocked;

        /// <summary>
        /// Shield is up: aim every bolt at the shield instead of the board behind it. These shots
        /// are deliberately real (not an empty list) so the animation still shows the shock being
        /// eaten by the shield rather than the turn silently doing nothing — and they're harmless,
        /// since <see cref="ShockShot"/> only shocks <see cref="Unit"/>s and deals no damage.
        /// </summary>
        private void AddBlockedShots(Shield shield, int level)
        {
            for (int i = 0; i < level; i++)
                Shots.Add(new ShockShot { Target = shield });
        }

        /// <summary>
        /// Improved Shock into a standing shield: bolts hit the shield (each dealing
        /// <paramref name="shieldDamage"/>) until its simulated HP runs out, then the leftover
        /// bolts go to the priority targets behind it.
        /// </summary>
        private void AddShieldBreakingShots(Shield shield, float shieldDamage, List<Creature> enemyCreatures,
            Hero enemyHero, int level)
        {
            float shieldHp = shield.Health.CurrentHealth;
            int bolts = level;
            while (bolts > 0 && shieldHp > 0f)
            {
                Shots.Add(new ShockShot { Target = shield, ShieldDamage = shieldDamage });
                shieldHp -= shieldDamage;
                bolts--;
            }

            AddShockShots(BuildPriorityTargets(enemyCreatures, enemyHero, shield, ignoresShield: true), bolts);
        }
    }
}
