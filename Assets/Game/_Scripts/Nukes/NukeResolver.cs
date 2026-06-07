using System.Collections.Generic;
using Game._Scripts.Creatures;
namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Base for nuke "resolvers" — the data-layer counterpart of <see cref="NukeActionAnimation"/>.
    /// Computes WHAT happens (which targets, how much damage); animation plays HOW it happens.
    /// Mirrors the AttacksResolver + BattleState split used for normal creature battles.
    /// </summary>
    public abstract class NukeResolver
    {
        public List<NukeShot> Shots { get; } = new();
        public abstract void Resolve(NukeSO source, Hero caster, List<Creature> enemyCreatures,
            Hero enemyHero, int level);
        public void ApplyInstant()
        {
            foreach (var s in Shots)
                s.Apply();
        }
        protected static List<Unit> BuildPriorityTargets(List<Creature> enemyCreatures, Hero enemyHero)
        {
            var targets = new List<Unit>();
            AddIfAlive(targets, FindByData<MageSO>(enemyCreatures));
            AddIfAlive(targets, FindByData<TankSO>(enemyCreatures));
            AddIfAlive(targets, FindByData<ArcherSO>(enemyCreatures));
            AddIfAlive(targets, enemyHero);
            return targets;
        }
        private static Creature FindByData<TData>(List<Creature> creatures) where TData : CreatureSO
        {
            foreach (var c in creatures)
            {
                if (c == null) continue;
                if (c.Data is TData) return c;
            }
            return null;
        }
        private static void AddIfAlive(List<Unit> list, Unit unit)
        {
            if (unit == null) return;
            if (unit.Health.IsDead()) return;
            list.Add(unit);
        }
    }
}
