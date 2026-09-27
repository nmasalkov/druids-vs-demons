using System;
using Game._Scripts.Creatures;

namespace Game._Scripts.Spells
{
    /// <summary>
    /// Minimal animation host for BattleCry. There's no projectile or spawned unit — the visual
    /// is entirely the looping buff/debuff particle toggle triggered inside each shot's Apply().
    /// </summary>
    public class BattleCryAnimation : SpellActionAnimation
    {
        public override void Execute(SpellSO source, Hero caster, SpellResolver resolver, Action onComplete)
        {
            resolver.ApplyInstant();
            onComplete?.Invoke();
        }
    }
}
