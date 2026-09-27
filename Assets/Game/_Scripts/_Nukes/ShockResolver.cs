using System.Collections.Generic;
using Game._Scripts.Creatures;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Resolver for the Shock nuke: emits up to <c>level</c> shots, one per priority target
    /// (Mage → Tank → Archer → enemy Hero). Each shot applies the Shocked status (Hero shots
    /// are no-ops, see <see cref="ShockShot"/>). No damage is dealt.
    ///
    /// **A standing enemy shield blocks Shock completely** — at any level, 1/2/3 alike. Nothing
    /// behind the shield is shocked while it's up; every bolt is spent on the shield itself and
    /// does nothing there. That's the whole reason Shock resolves last of the nukes
    /// (<see cref="NukeSO.ResolutionOrder"/> 30): a damage nuke in the same roll gets the chance
    /// to break the shield first, and only then does Shock reach the creatures.
    /// </summary>
    public class ShockResolver : NukeResolver
    {
        public override void Resolve(NukeSO source, Hero caster, List<Creature> enemyCreatures,
            Hero enemyHero, Shield enemyShield, int level)
        {
            Shots.Clear();

            if (IsAlive(enemyShield))
            {
                AddBlockedShots(enemyShield, level);
                return;
            }

            AddShockShots(BuildPriorityTargets(enemyCreatures, enemyHero, enemyShield, source.IgnoresShield), level);
        }

        private void AddShockShots(List<Targetable> targets, int level)
        {
            int count = level < targets.Count ? level : targets.Count;
            for (int i = 0; i < count; i++)
                Shots.Add(new ShockShot { Target = targets[i] });
        }

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
    }
}

