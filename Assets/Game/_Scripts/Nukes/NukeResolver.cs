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
        private static void AddIfAlive(List<Targetable> list, Targetable unit)
        {
            if (unit == null) return;
            if (unit.Health.IsDead()) return;
            list.Add(unit);
        }
    }
}
