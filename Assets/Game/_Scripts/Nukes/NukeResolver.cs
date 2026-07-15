using System.Collections.Generic;
using Game._Scripts.Creatures;
namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Base for nuke "resolvers" — the data-layer counterpart of <see cref="NukeActionAnimation"/>.
    /// Computes WHAT happens (which targets, how much damage); animation plays HOW it happens.
    /// Mirrors the AttacksResolver + BattleState split used for normal creature battles.
    /// </summary>
    public abstract class NukeResolver : ActionResolver
    {
        public List<NukeShot> Shots { get; } = new();
        public abstract void Resolve(NukeSO source, Hero caster, List<Creature> enemyCreatures,
            Hero enemyHero, Shield enemyShield, int level);
        public override void ApplyInstant()
        {
            foreach (var s in Shots)
                s.Apply();
        }
        protected static List<Targetable> BuildPriorityTargets(List<Creature> enemyCreatures, Hero enemyHero,
            Shield enemyShield, bool ignoresShield)
        {
            var targets = new List<Targetable>();
            if (!ignoresShield)
                AddIfAlive(targets, enemyShield);
            AddAllByData<MageSO>(targets, enemyCreatures);
            AddAllByData<TankSO>(targets, enemyCreatures);
            AddAllByData<ArcherSO>(targets, enemyCreatures);
            AddIfAlive(targets, enemyHero);
            return targets;
        }
        /// <summary>
        /// Appends every alive creature of a class, not just the first (charm slots can hold up
        /// to 3 creatures of the same class on a board).
        /// </summary>
        private static void AddAllByData<TData>(List<Targetable> targets, List<Creature> creatures) where TData : CreatureSO
        {
            foreach (var c in creatures)
            {
                if (c == null) continue;
                if (c.Data is TData) AddIfAlive(targets, c);
            }
        }
        private static void AddIfAlive(List<Targetable> list, Targetable unit)
        {
            if (unit == null) return;
            if (unit.Health.IsDead()) return;
            list.Add(unit);
        }
    }
}
