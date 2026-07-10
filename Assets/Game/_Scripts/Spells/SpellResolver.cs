using System.Collections.Generic;
using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;

namespace Game._Scripts.Spells
{
    /// <summary>
    /// Data-layer counterpart of <see cref="SpellActionAnimation"/>. Computes WHAT happens
    /// (where to spawn/heal); the animation plays HOW it happens. Mirrors <c>NukeResolver</c>.
    /// </summary>
    public abstract class SpellResolver : ActionResolver
    {
        public List<SpellShot> Shots { get; } = new();

        /// <summary>
        /// Plan the spell. <paramref name="casterView"/> is the caster's <see cref="HeroView"/>
        /// — spells that affect the caster's own board (e.g. summoning Shield) read their
        /// own slots from it. <paramref name="enemyCreatures"/> mirrors <c>NukeResolver.Resolve</c>
        /// for spells that also need to reach the enemy's board (e.g. BattleCry's debuff).
        /// </summary>
        public abstract void Resolve(SpellSO source, Hero caster, HeroView casterView,
            List<Creature> enemyCreatures, int level);

        public override void ApplyInstant()
        {
            foreach (var s in Shots)
                s.Apply();
        }
    }
}

