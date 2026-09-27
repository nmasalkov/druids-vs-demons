using System;
using Game._Scripts.Creatures;

namespace Game._Scripts.Spells
{
    /// <summary>
    /// Minimal animation host for the Shield spell. The actual grow-in tween lives in the
    /// summoned shield's ShieldAnimator, so this just fires the planned shots and completes.
    /// </summary>
    public class ShieldSpellAnimation : SpellActionAnimation
    {
        public override void Execute(SpellSO source, Hero caster, SpellResolver resolver, Action onComplete)
        {
            resolver.ApplyInstant();
            onComplete?.Invoke();
        }
    }
}

