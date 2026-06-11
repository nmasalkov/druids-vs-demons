using System.Collections.Generic;
using Game._Scripts.Creatures;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Resolver for the Shock nuke: emits up to <c>level</c> shots, one per priority target
    /// (Mage → Tank → Archer → enemy Hero). Each shot applies the Shocked status (Hero shots
    /// are no-ops, see <see cref="ShockShot"/>). No damage is dealt.
    /// </summary>
    public class ShockResolver : NukeResolver
    {
        public override void Resolve(NukeSO source, Hero caster, List<Creature> enemyCreatures,
            Hero enemyHero, int level)
        {
            Shots.Clear();

            var targets = BuildPriorityTargets(enemyCreatures, enemyHero);
            int count = level < targets.Count ? level : targets.Count;

            for (int i = 0; i < count; i++)
                Shots.Add(new ShockShot { Target = targets[i] });
        }
    }
}

