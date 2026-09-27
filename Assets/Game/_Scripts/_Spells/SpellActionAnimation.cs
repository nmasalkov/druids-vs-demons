using System;
using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.Spells
{
    /// <summary>
    /// Base class for spell action animation prefabs (view layer only). Receives a
    /// <see cref="SpellResolver"/> with pre-computed shots and plays the visuals — no gameplay
    /// computation here. The instant-resolve path skips animations and goes through
    /// <see cref="SpellResolver.ApplyInstant"/>.
    /// </summary>
    public abstract class SpellActionAnimation : ActionAnimation
    {
        public sealed override void Execute(ActionSO source, Hero caster, ActionResolver resolver, Action onComplete)
            => Execute((SpellSO)source, caster, (SpellResolver)resolver, onComplete);

        public abstract void Execute(SpellSO source, Hero caster, SpellResolver resolver, Action onComplete);
    }
}
