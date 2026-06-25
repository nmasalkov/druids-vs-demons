using System;
using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.Spells
{
    public class NoOpSpellAnimation : SpellActionAnimation
    {
        public override void Execute(SpellSO source, Hero caster, SpellResolver resolver, Action onComplete)
        {
            Debug.Log($"[Spell] no-op '{source.actionName}' cast");
            onComplete?.Invoke();
        }
    }
}

