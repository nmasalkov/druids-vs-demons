using System.Collections.Generic;
using System.Linq;
using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;
using UnityEngine;

namespace Game._Scripts.Spells
{
    /// <summary>
    /// Plans Charm (Robotek "Hack") outcome for a given roll: picks a target among the enemy's
    /// alive creatures that have a free destination on the caster's side, rolls success once
    /// (so the animated and instant paths share the same outcome), and queues a single
    /// <see cref="CharmShot"/>. No candidates → zero shots (silent no-op).
    /// </summary>
    public class CharmResolver : SpellResolver
    {
        public override void Resolve(SpellSO source, Hero caster, HeroView casterView,
            List<Creature> enemyCreatures, int level)
        {
            Shots.Clear();

            var charmSO = (CharmSO)source;
            var casterManager = casterView.CreaturesManager;
            var alive = enemyCreatures.Where(c => c != null && !c.Health.IsDead()).ToList();

            var candidates = BuildCandidates(alive, casterManager);
            if (candidates.Count == 0) return;

            candidates.Sort((a, b) => charmSO.GetStrength(a.Creature).CompareTo(charmSO.GetStrength(b.Creature)));
            var (target, destination) = PickByLevel(candidates, level);

            // Base odds first (scaled by how many creatures there are to steal from, clamped to 1),
            // then the creature-advantage penalty on top — a caster already ahead on board finds it
            // proportionally harder to steal. Deliberately applied after the clamp, so the penalty
            // bites even when the base chance had maxed out. See docs/ActionsAndSpells.md.
            float chance = Mathf.Clamp01(RewardBonuses.ApplyBonuses(charmSO, charmSO.GetChanceForLevel(level)) * alive.Count);
            chance *= charmSO.GetCreatureAdvantageMultiplier(CountAlive(casterManager), alive.Count);
            bool success = Random.value < chance;

            Shots.Add(new CharmShot
            {
                Target = target,
                DestinationSlot = destination,
                Success = success,
            });
        }

        /// <summary>
        /// Alive creatures on the caster's own board — the "do I already out-number them" input to
        /// the creature-advantage penalty. Counted the same way as the charmed side's <c>alive</c>
        /// list so the comparison is like-for-like.
        /// </summary>
        private static int CountAlive(CreaturesManager manager) =>
            manager.GetAllCreatures().Count(c => c != null && !c.Health.IsDead());

        /// <summary>
        /// Every alive enemy creature whose class has a free charm slot on the caster's side.
        /// The destination is always a charm slot — even for a creature being stolen back.
        /// </summary>
        private static List<(Creature Creature, UnitSlot Destination)> BuildCandidates(
            List<Creature> alive, CreaturesManager casterManager)
        {
            var candidates = new List<(Creature, UnitSlot)>();
            foreach (var creature in alive)
            {
                var destination = casterManager.GetFreeCharmSlot(creature.Data);
                if (destination == null) continue;
                candidates.Add((creature, destination));
            }
            return candidates;
        }

        private static (Creature Creature, UnitSlot Destination) PickByLevel(
            List<(Creature Creature, UnitSlot Destination)> sortedCandidates, int level)
        {
            int index = level switch
            {
                1 => 0,
                3 => sortedCandidates.Count - 1,
                _ => sortedCandidates.Count / 2,
            };
            return sortedCandidates[index];
        }
    }
}
